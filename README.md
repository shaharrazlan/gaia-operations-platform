# Gaia Operations Platform

[![CI](https://github.com/shaharrazlan/gaia-operations-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/shaharrazlan/gaia-operations-platform/actions/workflows/ci.yml)

A platform that runs **operations on two inputs**, Field A and Field B. Operations are arithmetic, text, or "other", such as today's weather for a latitude/longitude. Operators can add, change or remove operations at runtime, as safe formulas, with no code change or redeploy.

- **Server:** ASP.NET Core (.NET 10, C#). All business logic, the REST API, and SQL Server through EF Core.
- **Client:** React (JavaScript) + Vite, with two views: **Calculator** and **Manage operations**. The client only displays; every result and every validation comes from the API.
- **Bonus (Part B):** every run is stored. After Calculate, the UI shows the last 3 runs of the same operation and this month's count. The server keeps an error log and a request/response log.

The marker comment **`// A34D`** is line 1 of [`src/Gaia.Api/Program.cs`](src/Gaia.Api/Program.cs).

## Contents

[Prerequisites](#prerequisites) · [Run](#run) · [Try it](#try-it) · [Tests](#tests) · [API](#api) · [Formula language](#formula-language) · [How it works](#how-it-works) · [Configuration](#configuration) · [Logs](#logs) · [Design decisions and interpretations](#design-decisions-and-interpretations) · [Security](#security) · [Known limitations](#known-limitations) · [With more time](#with-more-time) · [Troubleshooting](#troubleshooting)

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | LTS (22 or newer) | `node --version` |
| SQL Server | LocalDB (installed with Visual Studio's "Data storage and processing" workload, or with the SQL Server Express installer), or any SQL Server Express/Developer instance | `sqllocaldb info MSSQLLocalDB` |
| Git | any | `git --version` |

Visual Studio 2026 opens `Gaia.sln` (Visual Studio 2022 doesn't support .NET 10; use the `dotnet` CLI or the scripts below instead).

## Run

From the repository root, in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run.ps1
```

The first run installs the client packages. The script then starts the API on http://localhost:5080 and the React app on http://localhost:5173 in two new windows, and opens the browser. The API creates the LocalDB database `GaiaDev` on its first start. Close the two windows to stop the app.

Or run the two parts yourself, in two terminals:

```powershell
dotnet run --project src/Gaia.Api --launch-profile http
```

```powershell
npm --prefix client ci        # first time only: installs the client packages from package-lock.json
npm --prefix client run dev
```

## Try it

1. **Calculator:** enter `7`, pick **Divide**, enter `2`, press **Calculate** → `3.5`. Below the result: how many times Divide ran this month, and its 3 previous runs.
2. Try `5 ÷ 0` (a domain error), `abc` (a validation error), and **Weather today** with `32.08` / `34.78` (Tel Aviv; the labels change to Latitude/Longitude).
3. **Manage operations:** enter the management key. In development it is **`gaia-dev-key`** (set in `src/Gaia.Api/appsettings.Development.json`; see [Security](#security)). Then:
   - Add an operation, e.g. id `triple`, formula `A * 3 + B`.
   - Disable one, or rename a built-in one.
   - Back in the Calculator, the list is already updated, with no reload or restart.
4. **API documentation:** http://localhost:5080/swagger lists every endpoint and lets you try them from the browser. Press **Authorize** and enter the key for the management calls.

## Tests

| Level | What it covers | Command | Count |
|---|---|---|---|
| Unit (server) | formula engine, services, history rules, weather handler | `dotnet test tests/Gaia.Core.Tests` | 307 |
| Integration (server, needs LocalDB) | the HTTP API against a real SQL Server database (one test database per test class, dropped afterwards), logs | `dotnet test tests/Gaia.Api.Tests` | 116 |
| Component (client) | React views with a fake API | `npm --prefix client test -- --run` | 67 |
| E2E (Playwright) | the real app in Chromium: calculate, error, add a formula and use it, history | `npm --prefix client run e2e` (first time: `npx --prefix client playwright install chromium`) | 5 |
| All | every level in order, stops at the first failure | `powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1` | |

- **Test names trace to the spec.** They start with an acceptance-criterion id, e.g. `R2_AC5_…` or `it('R5.AC4: …')`; the spec and the traceability matrix are in `specs/gaia-operations-platform/`.
- **Client packages.** Before running the client levels by hand the first time, run `npm --prefix client ci`. `test-all.ps1` and `run.ps1` do this themselves when the packages are missing.
- **Leaked-key scan.** The component run includes a scan of the client build for leaked keys (R13.AC6). It runs only when a build exists, so run `npm --prefix client run build` first (`test-all.ps1` does).
- **E2E isolation.** E2E runs the API on its own port (5081) with its own database (`GaiaE2E`, recreated on every run) and a fake weather provider, so it never touches your dev data or the internet.
- **LocalDB restart.** Before the E2E tests, `test-all.ps1` restarts the LocalDB instance (see [Troubleshooting](#troubleshooting)).
- **CI.** [`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push and pull request, on Ubuntu: it builds the whole solution, runs the unit tests, then the client lint, build and component tests. The integration and E2E tests need SQL Server and run locally.

## API

Base URL `http://localhost:5080/api`. All bodies are JSON. Field A and Field B are always sent as **text**; each operation parses them according to its input type (`Number`, `Text`, `Coordinate`).

| Method and path | What it does | Key needed |
|---|---|---|
| `GET /operations` | Lists the enabled operations (id, name, kind, Field A/B labels, input type, formula, version). | no |
| `POST /operations/{id}/execute` | Runs an operation on `{ "fieldA", "fieldB" }` and returns the result plus history. | no |
| `GET /operations?includeDisabled=true` | Lists all operations, disabled ones included (the Manage view). | yes |
| `POST /operations` | Adds an operation. Returns 201. | yes |
| `PUT /operations/{id}` | Changes an operation. Send the `version` you loaded (see below). | yes |
| `DELETE /operations/{id}` | Removes an operation (its history is kept). Returns 204. | yes |

The key goes in the `X-Management-Key` header. The examples use `curl` from Git Bash (or macOS/Linux):

```bash
# List the operations
curl http://localhost:5080/api/operations

# Run one
curl -X POST http://localhost:5080/api/operations/divide/execute \
  -H "Content-Type: application/json" -d '{"fieldA":"7","fieldB":"2"}'
# → {"operationId":"divide","fieldA":"7","fieldB":"2","result":"3.5",
#    "history":{"previousRuns":[{"fieldA":"9","fieldB":"3","result":"3","createdAtUtc":"…"}],"monthCount":2}}

# Add a formula operation
curl -X POST http://localhost:5080/api/operations \
  -H "Content-Type: application/json" -H "X-Management-Key: gaia-dev-key" \
  -d '{"id":"triple","name":"Triple A plus B","kind":"Arithmetic","fieldALabel":"Number","fieldBLabel":"Add",
       "inputType":"Number","formula":"A * 3 + B"}'
# → 201 with the operation, including its "version"

# Change it: send the whole operation with the version you loaded. "isEnabled": false disables it.
curl -X PUT http://localhost:5080/api/operations/triple \
  -H "Content-Type: application/json" -H "X-Management-Key: gaia-dev-key" \
  -d '{"id":"triple","name":"Triple A plus B","kind":"Arithmetic","fieldALabel":"Number","fieldBLabel":"Add",
       "inputType":"Number","formula":"A * 3 + B","isEnabled":false,"version":"<version from the last response>"}'

# Remove it
curl -X DELETE http://localhost:5080/api/operations/triple -H "X-Management-Key: gaia-dev-key"
```

**History in the execute response:**
- `previousRuns` holds the 3 most recent *successful* runs of the same operation, newest first, not counting the run just made.
- `monthCount` counts the successful runs of the operation since the 1st of this month (Israel time), including this one.
- `history` is `null` if the history store failed; the result is still returned.

**Errors** are [RFC 9457 problem details](https://www.rfc-editor.org/rfc/rfc9457) with a machine-readable `code`, so an external app never has to parse the message:

```json
{ "status": 400, "title": "Invalid request", "detail": "Field A must be a number like 12 or -2.5.",
  "code": "validation_error", "field": "fieldA", "correlationId": "280821db909c402a9da26c5f227499c1" }
```

| Status | `code` | When |
|---|---|---|
| 400 | `validation_error` | a missing or unparsable field (`field` names it) |
| 400 | `formula_invalid` | a formula that can't be parsed, with the position |
| 400 | `system_logic_readonly`, `system_cannot_delete` | changing a built-in operation's logic, or deleting it |
| 401 | `unauthorized` | a management call without the right key |
| 404 | `operation_not_found` | an unknown, disabled or deleted operation |
| 409 | `duplicate_id` | adding an id that exists (or existed) |
| 409 | `duplicate_name` | another operation (enabled or disabled) already has that name, ignoring case |
| 409 | `edit_conflict` | saving with an old `version`: someone else changed it first; reload |
| 422 | `domain_error` | the operation can't produce a result (division by zero, √−1) |
| 502 / 504 | `external_service_error`, `external_service_timeout` | the weather service failed or took more than 5 s |
| 503 | `database_unavailable` | SQL Server can't be reached |
| 500 | `internal_error` | an unexpected bug (no internals in the body; details are in the error log) |

Every response has an `X-Correlation-Id` header. The same id is in the logs, so a reported error can be found.

## Formula language

Operators define new operations as formulas over `A` and `B`. A formula is parsed into a syntax tree and evaluated by a small interpreter written for this project. There is no `eval`, no reflection and no way to call .NET, files or the network (NFR6).

| | |
|---|---|
| **Inputs** | `A` and `B`, already parsed by the operation's input type: `Number` (`.` decimal, e.g. `-2.5`), `Text`, or `Coordinate` (a number; A must be −90…90, B −180…180). |
| **Literals** | numbers `12`, `3.5`; text `"hello"` (write `""` for a quote inside). |
| **Operators** | from highest to lowest: `( )`, unary `-`, `^` (power, right-associative), `* / %`, `+ -`, comparisons `== != < <= > >=`. `+` adds numbers or joins texts. Mixing them is an error; use `concat`. |
| **Number functions** | `sqrt(x)`, `abs(x)`, `floor(x)`, `ceil(x)`, `round(x)` or `round(x, digits)`, `min(a, b, …)`, `max(a, b, …)` |
| **Text functions** | `upper(s)`, `lower(s)`, `trim(s)`, `len(s)`, `concat(a, b, …)`, `contains(s, part)`, `count(s, part)`, `replace(s, find, with)` |
| **Condition** | `if(condition, then, else)` |

Examples: `(A + B) / 2` · `sqrt(A * A + B * B)` · `upper(A) + " " + B` · `if(A > B, A, B)` · `round(A / B * 100, 1)`.

**Rules:**
- **Checked when saved.** A formula that could never run is refused at save time (`formula_invalid`), with its position, so it never reaches the Calculator:
  - **Syntax:** an unknown name or function, a wrong number of arguments, or a syntax error, e.g. `Expected ')' at position 7`.
  - **Types:** every part's type is worked out from the input type. `A ^ B` with input type Text is refused with *"'^' at position 3 needs two numbers, but here it gets a text and a text. A and B are text because the input type is Text. Choose input type Number for calculations."* The same goes for `upper(A)` with Number, `A + "kg"`, comparing a number with a text, `(A > B) * 2`, and `if(A, …)` (the condition must be true/false). Changing only the input type of an existing formula is checked too.
  - **Parts that always fail:** dividing by a fixed 0 (`A / 0`), `sqrt(-1)`, `round(A, 20)`, `count(A, "")`.
- **Mixing numbers and text.** Use functions, not operators: `concat` turns numbers into text (`concat("Total: ", A * 2)`), and `len` and `count` turn text into numbers (`len(A) + len(B)`).
- **Allowed on purpose.** Some things are allowed because they can work for some inputs: `if` branches of different types (`if(A > B, A, "none")`), a true/false result (`A > B`), and ignoring A or B. **Kind** is only a category for the list and isn't checked against the formula.
- **Errors while running.** Errors that depend on the values typed in, such as division by zero when B is 0, `sqrt` of a negative A, or a result that isn't a finite number, are a `domain_error` at run time.
- **Number formatting.** Numbers are shown in plain decimal notation with at most 15 significant digits, so `0.1 + 0.2` gives `0.3`.
- **Size limits.** A formula is at most 500 characters and 50 levels deep. Inputs are at most 200 characters, and text results at most 1000.

**Built-in operations** (seeded): add, subtract, multiply, divide, power, concat, count (occurrences of B in A), and weather. Their name, labels and enabled state can be changed, but not their logic, and they can't be deleted. **Average** and **Hypotenuse** are seeded as ordinary custom formulas, to show what an operator can do.

## How it works

```text
src/Gaia.Core            business logic, no framework dependencies
  Formulas/                tokenizer → parser → syntax tree → evaluator, plus the function whitelist
  Services/                OperationService (execute + history), CatalogService (add/change/remove rules)
  History/                 RunRecorder (stores every run), IsraelMonth (start of month in Israel time)
  Errors/                  ValidationException, NotFoundException, DomainException, ConflictException, …
src/Gaia.Infrastructure  EF Core (SQL Server): DbContext, repositories, migrations with seed data; weather handlers
src/Gaia.Api             Program.cs (a short outline), Extensions/ (service registration, database startup),
                           controllers, error → problem-details mapping, management-key filter, logging
client/src               api/ (HTTP client), views/ (Calculator, Manage), components/ (form, key gate, history)
tests/                   Gaia.Core.Tests (unit), Gaia.Api.Tests (integration); client/e2e (Playwright)
specs/                   spec.md (requirements), plan.md (design), tasks.md (build log), verify.md (checks)
```

**Executing an operation:**
1. Load the operation (404 if it's unknown, disabled or deleted).
2. Parse A and B by its input type.
3. Run its formula, or the code handler for weather.
4. Store the run, successful or failed.
5. Return the result with the history.

**Weather** calls [Open-Meteo](https://open-meteo.com/), which needs no API key, with a 5-second timeout. It is the one operation whose logic is code. A new external integration like it is still a development task (see interpretation A2).

## Configuration

Settings live in `src/Gaia.Api/appsettings*.json`. Any setting can be overridden with an environment variable, using `__` for `:`.

| Setting | Default | Notes |
|---|---|---|
| `ConnectionStrings:Gaia` | LocalDB, database `GaiaDev` | for SQL Server Express use e.g. `Server=.\SQLEXPRESS;Database=Gaia;Trusted_Connection=True;TrustServerCertificate=True` |
| `Management:Key` | `gaia-dev-key` in Development only; **none** elsewhere | with no key, management calls are refused (closed by default) and the server logs a warning at startup |
| `Weather:Provider` | `OpenMeteo` | `Fake` returns a fixed forecast (used by E2E and for offline demos) |
| `Weather:TimeoutSeconds` | `5` | |
| `LogFiles:Directory` | `logs` (under `src/Gaia.Api`) | empty = no log files |

**Database setup.** In the Development and E2E environments the API creates and upgrades its own database at startup. In any other environment, apply the migrations yourself, passing that environment's connection string:

```powershell
dotnet tool restore
dotnet ef database update --project src/Gaia.Infrastructure --connection "<connection string>"
```

To add a migration (developers): `dotnet ef migrations add <Name> --project src/Gaia.Infrastructure --output-dir Data/Migrations`. Without `--connection`, the `dotnet ef` tool uses the local `GaiaDev` database (`DesignTimeDbContextFactory`).

## Logs

Written by Serilog to `src/Gaia.Api/logs/`, one file per day, kept for 14 days:

- **`errors-YYYYMMDD.log`** (R10) holds unexpected errors only: time, correlation id, message and stack trace. Expected business errors (validation, not found, division by zero) are not system errors and aren't logged here.
- **`requests-YYYYMMDD.log`** (R11) has one line per `/api` request: time, correlation id, method, path, headers, request body, status, response body and duration.
  - The `X-Management-Key`, `Authorization` and `Cookie` headers are written as `***`.
  - Bodies are cut at 16 KB.

## Design decisions and interpretations

Where the assignment was open to more than one reading, this is the reading used (the full list, with reasons, is in `specs/gaia-operations-platform/spec.md`):

| # | Question | Choice |
|---|---|---|
| A1, A2 | What can be added "without development"? | **Formulas** over A and B, from a fixed safe language, defined by an operator. New *kinds* of integration (another weather-like web service) still need code. Letting operators configure arbitrary HTTP calls would be a security hole. |
| A3 | Which text operations? | `concat`, and `count` (occurrences of B in A); more can be added as formulas (`upper`, `replace`, …). |
| A5 | Does "last 3 runs" include the run just made? | **No.** The current result is already on screen, so the list shows the 3 runs before it. The monthly count **does** include the current run. |
| A6 | Who may manage operations? | Anyone with the **management key** (no user accounts). Listing and executing stay open, as the assignment describes them. |
| A7 | "Start of the current month" in which time zone? | **Israel time** (Asia/Jerusalem). Runs are stored in UTC; a run at 00:30 on the 1st (Israel) counts toward the new month. |
| A8 | "Operations of the same type": same operation or same kind? | The same **operation** (e.g. Divide), not the same kind (all arithmetic). |
| A9 | Do failed runs count? | Every run is **stored**, failed ones with their error. The last-3 list and the monthly count use **successful** runs only. |
| A10 | UI language | English; the button is **Calculate**. |

**Other decisions:**
- **Concurrent edits.** Saving an operation sends the version it was loaded with; if someone saved in between, the save is refused with `edit_conflict` instead of silently overwriting (optimistic concurrency, a SQL Server `rowversion`). The Manage form keeps what you typed and offers **Reload**.
- **Delete is soft.** The row stays, marked deleted, so its history stays readable. A deleted id can't be reused, so history never mixes two different operations.
- **Names are unique.** Users pick operations by name in the Calculator, so two operations (enabled or disabled) can't share a name, ignoring case. Saving a clash gives 409 `duplicate_name`. A deleted operation's name can be used again. The database enforces it too, with a unique index, so two simultaneous saves can't both win.
- **The UI is a client like any other.** It calls the same public API an external app would, and computes nothing itself. The Vite dev server proxies `/api` to the API, so the browser needs no CORS setup.

## Security

- **Open vs. protected calls.** Listing and executing operations are open. Adding, changing, removing and listing disabled operations need the `X-Management-Key` header.
  - The server compares hashes of the key in constant time.
  - A wrong or missing key gets `401`.
  - With no key configured, management is closed.
- **The key never ships to browsers.** It is set only in server configuration. The operator types it into the Manage view, which keeps it in the tab's `sessionStorage`: closing the tab or pressing **Lock** forgets it. The client build is scanned in the tests to prove no configured key is inside it.
- **The committed key is a development key.** `gaia-dev-key` in `appsettings.Development.json` is deliberately public, for local use. It applies only in the Development environment. Anywhere else, set a long random key outside the repo, e.g. the environment variable `Management__Key`.
- **Formulas can't run code.** They are interpreted from a fixed whitelist (see [Formula language](#formula-language)), with limits on length, depth and text size.
- **No secrets in logs.** Secret headers are written as `***`, and error responses never include stack traces or internal messages.
- **Production would add:**
  - HTTPS: the dev server runs on plain http://localhost.
  - Per-application API keys or user logins with roles, instead of one shared key.
  - Rate limiting: wrong keys aren't throttled today, so a long random key is what makes guessing impractical.
  - Swagger turned off or protected (it's served in every environment).

## Known limitations

- **Number precision.** Numbers are 64-bit floating point, shown with at most 15 significant digits and 15 decimals. A result smaller than 1e-15 shows as `0`, and a huge result (over 200 digits) can't be typed back in as an input.
- **One input type per operation.** A and B always share one input type, so an operation like "repeat text A, B times" can't be defined. There is also no function that turns a text like `"5"` into the number 5; use input type Number instead.
- **Formulas saved before the save-time checks.** Operations saved before these checks existed aren't re-checked. They still fail safely in the Calculator, with the same plain message, until they're edited.
- **History across a month boundary.** The E2E history test assumes it doesn't run exactly across midnight on the 1st of a month (Israel time).
- **Shared log folder.** The dev server and the E2E server write to the same `src/Gaia.Api/logs` folder.
- **LocalDB under low memory.** LocalDB can stall when the machine is short of memory (see [Troubleshooting](#troubleshooting)).
- **CI runs part of the tests.** It runs the unit and component tests only; the integration and E2E tests need SQL Server and run locally.

## With more time

- User accounts and roles for operators, an audit log of who changed what, and per-application API keys with rate limiting.
- A formula **preview** in the Manage form, to try a formula on sample A/B before saving.
- Exact decimal arithmetic (`decimal`, or arbitrary precision) for money-like operations.
- A paged history screen with filters, beyond the last 3 runs.
- One shared test database per run instead of one per test class, for faster integration tests; integration tests in CI against a SQL Server container.
- Docker Compose (API + SQL Server + client) for a one-command start on any OS.

## Troubleshooting

**The API answers `503 database_unavailable`, or the E2E run stops with "Timed out waiting 120000ms from config.webServer".** LocalDB has stalled. This happens when the machine is short of free memory, typically right after the integration tests, which create and drop many test databases. Restart it:

```powershell
sqllocaldb stop MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

Closing other memory-heavy programs (and `dotnet build-server shutdown`) helps it stay up.

**`sqllocaldb` isn't found.** Install LocalDB (Visual Studio Installer → "Data storage and processing", or the SQL Server Express installer → LocalDB), or point `ConnectionStrings:Gaia` at another SQL Server instance (see [Configuration](#configuration)).

**Port 5080 or 5173 is in use.** Another copy is running; close its windows (or stop the process using the port).
