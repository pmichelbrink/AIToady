"""Roman Imperial coin type lookup in OCRE (Online Coins of the Roman Empire).

Data comes from the Nomisma.org SPARQL endpoint, which holds the OCRE type corpus.
All coin types for a ruler are fetched once, cached on disk, and scored locally
against what the vision model read. Scoring locally tolerates worn or partly misread
legends, which exact-match server searches do not.
"""

import asyncio
import difflib
import json
import logging
import re
import time
import unicodedata
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Optional

import httpx

from .schemas import CatalogCandidate

log = logging.getLogger(__name__)

SPARQL_URL = "https://nomisma.org/query"
NOMISMA_ID = "http://nomisma.org/id/"
OCRE_TYPE = "http://numismatics.org/ocre/id/"
PAGE_SIZE = 3000
MAX_PAGES = 6
CACHE_VERSION = 1

STOPWORDS = {
    "and", "the", "with", "holding", "left", "right", "her", "his", "its", "in", "on", "of",
    "to", "from", "or", "a", "an", "at", "each", "hand", "which", "who", "is", "are", "coin",
    "standing", "seated", "facing", "above", "below", "between", "two", "one", "small",
}

# Common English names -> Nomisma ids used in OCRE.
MINT_ALIASES = {
    "antioch": "antiocheia_syria", "alexandria": "alexandreia_egypt", "trier": "treveri",
    "treveri": "treveri", "lyon": "lugdunum", "lyons": "lugdunum", "london": "londinium",
    "milan": "mediolanum", "arles": "arelate", "heraclea": "heraclea_thracica",
    "nicomedia": "nicomedia", "cyzicus": "cyzicus", "siscia": "siscia", "sirmium": "sirmium",
    "thessalonica": "thessalonica", "thessaloniki": "thessalonica", "serdica": "serdica",
    "sofia": "serdica", "ticinum": "ticinum", "pavia": "ticinum", "aquileia": "aquileia",
    "carthage": "carthage", "ostia": "ostia", "rome": "rome", "constantinople": "constantinople",
}
# OCRE describes late Roman bronzes by size (AE1-AE4) rather than "follis".
DENOMINATION_ALIASES = {
    "follis": {"ae1", "ae2", "ae3", "ae4"}, "nummus": {"ae1", "ae2", "ae3", "ae4"},
    "centenionalis": {"ae2", "ae3"}, "maiorina": {"ae2"}, "radiate": {"antoninianus"},
    "aurelianianus": {"antoninianus"}, "half_solidus": {"half_solidus", "half-solidus", "semissis"},
}

TYPES_QUERY = """
PREFIX nmo: <http://nomisma.org/ontology#>
PREFIX dcterms: <http://purl.org/dc/terms/>
PREFIX void: <http://rdfs.org/ns/void#>
SELECT ?type (SAMPLE(?obvLeg) AS ?obv) (SAMPLE(?revLeg) AS ?rev) (SAMPLE(?revDesc) AS ?revd)
       (GROUP_CONCAT(DISTINCT STR(?den); separator="|") AS ?dens)
       (GROUP_CONCAT(DISTINCT STR(?mint); separator="|") AS ?mints)
WHERE {
  { ?type nmo:hasAuthority <%(ruler)s> }
  UNION { ?type nmo:hasObverse ?po . ?po nmo:hasPortrait <%(ruler)s> }
  ?type void:inDataset <http://numismatics.org/ocre/> .
  OPTIONAL { ?type nmo:hasObverse ?o . ?o nmo:hasLegend ?obvLeg }
  OPTIONAL { ?type nmo:hasReverse ?r .
             OPTIONAL { ?r nmo:hasLegend ?revLeg }
             OPTIONAL { ?r dcterms:description ?revDesc FILTER(langMatches(lang(?revDesc), "en")) } }
  OPTIONAL { ?type nmo:hasDenomination ?den }
  OPTIONAL { ?type nmo:hasMint ?mint }
}
GROUP BY ?type
ORDER BY ?type
LIMIT %(limit)d OFFSET %(offset)d
"""

DETAILS_QUERY = """
PREFIX nmo: <http://nomisma.org/ontology#>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
PREFIX dcterms: <http://purl.org/dc/terms/>
SELECT ?type ?label ?denLabel ?mintLabel ?start ?end ?obvDesc WHERE {
  VALUES ?type { %(values)s }
  ?type skos:prefLabel ?label . FILTER(langMatches(lang(?label), "en"))
  OPTIONAL { ?type nmo:hasDenomination ?den . ?den skos:prefLabel ?denLabel .
             FILTER(langMatches(lang(?denLabel), "en")) }
  OPTIONAL { ?type nmo:hasMint ?mint . ?mint skos:prefLabel ?mintLabel .
             FILTER(langMatches(lang(?mintLabel), "en")) }
  OPTIONAL { ?type nmo:hasStartDate ?start }
  OPTIONAL { ?type nmo:hasEndDate ?end }
  OPTIONAL { ?type nmo:hasObverse ?o . ?o dcterms:description ?obvDesc .
             FILTER(langMatches(lang(?obvDesc), "en")) }
}
"""


