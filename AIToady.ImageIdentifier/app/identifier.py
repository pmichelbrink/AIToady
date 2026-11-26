"""Multi-pass identification pipeline.

Trades speed for accuracy:
  * every image gets a general pass;
  * non-coins get a verification pass;
  * generic coins get a pass per side plus a synthesis of both sides;
  * Roman Imperial coins get a specialist pass per side (using cropped, enhanced and
    rim-unwrapped views), a lookup of matching types in the OCRE catalogue, and a final
    pass that compares the coin with those catalogue candidates.
"""

import asyncio
import json
import logging
import time
from typing import Optional

from .catalog import CatalogError, CoinReading, OcreCatalog
from .config import Settings
from .imaging import PreparedImage, prepare
from .links import build_links, build_roman_links
from .llm import OllamaClient
from .schemas import (CatalogCandidate, CoinIdentification, CoinSideAnalysis, GeneralAnalysis,
                      IdentifyResponse, RomanCoinIdentification, RomanSideAnalysis)
from .sessions import CoinSession, SessionStore

log = logging.getLogger(__name__)

TWO_SIDED = {"coin", "medal_or_token"}

SYSTEM = (
    "You are an expert visual identification assistant with deep knowledge of numismatics, "
    "history, art, nature, products and landmarks. Examine images carefully. Transcribe text "
    "exactly as it appears. Do not invent details you cannot see; when unsure, say so and "
    "lower your confidence. Use an empty string for any text field you cannot determine. "
    "Respond only with JSON matching the requested schema."
)

ROMAN_SYSTEM = (
    "You are an expert numismatist specialising in Roman Imperial coinage (27 BC to AD 491), "
    "fluent in Latin legends, imperial titulature and the conventions of RIC (Roman Imperial "
    "Coinage). You read worn, off-centre and corroded coins carefully and never claim letters "
    "you cannot see. Use an empty string for any text field you cannot determine. Respond only "
    "with JSON matching the requested schema."
)

GENERAL_PROMPT = (
    "Examine this image and identify what it shows as specifically as possible. First "
    "transcribe all visible text, then list notable visual features, then describe the image "
    "and choose a category. Use 'coin' for any coin (circulating, ancient, commemorative or "
    "bullion) and 'medal_or_token' for medals, tokens and similar struck pieces. For coins, set "
    "'coin_era': 'roman_imperial' for coins of the Roman emperors and their families (Augustus "
    "to the late 5th century, including provincial and Alexandrian issues with an emperor's "
    "portrait), 'other_ancient' for Greek, Roman Republican, Celtic and other ancient coins. "
    "State which side is shown: the obverse (heads) usually carries a portrait or the main "
    "national emblem, the reverse (tails) usually carries the denomination design. In "
    "'identification' give the most specific name the image supports. 'search_query' is a "
    "short web search query that would find this item."
)

REFINE_PROMPT = (
    "Below is a first-pass analysis of this image. Look at the image again, check every claim "
    "against what is actually visible, correct any mistakes, and make the identification more "
    "specific where the image supports it.\n\nFirst-pass analysis:\n{draft}"
)

COIN_SIDE_PROMPT = (
    "This image shows one side of a coin, medal or token. Image 1 is the photo. Image 2 is a "
    "contrast-enhanced greyscale copy of the same photo to help read worn or low-contrast "
    "inscriptions. Read every inscription around the rim and in the field letter by letter, "
    "including Latin or other foreign-language abbreviations, dates and mint marks. Describe "
    "the design. If a person is portrayed, work out who it is from the legend, portrait style, "
    "issuing country and era; explain that in 'portrait_reasoning' before naming them in "
    "'portrait_person'.\n\n{context}"
)

SYNTHESIS_PROMPT = (
    "{image_note}\n\nDetailed notes from examining each side separately:\n{notes}\n\n"
    "Combine everything into one identification of the coin. Where the notes conflict or are "
    "incomplete, look at the images again to resolve it. Name the person on the obverse "
    "(heads) side if there is one. List anything that remains uncertain."
)

