# AIToady Image Identifier

Identifies the contents of an image with a local vision LLM running in [Ollama](https://ollama.com).
Coins get special handling: after the first side, the app asks for the other side, then combines
both sides into one identification that includes the person on the obverse (heads).

Python + FastAPI + Pillow. The browser UI and the JSON API are served by the same process.

## Setup (Windows, once)

```powershell
cd AIToady.ImageIdentifier
.\run.ps1 -Setup      # installs Python 3.12 and Ollama via winget if missing, creates .venv, pulls the model
```

After setup, start the app with `.\run.ps1`, then open http://localhost:8000/.
Use `-ListenAll` to accept connections from other machines and `-Port` to change the port.

## Choosing the model

The default is `qwen2.5vl:32b` (about 21 GB). It reads inscriptions well and gives the most
accurate results, but a laptop RTX 4090 has 16 GB of VRAM, so Ollama runs part of the model on
the CPU. That makes it slower, possibly minutes per image, and it needs 32 GB or more of system
RAM. To switch models, set `VISION_MODEL` before running `.\run.ps1 -Setup` or `.\run.ps1`:

| Model | Size | Notes |
|---|---|---|
| `qwen2.5vl:32b` | ~21 GB | Default; most accurate, partly offloaded to CPU |
| `qwen2.5vl:7b` | ~6 GB | Fits fully in VRAM, much faster, less accurate |

```powershell
$env:VISION_MODEL = "qwen2.5vl:7b"; .\run.ps1 -Setup
```

## Configuration (environment variables)

| Variable | Default | Purpose |
|---|---|---|
| `VISION_MODEL` | `qwen2.5vl:32b` | Ollama model name |
| `OLLAMA_URL` | `http://localhost:11434` | Ollama server |
| `LLM_TIMEOUT_SECONDS` | `900` | Per-call timeout |
| `LLM_NUM_CTX` | `16384` | Context window (images use many tokens) |
| `MAX_IMAGE_SIDE` / `MIN_IMAGE_SIDE` | `1536` / `768` | Images are resized into this range |
| `MAX_UPLOAD_MB` | `25` | Upload limit |
| `SESSION_TTL_SECONDS` | `1800` | How long a coin waits for its other side |

## How identification works

1. **General pass:** transcribes text, describes the image, and classifies it.
2. **Non-coins:** a verification pass re-checks the first answer against the image.
3. **Coins (and medals/tokens):** a side pass reads inscriptions from the photo plus a
   contrast-enhanced, sharpened greyscale copy, and reasons about who is portrayed. The API
   then returns `needs_reverse`. When the other side arrives, it gets its own side pass, and a
   final pass combines both sides. If the first photo was the reverse, the app asks for the obverse.

Links are search URLs (Google, Wikipedia, Numista, eBay sold listings) built from the result.
The app never passes on URLs written by the model.

## API

Interactive docs: http://localhost:8000/docs

| Method | Path | Body | Result |
|---|---|---|---|
| GET | `/api/health` | | Ollama reachable / model installed |
| POST | `/api/identify` | multipart `image`, optional `reverse` | `complete`, or `needs_reverse` + `session_id` for coins |
| POST | `/api/identify/{session_id}/reverse` | multipart `image` | `complete` |
| POST | `/api/identify/{session_id}/finish` | | `complete` using one side only |

```powershell
# Any image
curl.exe -F "image=@photo.jpg" http://localhost:8000/api/identify

# Coin, both sides in one call
curl.exe -F "image=@heads.jpg" -F "reverse=@tails.jpg" http://localhost:8000/api/identify

# Coin, two-step
curl.exe -F "image=@heads.jpg" http://localhost:8000/api/identify          # -> status needs_reverse, session_id
curl.exe -F "image=@tails.jpg" http://localhost:8000/api/identify/<session_id>/reverse
```

The key response fields are `status`, `summary`, `coin` (for coins: `name`, `obverse_person`,
`country_or_issuer`, `denomination`, `year`, ...), `general`, `front_side`, `back_side` and `links`.

Sessions are held in memory, so restarting the server clears them. The API has no
authentication, which is why it listens on localhost only by default.
