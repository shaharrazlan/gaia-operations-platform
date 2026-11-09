# Runs every test level in order and stops at the first failure.
# Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1
$root = Split-Path -Parent $PSScriptRoot

$steps = @(
    @{ Name = 'Unit (server)';        Command = { dotnet test (Join-Path $root 'tests/Gaia.Core.Tests') } },
    @{ Name = 'Integration (server)'; Command = { dotnet test (Join-Path $root 'tests/Gaia.Api.Tests') } },
    @{ Name = 'Component (client)';   Command = { npm --prefix (Join-Path $root 'client') test -- --run } },
    @{ Name = 'E2E';                  Command = { npm --prefix (Join-Path $root 'client') run e2e } }
)

foreach ($step in $steps) {
    Write-Host "`n=== $($step.Name) ===" -ForegroundColor Cyan
    & $step.Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n$($step.Name) FAILED" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host "`nAll test levels passed." -ForegroundColor Green
