"""Pydantic models for LLM structured output and API responses.

Field order matters for the LLM schemas: the model fills fields in order, so
observation fields (text, features, reasoning) come before conclusions.
"""

from typing import Literal, Optional

from pydantic import BaseModel, Field

Confidence = Literal["low", "medium", "high"]


class GeneralAnalysis(BaseModel):
    visible_text: list[str] = Field(description="Every piece of legible text, transcribed exactly.")
    notable_features: list[str]
    description: str
    category: Literal["coin", "medal_or_token", "banknote", "other"]
    coin_side_shown: Literal["obverse", "reverse", "both", "unknown", "not_a_coin"]
    people_depicted: list[str] = Field(description="Named people shown, if identifiable.")
    identification: str = Field(description="The most specific name for what the image shows.")
    confidence: Confidence
    search_query: str


class CoinSideAnalysis(BaseModel):
    legend_text: list[str] = Field(description="Inscriptions transcribed letter by letter.")
    design_description: str
    side: Literal["obverse", "reverse", "unknown"]
    portrait_reasoning: str
    portrait_person: str = Field(description="Who is portrayed, or empty string if no person.")
    country_or_issuer: str
    denomination: str
    year: str
    mint_mark: str
    other_details: list[str]


class CoinIdentification(BaseModel):
    reasoning: str
    name: str = Field(description="Full coin name, e.g. year, country, denomination and type.")
    country_or_issuer: str
    denomination: str
    year: str
    mint_mark: str
    obverse_person: str = Field(description="Person on the obverse (heads), or empty string.")
    obverse_description: str
    reverse_description: str
    inscriptions: list[str]
    composition: str
    series_or_catalog_reference: str
    historical_notes: str
    confidence: Confidence
    uncertainties: list[str]
    search_query: str


class Link(BaseModel):
    title: str
    url: str


class IdentifyResponse(BaseModel):
    status: Literal["needs_reverse", "complete"]
    session_id: Optional[str] = None
    message: str
    summary: str
    category: str
    general: GeneralAnalysis
    front_side: Optional[CoinSideAnalysis] = None
    back_side: Optional[CoinSideAnalysis] = None
    coin: Optional[CoinIdentification] = None
    links: list[Link]
    model: str
    elapsed_seconds: float
