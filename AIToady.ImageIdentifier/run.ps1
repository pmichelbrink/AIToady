<#
.SYNOPSIS
  Sets up and starts the AIToady Image Identifier.
.PARAMETER Setup
  Install Python and Ollama with winget (if missing), create the venv, install packages and pull the model.
.PARAMETER Port
  Port for the web UI / API. Default 8000.
.PARAMETER ListenAll
  Listen on all network interfaces instead of localhost only.
#>
param(
    [switch]$Setup,
    [int]$Port = 8000,
    [switch]$ListenAll
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$model = if ($env:VISION_MODEL) { $env:VISION_MODEL } else { "qwen2.5vl:32b" }

function Test-Command($name) { [bool](Get-Command $name -ErrorAction SilentlyContinue) }
function Update-SessionPath {
    $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [Environment]::GetEnvironmentVariable("Path", "User")
}

if ($Setup) {
    if (-not (Test-Command "py")) {
        winget install --id Python.Python.3.12 -e --accept-source-agreements --accept-package-agreements
        Update-SessionPath
    }
    if (-not (Test-Command "ollama")) {
        winget install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements
        Update-SessionPath
    }
    if (-not (Test-Path ".venv")) { py -3 -m venv .venv }
    & .\.venv\Scripts\python.exe -m pip install --upgrade pip
    & .\.venv\Scripts\python.exe -m pip install -r requirements.txt
    ollama pull $model
}

if (-not (Test-Path ".venv\Scripts\python.exe")) {
    throw "Virtual environment not found. Run: .\run.ps1 -Setup"
}

$bindHost = if ($ListenAll) { "0.0.0.0" } else { "127.0.0.1" }
Write-Host "UI:   http://localhost:$Port/"
Write-Host "API:  http://localhost:$Port/docs"
& .\.venv\Scripts\python.exe -m uvicorn app.main:app --host $bindHost --port $Port
