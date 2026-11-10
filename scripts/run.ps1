# Starts the Gaia API and the React dev server in two new windows, then opens the browser.
# Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts/run.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
# Paths go inside single-quoted strings below; doubling ' keeps paths like C:\Users\O'Brien working.
$quotedRoot = $root -replace "'", "''"

if (-not (Test-Path (Join-Path $root 'client/node_modules'))) {
    Write-Host 'Installing client packages (first run only)...'
    # "ci" installs exactly what package-lock.json lists. (npm 11's "install" ignores --prefix and looks for
    # package.json in the current folder.)
    npm --prefix (Join-Path $root 'client') ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
}

Write-Host 'Starting the API on http://localhost:5080 ...'
Start-Process powershell -ArgumentList '-NoExit', '-Command', "dotnet run --project '$quotedRoot/src/Gaia.Api' --launch-profile http"

Write-Host 'Starting the React app on http://localhost:5173 ...'
Start-Process powershell -ArgumentList '-NoExit', '-Command', "npm --prefix '$quotedRoot/client' run dev"

# Wait until the API answers before opening the browser (the first build can take a while).
$ready = $false
$deadline = (Get-Date).AddMinutes(3)
while (-not $ready -and (Get-Date) -lt $deadline) {
    try {
        Invoke-WebRequest -Uri 'http://localhost:5080/health' -UseBasicParsing -TimeoutSec 2 | Out-Null
        $ready = $true
    } catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Warning 'The API did not answer on http://localhost:5080/health within 3 minutes. Check the API window for errors (is LocalDB running?).'
}

Start-Process 'http://localhost:5173'
Write-Host 'Gaia is running. Close the two windows to stop it.'