class CatalogError(RuntimeError):
    """Raised when the catalogue cannot be queried."""


@dataclass
class TypeRecord:
    id: str
    obverse_legend: str = ""
    reverse_legend: str = ""
    reverse_description: str = ""
    denominations: list[str] = field(default_factory=list)
    mints: list[str] = field(default_factory=list)


@dataclass
class CoinReading:
    """What the vision model read from the coin, used for scoring."""
    obverse_legend: str = ""
    reverse_legend: str = ""
    reverse_description: str = ""
    denomination: str = ""
    mint: str = ""


def slugify(name: str) -> str:
    text = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode().lower()
    text = re.sub(r"\(.*?\)", " ", text)
    text = re.sub(r"\b(emperor|empress|the great|the elder|the younger|roman)\b", " ", text)
    return "_".join(re.sub(r"[^a-z0-9 ]", " ", text).split())


def ruler_id_candidates(*names: str) -> list[str]:
    ids: list[str] = []
    for name in names:
        if not name:
            continue
        slug = name.strip().lower() if re.fullmatch(r"[a-z0-9_]+", name.strip().lower()) else slugify(name)
        numbered = re.search(r"_(i|ii|iii|iv|v|vi)$", slug)
        # Nomisma numbers rulers who share a name ("constantine_i"); try that form too.
        for candidate in (slug,) if numbered else (slug, f"{slug}_i"):
            if candidate and candidate.strip("_") and candidate not in ids:
                ids.append(candidate)
    return ids


def _letters(text: str) -> str:
    text = text.upper().replace("U", "V").replace("J", "I")
    return re.sub(r"[^A-Z]", "", text)


def legend_similarity(read: str, catalogue: str) -> float:
    a = _letters(read)
    if not a or not catalogue:
        return 0.0
    best = 0.0
    for variant in re.split(r"\s+or\s+", catalogue, flags=re.IGNORECASE):
        b = _letters(variant)
        if b:
            best = max(best, difflib.SequenceMatcher(None, a, b, autojunk=False).ratio())
    return best


def _words(text: str) -> set[str]:
    return {w for w in re.findall(r"[a-z]+", text.lower()) if len(w) > 2 and w not in STOPWORDS}


def description_similarity(read: str, catalogue: str) -> float:
    a, b = _words(read), _words(catalogue)
    if not a or not b:
        return 0.0
    return len(a & b) / max(3, min(len(a), len(b)))


def score_type(reading: CoinReading, record: TypeRecord) -> float:
    parts: list[tuple[float, float]] = []  # (weight, score)
    if _letters(reading.obverse_legend):
        parts.append((0.30, legend_similarity(reading.obverse_legend, record.obverse_legend)))
    if _letters(reading.reverse_legend):
        parts.append((0.35, legend_similarity(reading.reverse_legend, record.reverse_legend)))
    if _words(reading.reverse_description):
        parts.append((0.25, min(1.0, description_similarity(reading.reverse_description,
                                                            record.reverse_description))))
    denomination = slugify(reading.denomination)
    if denomination:
        accepted = DENOMINATION_ALIASES.get(denomination, {denomination})
        parts.append((0.10, 1.0 if accepted & set(record.denominations) else 0.0))
    mint = slugify(reading.mint)
    if mint and record.mints:
        mint_id = MINT_ALIASES.get(mint, mint)
        parts.append((0.10, 1.0 if mint_id in record.mints else 0.0))
    if not parts:
        return 0.0
    total_weight = sum(w for w, _ in parts)
    return sum(w * s for w, s in parts) / total_weight


def _format_year(value: str) -> str:
    try:
        year = int(value)
    except (TypeError, ValueError):
        return ""
    return f"{-year} BCE" if year < 0 else f"{year} CE"


def _value(binding: dict, key: str) -> str:
    return binding.get(key, {}).get("value", "")


