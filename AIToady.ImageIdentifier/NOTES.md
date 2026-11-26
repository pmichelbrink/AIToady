# Build Notes

## Status

- **Not yet tested with a real model.** The machine this was written on has no Ollama or NVIDIA
  GPU, so the first real run has to happen on the 4090 laptop.
- **Checked with portable Python 3.12:**
  - **Image processing:** Ran on a synthetic coin. Detection, cropping and the rim unwrap work,
    and the curved legend comes out as straight, upright text.
  - **OCRE catalogue lookup:** Tested against the live service. For a Trajan denarius reading
    `IMP CAES NERVA TRAIAN AVG GERM / PONT MAX TR POT COS II`, RIC II Trajan 11 ranked first.
    For Constantine I `SOLI INVICTO COMITI` from Trier, the Trier types ranked first.
  - **Full API flow with a fake model:** The tests covered the two-step coin flow, both sides in
    one call, finishing with one side, an expired session (404), a bad image (400), and a
    catalogue outage.
- **nomisma.org is unreliable.** During testing it often timed out or reset connections for
  minutes at a time, and it rejects bursts of parallel requests. The app sends one request at a
  time, retries, caches each ruler's types on disk, and still answers without the catalogue
  when it is down.

## What was built

- **Language:** Python. Pillow and OpenCV handle the images, FastAPI is the web server, and one
  process serves both the browser UI and the JSON API.
- **Model:** Runs locally through Ollama and defaults to `qwen2.5vl:32b`. A laptop 4090 has 16 GB
  of video memory and this model is about 21 GB, so part of it runs on the CPU. Expect minutes
  per image and at least 32 GB of RAM. Switch with `VISION_MODEL` (e.g. `qwen2.5vl:7b`).
- **Any image:** A first pass reads text, describes the image and classifies it. A second pass
  checks that answer.
- **Coins in general:** The app asks for the other side, analyses each side, then combines them
  and names the person on the heads side. If the first photo was the tails side, it asks for the
  heads side. There is also an "I only have one side" option.
- **Roman Imperial coins:**
  - **Image views:** The coin is cropped automatically. The model sees the photo, a
    high-contrast copy for worn relief, and the rim unwrapped into straight lines of text.
  - **Side pass:** The model works through a checklist. It records the letters it can actually
    see, reconstructs, expands and translates the legend, and uses the TR P and COS numbers for
    dating. It also reads exergue mint marks and describes the design in catalogue style.
  - **Catalogue check:** The app downloads the ruler's coin types from OCRE and ranks them
    against what the model read. The model then picks the matching type, giving a RIC reference
    with a link, or says none fits.
  - **Links:** The OCRE match and candidates, OCRE search, acsearch, CoinArchives, Wildwinds,
    Google Images and Wikipedia.
- **UI:** Drag and drop an image. A timer shows while the model works. Results appear as a
  table, and for Roman coins the catalogue candidates are listed with the match highlighted.
- **API:** `POST /api/identify`, `POST /api/identify/{session_id}/reverse`,
  `POST /api/identify/{session_id}/finish`, `GET /api/health`. Interactive docs are at `/docs`,
  and README.md has `curl` examples.

## Running it on the laptop

```powershell
cd c:\dev\AIToady\AIToady.ImageIdentifier
.\run.ps1 -Setup   # once: installs Python 3.12 and Ollama via winget, installs packages, downloads the model
.\run.ps1          # then open http://localhost:8000/
```

Re-run `.\run.ps1 -Setup` after pulling these changes, because OpenCV and NumPy were added to
`requirements.txt`.

## Things to know

- The app only accepts connections from the laptop itself, because the API has no login. Use
  `-ListenAll` if other machines need to reach it.
- Waiting coin sessions are kept in memory, so restarting the server clears them.
- **The general pass decides whether a coin is Roman Imperial.** If it misjudges, the coin gets
  the generic coin path instead.
- **Late Roman types often differ only in small field marks or the workshop letter.** The model
  may then pick the right group of types but not the exact number. The candidates list shows the
  close alternatives.
- **Possible next step:** Build a reference library on the fly, saving corrections when the user
  doesn't get the answer they expected.
- See README.md for all configuration settings and API examples.
