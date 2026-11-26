"""Reference links. URLs are built from search queries or catalogue ids, never taken
from model output."""

from typing import Optional
from urllib.parse import quote_plus

from .schemas import CatalogCandidate, Link


def build_links(query: str, person: str = "", is_coin: bool = False) -> list[Link]:
    query = query.strip()
    links: list[Link] = []
    if query:
        q = quote_plus(query)
        links += [
            Link(title=f"Google: {query}", url=f"https://www.google.com/search?q={q}"),
            Link(title=f"Google Images: {query}", url=f"https://www.google.com/search?tbm=isch&q={q}"),
            Link(title=f"Wikipedia search: {query}",
                 url=f"https://en.wikipedia.org/wiki/Special:Search?search={q}"),
        ]
        if is_coin:
            links += [
                Link(title=f"Numista catalogue: {query}",
                     url=f"https://en.numista.com/catalogue/index.php?r={q}"),
                Link(title=f"eBay sold listings: {query}",
                     url=f"https://www.ebay.com/sch/i.html?_nkw={q}&LH_Sold=1&LH_Complete=1"),
            ]
    person = person.strip()
    if person:
        links.append(Link(title=f"Wikipedia: {person}",
                          url=f"https://en.wikipedia.org/wiki/Special:Search?search={quote_plus(person)}"))
    return links


def build_roman_links(ruler: str, query: str, matched: Optional[CatalogCandidate],
                      candidates: list[CatalogCandidate]) -> list[Link]:
    links: list[Link] = []
    if matched:
        links.append(Link(title=f"OCRE: {matched.label} (catalogue match)", url=matched.url))
    for candidate in candidates[:5]:
        if not matched or candidate.id != matched.id:
            links.append(Link(title=f"OCRE candidate: {candidate.label}", url=candidate.url))

    search = (matched.label if matched else query).strip()
    if search:
        q = quote_plus(search)
        links += [
            Link(title=f"OCRE search: {search}", url=f"https://numismatics.org/ocre/results?q={q}"),
            Link(title=f"acsearch auction archive: {search}",
                 url=f"https://www.acsearch.info/search.html?term={q}"),
            Link(title=f"CoinArchives: {search}", url=f"https://www.coinarchives.com/a/results.php?search={q}"),
            Link(title=f"Wildwinds: {search}",
                 url=f"https://www.google.com/search?q={quote_plus('site:wildwinds.com ' + search)}"),
            Link(title=f"Google Images: {search}", url=f"https://www.google.com/search?tbm=isch&q={q}"),
        ]
    ruler = ruler.strip()
    if ruler:
        links.append(Link(title=f"Wikipedia: {ruler}",
                          url=f"https://en.wikipedia.org/wiki/Special:Search?search={quote_plus(ruler)}"))
    return links
