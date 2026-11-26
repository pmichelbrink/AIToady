"""FastAPI app: JSON API under /api and a browser UI at /."""

import logging
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, File, HTTPException, Request, UploadFile
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles

from .catalog import OcreCatalog
from .config import settings
from .identifier import Identifier
from .imaging import InvalidImageError
from .llm import LLMError, OllamaClient
from .schemas import IdentifyResponse
from .sessions import SessionNotFoundError, SessionStore

logging.basicConfig(level=logging.INFO)
STATIC_DIR = Path(__file__).parent / "static"


@asynccontextmanager
async def lifespan(app: FastAPI):
    llm = OllamaClient(settings.ollama_url, settings.model, settings.request_timeout, settings.num_ctx)
    catalog = (OcreCatalog(settings.catalog_cache_dir, settings.catalog_timeout,
                           settings.catalog_cache_days)
               if settings.catalog_enabled else None)
    app.state.identifier = Identifier(llm, SessionStore(settings.session_ttl_seconds), settings,
                                      catalog)
    yield
    await llm.close()
    if catalog is not None:
        await catalog.close()


app = FastAPI(
    title="AIToady Image Identifier",
    description="Identify images (with special handling for coins) using a local vision LLM.",
    version="1.0.0",
    lifespan=lifespan,
)
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


@app.exception_handler(InvalidImageError)
async def _invalid_image(_: Request, exc: InvalidImageError):
    return JSONResponse(status_code=400, content={"detail": str(exc)})


@app.exception_handler(SessionNotFoundError)
async def _missing_session(_: Request, __: SessionNotFoundError):
    return JSONResponse(status_code=404, content={"detail": "Session not found or expired. "
                                                            "Start again with the first image."})


@app.exception_handler(LLMError)
async def _llm_error(_: Request, exc: LLMError):
    return JSONResponse(status_code=502, content={"detail": str(exc)})


async def _read_upload(upload: UploadFile) -> bytes:
    limit = settings.max_upload_mb * 1024 * 1024
    data = await upload.read(limit + 1)
    if len(data) > limit:
        raise HTTPException(status_code=413, detail=f"Image exceeds {settings.max_upload_mb} MB.")
    if not data:
        raise HTTPException(status_code=400, detail="Empty file.")
    return data


def _identifier(request: Request) -> Identifier:
    return request.app.state.identifier


@app.get("/", include_in_schema=False)
async def index():
    return FileResponse(STATIC_DIR / "index.html")


@app.get("/api/health")
async def health(request: Request):
    return await _identifier(request).llm.health()


@app.post("/api/identify", response_model=IdentifyResponse)
async def identify(request: Request, image: UploadFile = File(..., description="Image to identify"),
                   reverse: Optional[UploadFile] = File(None, description="Optional coin reverse side")):
    """Identify an image. For a coin without `reverse`, returns status `needs_reverse` and a session_id."""
    front = await _read_upload(image)
    back = await _read_upload(reverse) if reverse is not None and reverse.filename else None
    return await _identifier(request).identify(front, back)


@app.post("/api/identify/{session_id}/reverse", response_model=IdentifyResponse)
async def add_reverse(request: Request, session_id: str,
                      image: UploadFile = File(..., description="Other side of the coin")):
    """Supply the other side of a coin from an earlier `needs_reverse` response."""
    return await _identifier(request).add_reverse(session_id, await _read_upload(image))


@app.post("/api/identify/{session_id}/finish", response_model=IdentifyResponse)
async def finish(request: Request, session_id: str):
    """Finish a coin identification using only the first side."""
    return await _identifier(request).finish_without_reverse(session_id)
