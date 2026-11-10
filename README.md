# Gaia Operations Platform

A platform that runs **operations on two inputs** (Field A and Field B): arithmetic, text, and "other" operations such as today's weather for a latitude/longitude. New operations can be added, changed or removed at runtime — as safe formulas — without writing code.

- **Server:** ASP.NET Core (.NET 10, C#) — all business logic, REST API, SQL Server.
- **Client:** React (JavaScript) + Vite — a calculator view and a "Manage operations" view.

> Sections marked _(coming)_ are filled in as the project is built.

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | LTS (22 or newer) | `node --version` |
| SQL Server | LocalDB (installed with Visual Studio's "Data storage" workload, or the SQL Server Express installer) — or any SQL Server Express/Developer instance | `sqllocaldb info MSSQLLocalDB` |
| Git | any | `git --version` |

Visual Studio 2026 (or the `dotnet` CLI) can open `Gaia.sln`.

## Run

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run.ps1
```

This starts the API on http://localhost:5080 and the React app on http://localhost:5173, then opens the browser.

Or run the two parts yourself, in two terminals:

```powershell
dotnet run --project src/Gaia.Api --launch-profile http
```

```powershell
npm --prefix client ci        # first time only: installs the client packages from package-lock.json
npm --prefix client run dev
```

## Tests

| Level | Command |
|---|---|
| Unit (server) | `dotnet test tests/Gaia.Core.Tests` |
| Integration (server, needs LocalDB) | `dotnet test tests/Gaia.Api.Tests` |
| Component (client) | `npm --prefix client test -- --run` |
| E2E (Playwright) | `npm --prefix client run e2e` (first time: `npx --prefix client playwright install chromium`) |
| All | `powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1` |

Before running the client levels by hand the first time, install the client packages: `npm --prefix client ci` (`test-all.ps1` and `run.ps1` do this themselves when they're missing). The component run includes a scan of the client build for leaked keys (R13.AC6); it runs only when a build exists, so run `npm --prefix client run build` first (`test-all.ps1` does). Before the E2E tests, `test-all.ps1` restarts the LocalDB instance (see Troubleshooting).

## Troubleshooting

**The API answers `503 database_unavailable`, or the E2E run stops with "Timed out waiting 120000ms from config.webServer".** LocalDB has stalled. This happens when the machine is short of free memory, typically right after the integration tests, which create and drop many test databases. Restart it:

```powershell
sqllocaldb stop MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

Closing other memory-heavy programs (and `dotnet build-server shutdown`) helps it stay up.

## Database

The API creates and updates its own database on startup (Development and E2E environments), so no database commands are needed to run it. Default: LocalDB database `GaiaDev`; change `ConnectionStrings:Gaia` to use SQL Server Express/Developer instead.

To create a new migration (developers only):

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/Gaia.Infrastructure --output-dir Data/Migrations
```

## API _(coming)_

## Formula language _(coming)_

## Design decisions and interpretations _(coming)_

## Security _(coming)_

## Known limitations _(coming)_