ROMAN_SIDE_PROMPT = (
    "These images show one side of a Roman Imperial coin.\n"
    "{image_list}\n\n"
    "Work in this order:\n"
    "1. Legend: read the legend around the edge letter by letter, clockwise, normally starting "
    "at about 7 o'clock. Put what you actually see in 'legend_fragments', using '.' for each "
    "unreadable letter; do not guess in this field. Remember the Latin alphabet on coins: V for "
    "U, I for J, and ligatures and hyphenated breaks are common.\n"
    "2. Reconstruct the full legend in standard catalogue form in 'legend_reconstructed', "
    "using the readable letters and the known legends of the ruler. Expand the abbreviations "
    "(IMP = Imperator, CAES = Caesar, AVG = Augustus, P M / PONT MAX = Pontifex Maximus, "
    "TR P / TR POT = Tribunicia Potestate, COS = Consul, P P = Pater Patriae, P F = Pius Felix, "
    "D N = Dominus Noster, NOB C = Nobilissimus Caesar, GERM / DAC / PARTH = victory titles) "
    "and translate it. Numbers after TR P and COS date the coin; use them.\n"
    "3. Field and exergue: list letters, stars, wreaths or other marks in the field and in the "
    "exergue below the ground line. Late Roman mint marks there name the mint and workshop, "
    "e.g. SMANTA (Antioch, officina A), TR or TRP (Trier), PLG or LVG (Lyon), SIS (Siscia), "
    "ALE (Alexandria), CONS (Constantinople), R or RM (Rome).\n"
    "4. Design: describe it in catalogue style (who or what, attributes, direction). For a "
    "portrait note head or bust, laureate, radiate, diademed or bare, draped or cuirassed, and "
    "facing left or right. A radiate crown on a man, or a crescent behind an empress's "
    "shoulders, usually marks a double denomination (dupondius, antoninianus).\n"
    "5. Identity: on the obverse, decide which emperor or family member is portrayed from the "
    "legend, portrait features (beard or not, age, hairstyle) and titles; explain in "
    "'portrait_reasoning', then name them in 'portrait_person' using the standard English name "
    "(e.g. 'Trajan', 'Septimius Severus', 'Constantine I', 'Julia Domna'). Give other common "
    "names in 'ruler_name_variants'. On the reverse, name the deity, personification or scene "
    "in 'reverse_type' (e.g. Victory, Sol, Pax, Fortuna, emperor on horseback).\n"
    "6. Estimate the denomination from size, metal colour and portrait type (aureus, denarius, "
    "antoninianus, sestertius, dupondius, as, follis, AE3, solidus, siliqua), plus the mint and "
    "date range if the evidence supports them.\n\n"
    "{context}"
)

ROMAN_FINAL_PROMPT = (
    "{image_note}\n\n"
    "Notes from examining each side separately:\n{notes}\n\n"
    "{candidate_block}\n\n"
    "Make the final identification of this Roman Imperial coin. Look at the images again and "
    "check the readings against each other. {candidate_instructions} Report the ruler, the "
    "legends (catalogue form, expanded and translated), designs, denomination, metal, mint, "
    "mint mark, date range and catalogue reference. Be honest in 'confidence' and list "
    "remaining doubts in 'uncertainties'. 'search_query' should be a short query such as "
    "'Trajan denarius Abundantia'."
)

CANDIDATE_INSTRUCTIONS = (
    "The catalogue candidates come from OCRE, ranked by how closely their legends and designs "
    "match the readings; the ranking is approximate. Choose the candidate whose obverse legend, "
    "reverse legend, reverse design and denomination all match the coin, and put its id in "
    "'matched_catalog_id' and its label in 'catalog_reference'. Ignore small spelling "
    "differences caused by wear, but do not accept a candidate with a different reverse design "
    "or legend. If none fits, leave 'matched_catalog_id' empty and give your best reference."
)

NO_CANDIDATE_INSTRUCTIONS = (
    "No catalogue candidates were available, so leave 'matched_catalog_id' empty and give the "
    "most likely RIC reference only if you are reasonably sure."
)


def _other_side_message(side: str) -> str:
    if side == "reverse":
        return ("This looks like the reverse (tails) side of a coin. Please upload a photo of the "
                "obverse (heads) side for a full identification.")
    return ("This looks like a coin. Please upload a photo of the reverse (tails) side for a full "
            "identification.")


def _elapsed(started: float) -> float:
    return round(time.monotonic() - started, 1)


