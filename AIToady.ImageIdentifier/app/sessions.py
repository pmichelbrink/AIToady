"""In-memory store for coins waiting on their reverse-side image."""

import secrets
import threading
import time
from dataclasses import dataclass

from .imaging import PreparedImage
from .schemas import CoinSideAnalysis, GeneralAnalysis


class SessionNotFoundError(KeyError):
    """Raised for unknown or expired session ids."""


@dataclass
class CoinSession:
    front: PreparedImage
    general: GeneralAnalysis
    front_side: CoinSideAnalysis


class SessionStore:
    def __init__(self, ttl_seconds: int):
        self._ttl = ttl_seconds
        self._items: dict[str, tuple[float, CoinSession]] = {}
        self._lock = threading.Lock()

    def _purge(self) -> None:
        cutoff = time.monotonic() - self._ttl
        for key in [k for k, (created, _) in self._items.items() if created < cutoff]:
            del self._items[key]

    def create(self, session: CoinSession) -> str:
        session_id = secrets.token_urlsafe(16)
        with self._lock:
            self._purge()
            self._items[session_id] = (time.monotonic(), session)
        return session_id

    def get(self, session_id: str) -> CoinSession:
        with self._lock:
            self._purge()
            item = self._items.get(session_id)
        if item is None:
            raise SessionNotFoundError(session_id)
        return item[1]

    def delete(self, session_id: str) -> None:
        with self._lock:
            self._items.pop(session_id, None)
