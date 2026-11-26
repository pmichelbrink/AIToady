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
    coin_era: Literal["roman_imperial", "other_ancient", "medieval", "modern", "unknown",
                      "not_a_coin"]
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


class RomanSideAnalysis(BaseModel):
    legend_fragments: list[str] = Field(
        description="Legend exactly as seen, in reading order; '.' for each unreadable letter.")
    legend_reconstructed: str = Field(
        description="Full legend in standard catalogue form, e.g. IMP CAES NERVA TRAIAN AVG GERM.")
    legend_expanded: str = Field(description="Abbreviations written out in full Latin.")
    legend_translation: str
    field_and_exergue_marks: list[str] = Field(
        description="Letters or symbols in the field and the exergue (mint mark), as seen.")
    design_description: str = Field(
        description="Catalogue-style description, e.g. 'Head of Trajan, laureate, right'.")
    side: Literal["obverse", "reverse", "unknown"]
    portrait_reasoning: str
    portrait_person: str = Field(description="Emperor or family member portrayed, or empty string.")
    ruler_name_variants: list[str] = Field(
        description="Other common English names for the ruler, e.g. 'Constantine I', 'Constantine the Great'.")
    reverse_type: str = Field(description="Deity, personification or scene on the reverse, or empty.")
    denomination: str
    metal: str
    mint: str
    date_range: str
    other_details: list[str]


class CatalogCandidate(BaseModel):
    id: str
    label: str
    url: str
    denomination: str
    mint: str
    date_range: str
    obverse_legend: str
    obverse_description: str
    reverse_legend: str
    reverse_description: str
    score: float


class RomanCoinIdentification(BaseModel):
    reasoning: str
    matched_catalog_id: str = Field(
        description="id of the catalogue candidate that matches both sides, or empty string.")
    ruler: str
    ruler_reign: str
    obverse_legend: str
    obverse_legend_expanded: str
    obverse_translation: str
    obverse_description: str
    reverse_legend: str
    reverse_legend_expanded: str
    reverse_translation: str
    reverse_description: str
    reverse_type: str
    denomination: str
    metal: str
    mint: str
    mint_mark: str
    date_range: str
    catalog_reference: str = Field(description="e.g. 'RIC II Trajan 11', or empty if not known.")
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
    roman_front: Optional[RomanSideAnalysis] = None
    roman_back: Optional[RomanSideAnalysis] = None
    roman_coin: Optional[RomanCoinIdentification] = None
    catalog_candidates: list[CatalogCandidate] = []
    catalog_note: str = ""
    links: list[Link]
    model: str
    elapsed_seconds: float