class Identifier:
    def __init__(self, llm: OllamaClient, sessions: SessionStore, settings: Settings,
                 catalog: Optional[OcreCatalog] = None):
        self.llm = llm
        self.sessions = sessions
        self.settings = settings
        self.catalog = catalog

    async def _prepare(self, data: bytes) -> PreparedImage:
        return await asyncio.to_thread(
            prepare, data, self.settings.min_image_side, self.settings.max_image_side
        )

    # ----- entry points -------------------------------------------------------------------

    async def identify(self, front_bytes: bytes, reverse_bytes: Optional[bytes] = None) -> IdentifyResponse:
        started = time.monotonic()
        front = await self._prepare(front_bytes)
        back = await self._prepare(reverse_bytes) if reverse_bytes else None

        general = await self.llm.structured_chat(
            SYSTEM, GENERAL_PROMPT, [front.original_b64], GeneralAnalysis
        )

        if general.category not in TWO_SIDED:
            return await self._identify_other(front, general, started)
        if general.category == "coin" and general.coin_era == "roman_imperial":
            return await self._identify_roman(front, general, back, started)
        return await self._identify_generic_coin(front, general, back, started)

    async def add_reverse(self, session_id: str, reverse_bytes: bytes) -> IdentifyResponse:
        started = time.monotonic()
        session = self.sessions.get(session_id)
        back = await self._prepare(reverse_bytes)
        if session.roman_front is not None:
            result = await self._complete_roman(session.general, session.front, session.roman_front,
                                                back, started)
        else:
            result = await self._complete_generic_coin(session.general, session.front,
                                                       session.front_side, back, started)
        self.sessions.delete(session_id)
        return result

    async def finish_without_reverse(self, session_id: str) -> IdentifyResponse:
        started = time.monotonic()
        session = self.sessions.get(session_id)
        if session.roman_front is not None:
            result = await self._finish_roman(session.general, [(session.front, session.roman_front)],
                                              started)
        else:
            coin = await self._synthesise([(session.front, session.front_side)])
            result = self._coin_response(session.general, session.front_side, None, coin, started)
        self.sessions.delete(session_id)
        return result

    # ----- non-coins ----------------------------------------------------------------------

    async def _identify_other(self, front: PreparedImage, general: GeneralAnalysis,
                              started: float) -> IdentifyResponse:
        refined = await self.llm.structured_chat(
            SYSTEM, REFINE_PROMPT.format(draft=general.model_dump_json()),
            [front.original_b64], GeneralAnalysis,
        )
        person = refined.people_depicted[0] if refined.people_depicted else ""
        return IdentifyResponse(
            status="complete", message="Identification complete.",
            summary=refined.identification, category=refined.category, general=refined,
            links=build_links(refined.search_query or refined.identification, person),
            model=self.llm.model, elapsed_seconds=_elapsed(started),
        )

    # ----- generic coins ------------------------------------------------------------------

    async def _analyse_side(self, image: PreparedImage, context: str) -> CoinSideAnalysis:
        return await self.llm.structured_chat(
            SYSTEM, COIN_SIDE_PROMPT.format(context=context),
            [image.coin_b64, image.enhanced_b64], CoinSideAnalysis,
        )

    async def _synthesise(self, sides: list[tuple[PreparedImage, CoinSideAnalysis]]) -> CoinIdentification:
        if len(sides) == 2:
            image_note = "Image 1 is the first side of the coin and image 2 is the other side."
        else:
            image_note = ("Only one side of the coin is available (image 1). Identify it as far "
                          "as that side allows.")
        notes = "\n".join(
            f"Side {i + 1}: {analysis.model_dump_json()}" for i, (_, analysis) in enumerate(sides)
        )
        return await self.llm.structured_chat(
            SYSTEM, SYNTHESIS_PROMPT.format(image_note=image_note, notes=notes),
            [image.coin_b64 for image, _ in sides], CoinIdentification,
        )

    def _coin_response(self, general: GeneralAnalysis, front_side: CoinSideAnalysis,
                       back_side: Optional[CoinSideAnalysis], coin: CoinIdentification,
                       started: float) -> IdentifyResponse:
        summary = coin.name or general.identification
        if coin.obverse_person:
            summary += f" | Obverse: {coin.obverse_person}"
        message = "Identification complete."
        if back_side is None:
            message = "Identification based on one side only; results may be less accurate."
        return IdentifyResponse(
            status="complete", message=message, summary=summary, category=general.category,
            general=general, front_side=front_side, back_side=back_side, coin=coin,
            links=build_links(coin.search_query or summary, coin.obverse_person, is_coin=True),
            model=self.llm.model, elapsed_seconds=_elapsed(started),
        )

    async def _identify_generic_coin(self, front: PreparedImage, general: GeneralAnalysis,
                                     back: Optional[PreparedImage], started: float) -> IdentifyResponse:
        front_side = await self._analyse_side(
            front, f"Earlier analysis of this photo:\n{general.model_dump_json()}"
        )
        if back is not None:
            return await self._complete_generic_coin(general, front, front_side, back, started)

        session_id = self.sessions.create(CoinSession(front, general, front_side=front_side))
        summary = general.identification
        if front_side.portrait_person:
            summary += f" | Portrait: {front_side.portrait_person}"
        return IdentifyResponse(
            status="needs_reverse", session_id=session_id,
            message=_other_side_message(front_side.side), summary=summary,
            category=general.category, general=general, front_side=front_side,
            links=build_links(general.search_query or general.identification,
                              front_side.portrait_person, is_coin=True),
            model=self.llm.model, elapsed_seconds=_elapsed(started),
        )

    async def _complete_generic_coin(self, general: GeneralAnalysis, front: PreparedImage,
                                     front_side: CoinSideAnalysis, back: PreparedImage,
                                     started: float) -> IdentifyResponse:
        back_side = await self._analyse_side(
            back, f"The other side of this coin was already examined:\n{front_side.model_dump_json()}"
        )
        sides = [(front, front_side), (back, back_side)]
        if front_side.side == "reverse" and back_side.side != "reverse":
            sides.reverse()  # put the obverse first
        coin = await self._synthesise(sides)
        return self._coin_response(general, front_side, back_side, coin, started)

    # ----- Roman Imperial coins -----------------------------------------------------------

    async def _analyse_roman_side(self, image: PreparedImage, context: str) -> RomanSideAnalysis:
        images = [image.coin_b64, image.enhanced_b64]
        image_list = ("Image 1: the photo, cropped to the coin.\n"
                      "Image 2: a contrast-enhanced greyscale copy that brings out worn relief "
                      "and faint letters.")
        if image.rim_b64:
            images.append(image.rim_b64)
            image_list += (
                "\nImage 3: the outer band of the coin unwrapped into straight strips, so the "
                "legend reads left to right. The top row covers the first 200 degrees "
                "clockwise from 3 o'clock and the bottom row continues from 9 o'clock, so the "
                "two rows overlap. Letters that face inward, often in the exergue, appear "
                "upside down here."
            )
        if not image.coin_found:
            image_list += "\n(The coin could not be located automatically, so image 1 is the full photo.)"
        return await self.llm.structured_chat(
            ROMAN_SYSTEM, ROMAN_SIDE_PROMPT.format(image_list=image_list, context=context),
            images, RomanSideAnalysis,
        )

    async def _identify_roman(self, front: PreparedImage, general: GeneralAnalysis,
                              back: Optional[PreparedImage], started: float) -> IdentifyResponse:
        roman_front = await self._analyse_roman_side(
            front, f"A general first look at this photo produced:\n{general.model_dump_json()}"
        )
        if back is not None:
            return await self._complete_roman(general, front, roman_front, back, started)

        session_id = self.sessions.create(CoinSession(front, general, roman_front=roman_front))
        summary = "Roman Imperial coin"
        if roman_front.portrait_person:
            summary += f" of {roman_front.portrait_person}"
        return IdentifyResponse(
            status="needs_reverse", session_id=session_id,
            message=_other_side_message(roman_front.side), summary=summary,
            category=general.category, general=general, roman_front=roman_front,
            links=build_roman_links(roman_front.portrait_person,
                                    " ".join(filter(None, [roman_front.portrait_person,
                                                           roman_front.denomination])),
                                    None, []),
            model=self.llm.model, elapsed_seconds=_elapsed(started),
        )

    async def _complete_roman(self, general: GeneralAnalysis, front: PreparedImage,
                              roman_front: RomanSideAnalysis, back: PreparedImage,
                              started: float) -> IdentifyResponse:
        roman_back = await self._analyse_roman_side(
            back, "The other side of this coin was already examined:\n"
                  f"{roman_front.model_dump_json()}"
        )
        sides = [(front, roman_front), (back, roman_back)]
        if roman_front.side == "reverse" and roman_back.side != "reverse":
            sides.reverse()  # put the obverse first
        return await self._finish_roman(general, sides, started)

    async def _lookup_candidates(self, sides: list[RomanSideAnalysis]) -> tuple[list[CatalogCandidate], str]:
        if self.catalog is None:
            return [], "Catalogue lookup is turned off."
        if len(sides) == 1 and sides[0].side == "reverse":
            obverse, reverse = None, sides[0]
        else:
            obverse = next((s for s in sides if s.side == "obverse"), sides[0])
            reverse = next((s for s in sides if s is not obverse), None)

        names: list[str] = []
        for side in sides:
            for name in [side.portrait_person, *side.ruler_name_variants]:
                if name and name not in names:
                    names.append(name)
        if not names:
            return [], "The ruler could not be determined, so the catalogue was not searched."

        def legend(side: Optional[RomanSideAnalysis]) -> str:
            if side is None:
                return ""
            return side.legend_reconstructed or " ".join(side.legend_fragments)

        reading = CoinReading(
            obverse_legend=legend(obverse),
            reverse_legend=legend(reverse),
            reverse_description=" ".join(filter(None, [reverse.design_description,
                                                       reverse.reverse_type])) if reverse else "",
            denomination=next((s.denomination for s in sides if s.denomination), ""),
            mint=next((s.mint for s in (reverse, obverse) if s and s.mint), ""),
        )
        try:
            ruler_id, candidates = await self.catalog.find_candidates(
                names, reading, self.settings.catalog_candidates
            )
        except CatalogError as exc:
            log.warning("Catalogue lookup failed: %s", exc)
            return [], f"Catalogue lookup failed: {exc}"
        if not candidates:
            return [], f"No OCRE coin types were found for: {', '.join(names)}."
        return candidates, f"Compared against {len(candidates)} OCRE coin types for {ruler_id}."

    async def _finish_roman(self, general: GeneralAnalysis,
                            sides: list[tuple[PreparedImage, RomanSideAnalysis]],
                            started: float) -> IdentifyResponse:
        analyses = [analysis for _, analysis in sides]
        candidates, catalog_note = await self._lookup_candidates(analyses)

        if len(sides) == 2:
            image_note = ("Image 1 is the obverse (heads) of the coin and image 2 is the reverse "
                          "(tails), both cropped to the coin.")
        else:
            image_note = ("Only one side of the coin is available (image 1). Identify it as far "
                          "as that side allows.")
        notes = "\n".join(f"Side {i + 1}: {a.model_dump_json()}" for i, a in enumerate(analyses))
        if candidates:
            compact = [c.model_dump(exclude={"url", "score"}) for c in candidates]
            candidate_block = "Catalogue candidates (OCRE):\n" + json.dumps(compact, ensure_ascii=False)
            instructions = CANDIDATE_INSTRUCTIONS
        else:
            candidate_block = "Catalogue candidates: none available."
            instructions = NO_CANDIDATE_INSTRUCTIONS

        coin = await self.llm.structured_chat(
            ROMAN_SYSTEM,
            ROMAN_FINAL_PROMPT.format(image_note=image_note, notes=notes,
                                      candidate_block=candidate_block,
                                      candidate_instructions=instructions),
            [image.coin_b64 for image, _ in sides], RomanCoinIdentification,
        )

        matched = next((c for c in candidates if c.id == coin.matched_catalog_id), None)
        if matched is None:
            coin.matched_catalog_id = ""  # never report an id that wasn't offered
        else:
            coin.catalog_reference = matched.label

        parts = [coin.ruler, coin.denomination, coin.reverse_type]
        summary = " ".join(p for p in parts if p) or general.identification
        if coin.catalog_reference:
            summary += f" ({coin.catalog_reference})"
        message = "Identification complete."
        if len(sides) == 1:
            message = "Identification based on one side only; results may be less accurate."
        if matched:
            message += " Matched to an OCRE catalogue type."
        elif candidates:
            message += " No OCRE candidate matched exactly; see the candidates for close types."

        obverse = analyses[0]
        reverse = analyses[1] if len(analyses) == 2 else None
        return IdentifyResponse(
            status="complete", message=message, summary=summary, category=general.category,
            general=general, roman_front=obverse, roman_back=reverse, roman_coin=coin,
            catalog_candidates=candidates, catalog_note=catalog_note,
            links=build_roman_links(coin.ruler, coin.search_query or summary, matched, candidates),
            model=self.llm.model, elapsed_seconds=_elapsed(started),
        )
