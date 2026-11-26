# Getting the Image Identifier Running on the Laptop

## 0. Before you leave the current machine

The latest changes (the Roman coin work) are not committed yet. Commit and push them so the
laptop can pull them:

```powershell
cd c:\dev\AIToady
git add AIToady.ImageIdentifier
git commit -m "Image identifier: Roman Imperial coin path with OCRE lookup"
git push
```

## 1. What the laptop needs

- Windows 10 or 11 with **winget** (built in; if `winget` isn't found, install "App Installer"
  from the Microsoft Store).
- An up-to-date **NVIDIA driver** for the RTX 4090. Check with `nvidia-smi` in PowerShell; it
  should list the GPU.
- **32 GB of RAM or more** for the default model, because part of it runs on the CPU.
- About **30 GB of free disk space**: the model is about 21 GB.
- An internet connection: for the downloads, and for the Roman coin catalogue lookup.

## 2. Get the code

If the repo isn't on the laptop yet:

```powershell
cd c:\dev
git clone https://github.com/pmichelbrink/AIToady.git
```

If it is already there:

```powershell
cd c:\dev\AIToady
git pull
```

## 3. Run the one-time setup

Open PowerShell (a normal window, not as administrator) and run:

```powershell
cd c:\dev\AIToady\AIToady.ImageIdentifier
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass   # lets this window run the script
.\run.ps1 -Setup
```

This does the following:

1. Installs Python 3.12 and Ollama with winget, if they are missing. Windows may ask you to
   approve the installers.
2. Creates a virtual environment in `.venv` and installs the Python packages.
3. Downloads the vision model `qwen2.5vl:32b` (about 21 GB, so this can take a while).

If a step fails because `py` or `ollama` "is not recognized", close PowerShell, open a new
window and run the same commands again. The new install needs a fresh window to be found.

If the model download fails with a connection error, start **Ollama** from the Start menu,
wait a few seconds, and run `.\run.ps1 -Setup` again.

## 4. Start the app

```powershell
cd c:\dev\AIToady\AIToady.ImageIdentifier
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\run.ps1
```

Then open **http://localhost:8000/** in a browser. The line under the title should say
"Using local model qwen2.5vl:32b". The API docs are at **http://localhost:8000/docs**.

Leave the PowerShell window open while you use the app. Press Ctrl+C in it to stop the app.

## 5. First test

1. Drop in a photo of the heads side of a coin and click **Identify**.
2. Expect it to take a few minutes. The first request also loads the model into memory.
3. When it asks for the other side, drop in the tails photo.

For a Roman Imperial coin, the first coin of each emperor also downloads that emperor's coin
types from the catalogue. This is cached, so later coins of the same emperor are faster.

For the best results, use photos that are sharp, evenly lit and taken straight on, with the
coin filling most of the frame.

To see how much of the model is on the GPU, run this in a second PowerShell window while a
request is running:

```powershell
ollama ps
```

## 6. If something goes wrong

| Problem | Fix |
|---|---|
| "Cannot reach Ollama" | Start Ollama from the Start menu, or run `ollama serve` in another window. |
| "Model ... is not installed" | Run `ollama pull qwen2.5vl:32b`. |
| Very slow, or out-of-memory errors | Use the smaller model (see below). |
| "The local model timed out" | Run `$env:LLM_TIMEOUT_SECONDS = "1800"` before `.\run.ps1`. |
| "Catalogue lookup failed" | nomisma.org is often down for a few minutes. Try again later; the rest of the identification still works. |
| Port 8000 already in use | Run `.\run.ps1 -Port 8080` and open http://localhost:8080/. |
| `python` opens the Microsoft Store | Ignore it; the scripts use `py` and `.venv`. |

To switch to the smaller, faster model, which fits fully on the GPU but is less accurate:

```powershell
$env:VISION_MODEL = "qwen2.5vl:7b"
.\run.ps1 -Setup     # downloads the smaller model (about 6 GB)
.\run.ps1
```

The setting lasts only for that PowerShell window. Set `$env:VISION_MODEL` again each time,
or set it permanently with `setx VISION_MODEL qwen2.5vl:7b` and open a new window.

## 7. Using the app from other computers (optional)

By default only the laptop itself can connect, because the API has no login. To allow other
machines on your network:

```powershell
.\run.ps1 -ListenAll
```

Windows Firewall will ask whether to allow Python on the network. Allow it on **private**
networks only. Other machines then use `http://<laptop-name-or-ip>:8000/`.

## 8. After pulling future changes

```powershell
cd c:\dev\AIToady
git pull
cd AIToady.ImageIdentifier
.\run.ps1 -Setup     # safe to re-run; installs any new packages
.\run.ps1
```
