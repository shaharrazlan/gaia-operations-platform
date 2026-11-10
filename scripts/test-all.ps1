# Runs every test level in order and stops at the first failure.
# Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1
$ErrorActionPreference = 'Stop'   # a missing tool (dotnet/npm not found) stops the script with exit code 1
$root = Split-Path -Parent $PSScriptRoot

$client = Join-Path $root 'client'

# After the integration tests (which create and drop many databases), LocalDB can stall when the machine is short
# of memory, and the E2E server then never starts. Freeing the build servers' memory and restarting the
# LocalDB instance avoids it. Skipped when LocalDB isn't installed (e.g. SQL Server Express is used instead).
function Reset-ForE2E {
    dotnet build-server shutdown | Out-Null
    if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
        Write-Host 'Restarting LocalDB (MSSQLLocalDB) before the E2E tests...'
        sqllocaldb stop MSSQLLocalDB | Out-Null
        sqllocaldb start MSSQLLocalDB | Out-Null
        return   # LASTEXITCODE is the start command's: a LocalDB that won't start fails this step, not E2E later
    }
    $global:LASTEXITCODE = 0
}

$steps = @(
    @{ Name = 'Unit (server)';        Command = { dotnet test (Join-Path $root 'tests/Gaia.Core.Tests') } },
    @{ Name = 'Integration (server)'; Command = { dotnet test (Join-Path $root 'tests/Gaia.Api.Tests') } },
    # A fresh clone has no client packages yet; "ci" installs exactly what package-lock.json lists.
    @{ Name = 'Client packages';      Command = { if (Test-Path (Join-Path $client 'node_modules')) { $global:LASTEXITCODE = 0 } else { npm --prefix $client ci } } },
    # Build first: the component run also scans the build output for leaked keys (R13.AC6).
    @{ Name = 'Client build';         Command = { npm --prefix $client run build } },
    @{ Name = 'Component (client)';   Command = { npm --prefix $client test -- --run } },
    @{ Name = 'Prepare E2E';          Command = { Reset-ForE2E } },
    @{ Name = 'E2E';                  Command = { npm --prefix $client run e2e } }
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
