"""Runtime settings, read from environment variables."""

import os
from dataclasses import dataclass
from pathlib import Path

_APP_ROOT = Path(__file__).resolve().parent.parent


def _flag(name: str, default: str) -> bool:
    return os.getenv(name, default).strip().lower() in ("1", "true", "yes", "on")


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
    catalog_enabled: bool = _flag("CATALOG_ENABLED", "true")
    catalog_timeout: float = float(os.getenv("CATALOG_TIMEOUT_SECONDS", "180"))
    catalog_candidates: int = int(os.getenv("CATALOG_CANDIDATES", "12"))
    catalog_cache_days: int = int(os.getenv("CATALOG_CACHE_DAYS", "30"))
    catalog_cache_dir: Path = Path(os.getenv("CATALOG_CACHE_DIR", str(_APP_ROOT / "cache" / "ocre")))


settings = Settings()
