"""Minimal async client for a local Ollama server with structured (JSON schema) output."""

import json
import logging
from typing import TypeVar

import httpx
from pydantic import BaseModel, ValidationError

log = logging.getLogger(__name__)
T = TypeVar("T", bound=BaseModel)


class LLMError(RuntimeError):
    """Raised when the local model cannot be reached or returns unusable output."""


class OllamaClient:
    def __init__(self, base_url: str, model: str, timeout: float, num_ctx: int):
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.num_ctx = num_ctx
        self._client = httpx.AsyncClient(
            base_url=self.base_url, timeout=httpx.Timeout(timeout, connect=10.0)
        )

    async def close(self) -> None:
        await self._client.aclose()

    async def health(self) -> dict:
        try:
            response = await self._client.get("/api/tags")
            response.raise_for_status()
        except httpx.HTTPError as exc:
            return {"ollama_reachable": False, "model": self.model, "model_installed": False,
                    "error": f"Cannot reach Ollama at {self.base_url}: {exc}"}
        names = {m.get("name", "") for m in response.json().get("models", [])}
        wanted = self.model if ":" in self.model else f"{self.model}:latest"
        return {"ollama_reachable": True, "model": self.model, "model_installed": wanted in names}

    async def structured_chat(self, system: str, prompt: str, images: list[str],
                              schema: type[T], attempts: int = 2) -> T:
        json_schema = schema.model_json_schema()
        payload = {
            "model": self.model,
            "stream": False,
            "format": json_schema,
            "keep_alive": "30m",
            "options": {"temperature": 0, "num_ctx": self.num_ctx},
            "messages": [
                {"role": "system", "content": system},
                {
                    "role": "user",
                    "content": f"{prompt}\n\nRespond with JSON matching this schema:\n"
                               f"{json.dumps(json_schema)}",
                    "images": images,
                },
            ],
        }

        last_error = ""
        for attempt in range(1, attempts + 1):
            try:
                response = await self._client.post("/api/chat", json=payload)
            except httpx.ConnectError as exc:
                raise LLMError(f"Cannot reach Ollama at {self.base_url}. Is it running?") from exc
            except httpx.TimeoutException as exc:
                raise LLMError("The local model timed out. Increase LLM_TIMEOUT_SECONDS "
                               "or use a smaller model.") from exc
            except httpx.HTTPError as exc:
                raise LLMError(f"Error talking to Ollama: {exc}") from exc

            if response.status_code != 200:
                raise LLMError(f"Ollama returned {response.status_code}: {response.text[:500]}")

            content = response.json().get("message", {}).get("content", "")
            try:
                return schema.model_validate_json(content)
            except ValidationError as exc:
                last_error = str(exc)
                log.warning("Invalid structured output (attempt %d/%d): %s",
                            attempt, attempts, last_error)

        raise LLMError(f"The model returned output that did not match the schema: {last_error[:500]}")
