"""Runtime settings, read from environment variables."""

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    ollama_url: str = os.getenv("OLLAMA_URL", "http://localhost:11434")
    model: str = os.getenv("VISION_MODEL", "qwen2.5vl:32b")
    request_timeout: float = float(os.getenv("LLM_TIMEOUT_SECONDS", "900"))
    num_ctx: int = int(os.getenv("LLM_NUM_CTX", "16384"))
    max_image_side: int = int(os.getenv("MAX_IMAGE_SIDE", "1536"))
    min_image_side: int = int(os.getenv("MIN_IMAGE_SIDE", "768"))
    max_upload_mb: int = int(os.getenv("MAX_UPLOAD_MB", "25"))
    session_ttl_seconds: int = int(os.getenv("SESSION_TTL_SECONDS", "1800"))


settings = Settings()
