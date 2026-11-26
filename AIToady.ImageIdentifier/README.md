# AIToady Image Identifier

Identifies the contents of an image with a local vision LLM running in [Ollama](https://ollama.com).
Coins get special handling: after the first side, the app asks for the other side, then combines
both sides into one identification that includes the person on the obverse (heads).
**Roman Imperial coins** get a specialist path that reads the Latin legends and checks the
result against the OCRE catalogue (Online Coins of the Roman Empire).

Python + FastAPI + Pillow + OpenCV. The browser UI and the JSON API are served by the same process.

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
| `CATALOG_ENABLED` | `true` | Look up Roman Imperial coins in OCRE (needs internet) |
| `CATALOG_TIMEOUT_SECONDS` | `180` | Per-request timeout for nomisma.org |
| `CATALOG_CANDIDATES` | `12` | How many OCRE types the model compares the coin with |
| `CATALOG_CACHE_DAYS` | `30` | How long a ruler's downloaded types are reused |
| `CATALOG_CACHE_DIR` | `cache/ocre` | Where downloaded types are stored |

## How identification works

1. **General pass:** transcribes text, describes the image, and classifies it.
2. **Non-coins:** a verification pass re-checks the first answer against the image.
3. **Coins (and medals/tokens):** a side pass reads inscriptions from the photo plus a
   contrast-enhanced, sharpened greyscale copy, and reasons about who is portrayed. The API
   then returns `needs_reverse`. When the other side arrives, it gets its own side pass, and a
   final pass combines both sides. If the first photo was the reverse, the app asks for the obverse.
4. **Roman Imperial coins** take a specialist path instead of step 3:
   - **Image views:** OpenCV finds the coin and crops to it. The app also makes a CLAHE-enhanced
     greyscale copy that brings out worn relief, and a *rim strip*, where the outer band of the
     coin is unwrapped with a polar transform so the circular legend reads as straight text.
   - **Side pass:** For each side, the model gets those three views and a numismatist's checklist.
     It records the legend letters it actually sees, then reconstructs, expands and translates the
     legend. It also reads field and exergue mint marks, describes the design in catalogue style,
     and names the emperor and the reverse type.
   - **Catalogue lookup:** The app downloads every OCRE type for that ruler from the Nomisma.org
     SPARQL endpoint and ranks them locally against the readings: obverse legend, reverse legend,
     reverse design, denomination and mint. Ranking locally tolerates worn or partly misread
     legends. Each ruler's types are cached on disk, so later lookups are instant.
   - **Final pass:** The model compares both sides with the top-ranked catalogue types and picks
     the one that matches, such as "RIC II Trajan 11", or says that none fits. The app only
     accepts an id that was actually among the candidates.
   - **Offline fallback:** If nomisma.org is unreachable, which happens, the app still returns the
     model's identification and explains in `catalog_note` that the lookup failed.

Links are built from the result, never from URLs the model wrote. Roman coins get the OCRE
match and candidates, an OCRE search, acsearch and CoinArchives auction archives, Wildwinds
(through a Google site search), Google Images and Wikipedia. Other coins get Google, Wikipedia,
Numista and eBay sold listings.

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

The key response fields are `status`, `summary`, `general` and `links`. Coins also return:

- **Other coins:** `coin` (`name`, `obverse_person`, `country_or_issuer`, `denomination`,
  `year`, ...), `front_side` and `back_side`.
- **Roman Imperial coins:** `roman_coin` (`ruler`, `catalog_reference`, `matched_catalog_id`,
  legends with expansions and translations, `denomination`, `mint`, `date_range`, ...),
  `roman_front`, `roman_back`, `catalog_candidates` and `catalog_note`.

Sessions are held in memory, so restarting the server clears them. The API has no
authentication, which is why it listens on localhost only by default.
