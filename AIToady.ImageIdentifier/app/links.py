"""Reference links. URLs are built from search queries, never taken from model output."""

from urllib.parse import quote_plus

from .schemas import Link


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
