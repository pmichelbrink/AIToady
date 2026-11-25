"""Multi-pass identification pipeline.

Trades speed for accuracy: every image gets a general pass, then either a
verification pass (non-coins) or per-side coin passes plus a final synthesis
that looks at both sides together.
"""

import asyncio
import time
from typing import Optional

from .config import Settings
from .imaging import PreparedImage, prepare
from .links import build_links
from .llm import OllamaClient
from .schemas import CoinIdentification, CoinSideAnalysis, GeneralAnalysis, IdentifyResponse
from .sessions import CoinSession, SessionStore

TWO_SIDED = {"coin", "medal_or_token"}

SYSTEM = (
    "You are an expert visual identification assistant with deep knowledge of numismatics, "
    "history, art, nature, products and landmarks. Examine images carefully. Transcribe text "
    "exactly as it appears. Do not invent details you cannot see; when unsure, say so and "
    "lower your confidence. Use an empty string for any text field you cannot determine. "
    "Respond only with JSON matching the requested schema."
)

GENERAL_PROMPT = (
    "Examine this image and identify what it shows as specifically as possible. First "
    "transcribe all visible text, then list notable visual features, then describe the image "
    "and choose a category. Use 'coin' for any coin (circulating, ancient, commemorative or "
    "bullion) and 'medal_or_token' for medals, tokens and similar struck pieces. For coins, "
    "state which side is shown: the obverse (heads) usually carries a portrait or the main "
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


def _other_side_message(front_side: CoinSideAnalysis) -> str:
    if front_side.side == "reverse":
        return ("This looks like the reverse (tails) side of a coin. Please upload a photo of the "
                "obverse (heads) side for a full identification.")
    return ("This looks like a coin. Please upload a photo of the reverse (tails) side for a full "
            "identification.")


class Identifier:
    def __init__(self, llm: OllamaClient, sessions: SessionStore, settings: Settings):
        self.llm = llm
        self.sessions = sessions
        self.settings = settings

    async def _prepare(self, data: bytes) -> PreparedImage:
        return await asyncio.to_thread(
            prepare, data, self.settings.min_image_side, self.settings.max_image_side
        )

    async def _analyse_side(self, image: PreparedImage, context: str) -> CoinSideAnalysis:
        return await self.llm.structured_chat(
            SYSTEM, COIN_SIDE_PROMPT.format(context=context),
            [image.standard_b64, image.enhanced_b64], CoinSideAnalysis,
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
            [image.standard_b64 for image, _ in sides], CoinIdentification,
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
            model=self.llm.model, elapsed_seconds=round(time.monotonic() - started, 1),
        )

    async def identify(self, front_bytes: bytes, reverse_bytes: Optional[bytes] = None) -> IdentifyResponse:
        started = time.monotonic()
        front = await self._prepare(front_bytes)
        back = await self._prepare(reverse_bytes) if reverse_bytes else None

        general = await self.llm.structured_chat(
            SYSTEM, GENERAL_PROMPT, [front.standard_b64], GeneralAnalysis
        )

        if general.category not in TWO_SIDED:
            refined = await self.llm.structured_chat(
                SYSTEM, REFINE_PROMPT.format(draft=general.model_dump_json()),
                [front.standard_b64], GeneralAnalysis,
            )
            person = refined.people_depicted[0] if refined.people_depicted else ""
            return IdentifyResponse(
                status="complete", message="Identification complete.",
                summary=refined.identification, category=refined.category, general=refined,
                links=build_links(refined.search_query or refined.identification, person),
                model=self.llm.model, elapsed_seconds=round(time.monotonic() - started, 1),
            )

        front_side = await self._analyse_side(
            front, f"Earlier analysis of this photo:\n{general.model_dump_json()}"
        )

        if back is None:
            session_id = self.sessions.create(CoinSession(front, general, front_side))
            summary = general.identification
            if front_side.portrait_person:
                summary += f" | Portrait: {front_side.portrait_person}"
            return IdentifyResponse(
                status="needs_reverse", session_id=session_id,
                message=_other_side_message(front_side), summary=summary,
                category=general.category, general=general, front_side=front_side,
                links=build_links(general.search_query or general.identification,
                                  front_side.portrait_person, is_coin=True),
                model=self.llm.model, elapsed_seconds=round(time.monotonic() - started, 1),
            )

        return await self._complete_coin(general, front, front_side, back, started)

    async def _complete_coin(self, general: GeneralAnalysis, front: PreparedImage,
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

    async def add_reverse(self, session_id: str, reverse_bytes: bytes) -> IdentifyResponse:
        started = time.monotonic()
        session = self.sessions.get(session_id)
        back = await self._prepare(reverse_bytes)
        result = await self._complete_coin(session.general, session.front, session.front_side,
                                           back, started)
        self.sessions.delete(session_id)
        return result

    async def finish_without_reverse(self, session_id: str) -> IdentifyResponse:
        started = time.monotonic()
        session = self.sessions.get(session_id)
        coin = await self._synthesise([(session.front, session.front_side)])
        result = self._coin_response(session.general, session.front_side, None, coin, started)
        self.sessions.delete(session_id)
        return result
