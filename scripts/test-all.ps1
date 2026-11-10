# Runs every test level in order and stops at the first failure.
# Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1
$ErrorActionPreference = 'Stop'   # a missing tool (dotnet/npm not found) stops the script with exit code 1
$root = Split-Path -Parent $PSScriptRoot

$steps = @(
    @{ Name = 'Unit (server)';        Command = { dotnet test (Join-Path $root 'tests/Gaia.Core.Tests') } },
    @{ Name = 'Integration (server)'; Command = { dotnet test (Join-Path $root 'tests/Gaia.Api.Tests') } },
    # Build first: the component run also scans the build output for leaked keys (R13.AC6).
    @{ Name = 'Client build';         Command = { npm --prefix (Join-Path $root 'client') run build } },
    @{ Name = 'Component (client)';   Command = { npm --prefix (Join-Path $root 'client') test -- --run } },
    @{ Name = 'E2E';                  Command = { npm --prefix (Join-Path $root 'client') run e2e } }
)

foreach ($step in $steps) {
    Write-Host "`n=== $($step.Name) ===" -ForegroundColor Cyan
    $global:LASTEXITCODE = -1   # so a step that never ran a program can't inherit the previous step's 0
    & $step.Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n$($step.Name) FAILED (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nAll test levels passed." -ForegroundColor Green
