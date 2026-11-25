# Build Notes

## Status

None of the app has been run yet. The machine it was written on has no Python, no Ollama and no
NVIDIA GPU, so the first real test has to happen on the 4090 laptop.

## What was built

- **Language:** Python, using Pillow for image handling and FastAPI for the web server. One
  process serves both the browser UI and the JSON API.
- **Model:** Runs locally through Ollama and defaults to `qwen2.5vl:32b`, which is good at reading
  coin inscriptions. A laptop 4090 has 16 GB of video memory and this model is about 21 GB, so part
  of it runs on the CPU. That makes each image slow, possibly minutes, and the laptop needs at
  least 32 GB of RAM. Switch to a smaller model with the `VISION_MODEL` setting (for example
  `qwen2.5vl:7b`).
- **How it identifies:**
  - Every image gets a first pass that reads any text, describes the image and decides what kind
    of thing it is.
  - For anything that isn't a coin, a second pass checks that first answer against the image.
  - For coins, a separate pass reads the inscriptions and works out who is on the coin. It looks at
    the photo and also at a sharpened, high-contrast copy to make worn lettering easier to read.
- **Coin flow:** After the first side, the app asks for the reverse. If the first photo was
  actually the tails side, it asks for the heads side instead. Once it has both, a final pass
  combines them and names the person on the heads side. There is also an "I only have one side"
  option.
- **UI:** Drag and drop an image, a timer shows while the model works, and the results appear as a
  table with links. The links are searches on Google, Wikipedia, Numista (a coin catalogue) and
  eBay sold listings. The app builds these itself and never uses links written by the model.
- **API:**
  - `POST /api/identify` takes `image`, plus `reverse` to send both sides at once.
  - `POST /api/identify/{session_id}/reverse` sends the second side of a coin.
  - `POST /api/identify/{session_id}/finish` finishes a coin with one side only.
  - `GET /api/health` checks that Ollama is running and the model is installed.
  - Interactive API docs are at `/docs`, and README.md has `curl` examples.

## Running it on the laptop

```powershell
cd c:\dev\AIToady\AIToady.ImageIdentifier
.\run.ps1 -Setup   # once: installs Python 3.12 and Ollama via winget, installs packages, downloads the model
.\run.ps1          # then open http://localhost:8000/
```

## Things to know

- The app only accepts connections from the laptop itself, because the API has no login. Use
  `-ListenAll` if other machines need to reach it.
- Waiting coin sessions are kept in memory, so restarting the server clears them.
- See README.md for the full configuration settings and API examples.
