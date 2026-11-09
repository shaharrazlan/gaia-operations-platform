# Starts the Gaia API and the React dev server in two new windows, then opens the browser.
# Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts/run.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path (Join-Path $root 'client/node_modules'))) {
    Write-Host 'Installing client packages (first run only)...'
    npm --prefix (Join-Path $root 'client') install
}

Write-Host 'Starting the API on http://localhost:5080 ...'
Start-Process powershell -ArgumentList '-NoExit', '-Command', "dotnet run --project '$root/src/Gaia.Api' --launch-profile http"

Write-Host 'Starting the React app on http://localhost:5173 ...'
Start-Process powershell -ArgumentList '-NoExit', '-Command', "npm --prefix '$root/client' run dev"

# Wait until the API answers before opening the browser (the first build can take a while).
$deadline = (Get-Date).AddMinutes(3)
while ((Get-Date) -lt $deadline) {
    try {
        Invoke-WebRequest -Uri 'http://localhost:5080/health' -UseBasicParsing -TimeoutSec 2 | Out-Null
        break
    } catch {
        Start-Sleep -Seconds 2
    }
}

Start-Process 'http://localhost:5173'
Write-Host 'Gaia is running. Close the two windows to stop it.'