class OcreCatalog:
    def __init__(self, cache_dir: Path, timeout: float, cache_days: int):
        self._cache_dir = cache_dir
        self._cache_seconds = cache_days * 86400
        self._client = httpx.AsyncClient(
            timeout=httpx.Timeout(timeout, connect=20.0),
            follow_redirects=True,
            headers={"Accept": "application/sparql-results+json",
                     "User-Agent": "AIToady-ImageIdentifier/1.0"},
        )
        self._lock = asyncio.Lock()  # the endpoint rejects bursts of parallel requests
        self._memory: dict[str, list[TypeRecord]] = {}

    async def close(self) -> None:
        await self._client.aclose()

    async def _query(self, sparql: str) -> list[dict]:
        last_error = ""
        async with self._lock:
            for attempt in range(3):
                try:
                    response = await self._client.get(SPARQL_URL, params={"query": sparql})
                    if response.status_code == 200:
                        return response.json()["results"]["bindings"]
                    last_error = f"HTTP {response.status_code}"
                    if response.status_code < 500 and response.status_code != 429:
                        break
                except (httpx.TransportError, ValueError, KeyError) as exc:
                    last_error = f"{type(exc).__name__}: {exc}"
                log.warning("Nomisma query failed (attempt %d): %s", attempt + 1, last_error)
                await asyncio.sleep(5 * (attempt + 1))
        raise CatalogError(f"Could not query the OCRE catalogue at nomisma.org ({last_error}).")

    def _cache_path(self, ruler_id: str) -> Path:
        return self._cache_dir / f"{ruler_id}.json"

    def _read_cache(self, ruler_id: str) -> Optional[list[TypeRecord]]:
        path = self._cache_path(ruler_id)
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return None
        if data.get("version") != CACHE_VERSION or time.time() - data.get("fetched", 0) > self._cache_seconds:
            return None
        return [TypeRecord(**item) for item in data.get("types", [])]

    def _write_cache(self, ruler_id: str, records: list[TypeRecord]) -> None:
        try:
            self._cache_dir.mkdir(parents=True, exist_ok=True)
            payload = {"version": CACHE_VERSION, "fetched": time.time(),
                       "types": [asdict(r) for r in records]}
            self._cache_path(ruler_id).write_text(json.dumps(payload), encoding="utf-8")
        except OSError as exc:
            log.warning("Could not write catalogue cache for %s: %s", ruler_id, exc)

    async def types_for_ruler(self, ruler_id: str) -> list[TypeRecord]:
        if ruler_id in self._memory:
            return self._memory[ruler_id]
        cached = self._read_cache(ruler_id)
        if cached is not None:
            self._memory[ruler_id] = cached
            return cached

        records: list[TypeRecord] = []
        uri = NOMISMA_ID + ruler_id
        for page in range(MAX_PAGES):
            rows = await self._query(TYPES_QUERY % {"ruler": uri, "limit": PAGE_SIZE,
                                                    "offset": page * PAGE_SIZE})
            for row in rows:
                type_uri = _value(row, "type")
                if not type_uri.startswith(OCRE_TYPE):
                    continue
                records.append(TypeRecord(
                    id=type_uri[len(OCRE_TYPE):],
                    obverse_legend=_value(row, "obv"),
                    reverse_legend=_value(row, "rev"),
                    reverse_description=_value(row, "revd"),
                    denominations=[d.rsplit("/", 1)[-1] for d in _value(row, "dens").split("|") if d],
                    mints=[m.rsplit("/", 1)[-1] for m in _value(row, "mints").split("|") if m],
                ))
            if len(rows) < PAGE_SIZE:
                break

        self._memory[ruler_id] = records
        self._write_cache(ruler_id, records)  # cache empty results too, so wrong ids aren't re-queried
        return records

    async def resolve_ruler(self, names: list[str]) -> tuple[str, list[TypeRecord]]:
        """Try each plausible Nomisma id for the ruler; return the first that has OCRE types."""
        for ruler_id in ruler_id_candidates(*names):
            records = await self.types_for_ruler(ruler_id)
            if records:
                return ruler_id, records
        return "", []

    async def _details(self, ids: list[str]) -> dict[str, dict]:
        values = " ".join(f"<{OCRE_TYPE}{i}>" for i in ids)
        rows = await self._query(DETAILS_QUERY % {"values": values})
        details: dict[str, dict] = {}
        for row in rows:
            type_id = _value(row, "type")[len(OCRE_TYPE):]
            entry = details.setdefault(type_id, {"label": "", "denominations": [], "mints": [],
                                                 "start": "", "end": "", "obverse_description": ""})
            entry["label"] = entry["label"] or _value(row, "label")
            for key, target in (("denLabel", "denominations"), ("mintLabel", "mints")):
                value = _value(row, key)
                if value and value not in entry[target]:
                    entry[target].append(value)
            entry["start"] = entry["start"] or _value(row, "start")
            entry["end"] = entry["end"] or _value(row, "end")
            entry["obverse_description"] = entry["obverse_description"] or _value(row, "obvDesc")
        return details

    async def find_candidates(self, ruler_names: list[str], reading: CoinReading,
                              limit: int) -> tuple[str, list[CatalogCandidate]]:
        ruler_id, records = await self.resolve_ruler(ruler_names)
        if not records:
            return "", []
        ranked = sorted(records, key=lambda r: score_type(reading, r), reverse=True)[:limit]
        try:
            details = await self._details([r.id for r in ranked])
        except CatalogError as exc:  # labels and dates are nice to have; the ranking still works
            log.warning("Could not fetch catalogue details: %s", exc)
            details = {}

        candidates = []
        for record in ranked:
            info = details.get(record.id, {})
            start, end = _format_year(info.get("start", "")), _format_year(info.get("end", ""))
            date_range = start if start == end else " to ".join(d for d in (start, end) if d)
            candidates.append(CatalogCandidate(
                id=record.id,
                label=info.get("label") or record.id,
                url=f"https://numismatics.org/ocre/id/{record.id}",
                denomination=", ".join(info.get("denominations") or record.denominations),
                mint=", ".join(info.get("mints") or record.mints),
                date_range=date_range,
                obverse_legend=record.obverse_legend,
                obverse_description=info.get("obverse_description", ""),
                reverse_legend=record.reverse_legend,
                reverse_description=record.reverse_description,
                score=round(score_type(reading, record), 3),
            ))
        return ruler_id, candidates
