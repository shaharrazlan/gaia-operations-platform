# Gaia Operations Platform: Tasks

**Status:** Approved (2026-11-09)
**Plan:** [plan.md](plan.md) (approved 2026-11-09)

**Commands** (from plan.md):
`UNIT` = `dotnet test tests/Gaia.Core.Tests` · `INT` = `dotnet test tests/Gaia.Api.Tests` · `COMP` = `npm --prefix client test -- --run` · `E2E` = `npm --prefix client run e2e` · `ALL` = `powershell -ExecutionPolicy Bypass -File scripts/test-all.ps1`

Build order (plan): S1 → S2 → S3 → S4 → S5 → S6 → S7 → S12 → S8 → S9 → S10 → S11 → Review & verify.
**[P]** = can run in parallel with the other [P] tasks of the same step.

---

## S1 · Project setup   (Must) · ~1 h

- [x] **T1 · Install prerequisites** (approved by user 2026-11-09)
  - Covers: NFR3 · Step S1
  - Files: none (machine setup)
  - Tests: `dotnet --version` ≥ 10, `node --version` ≥ 22, `sqllocaldb info MSSQLLocalDB`, `git --version` all succeed
  - Depends on: —
  - Done when: the four commands succeed (via `winget`: .NET 10 SDK, Node.js LTS, SQL Server Express LocalDB, Git).
  - Commit: — (no repo yet)
  - Note: installed .NET SDK 10.0.403, Node v26.13.0 (current LTS), Git 2.56, SQL Server 2022 **LocalDB** (MSI fetched with Microsoft's SSEI bootstrapper; the full Express winget package failed with 1009 when run silently). LocalDB is what the plan uses.

- [x] **T2 · Server solution skeleton** [P]
  - Covers: foundation · Step S1
  - Files: `Gaia.sln`, `Directory.Build.props`, `.gitignore`, `src/Gaia.Core/`, `src/Gaia.Infrastructure/`, `src/Gaia.Api/` (empty `Program.cs` with `GET /health`)
  - Tests: `dotnet build` succeeds
  - Depends on: T1
  - Done when: `git init` done; solution builds; first commit.
  - Commit: 61050ec

- [x] **T3 · Client scaffold** [P]
  - Covers: foundation · Step S1
  - Files: `client/` (Vite React JS; demo code removed), `client/vite.config.js` (proxy `/api` → API URL, Vitest + jsdom), `client/src/main.jsx`, `client/src/smoke.test.jsx`
  - Tests: component "T3: app renders" — `COMP` green
  - Depends on: T1
  - Done when: `COMP` green; `npm --prefix client run dev` serves a blank page.
  - Commit: 9ff4526

- [x] **T4 · Server test projects**
  - Covers: foundation · Step S1
  - Files: `tests/Gaia.Core.Tests/`, `tests/Gaia.Api.Tests/` (`GaiaApiFactory` using a per-class LocalDB database `GaiaTests_<guid>`)
  - Tests: unit "T4: smoke"; integration "T4: GET /health returns 200"
  - Depends on: T2
  - Done when: `UNIT` and `INT` green.
  - Commit: 85e8fbb

- [x] **T5 · Playwright setup**
  - Covers: foundation · Step S1
  - Files: `client/playwright.config.js` (`webServer` starts API with `ASPNETCORE_ENVIRONMENT=E2E` + Vite), `client/e2e/smoke.spec.js`
  - Tests: e2e "T5: home page loads" — `E2E` green
  - Depends on: T3, T4
  - Done when: `E2E` green (Chromium installed via `npx playwright install chromium`).
  - Commit: cfb801c

- [x] **T6 · Dev scripts**
  - Covers: NFR3 · Step S1
  - Files: `scripts/run.ps1` (starts API + Vite, opens browser), `scripts/test-all.ps1` (runs UNIT, INT, COMP, E2E; non-zero exit on failure)
  - Tests: `ALL` green
  - Depends on: T5
  - Done when: `ALL` green; `run.ps1` opens the app.
  - Commit: be00d70

- [x] **T7 · README skeleton**
  - Covers: NFR3 · Step S1
  - Files: `README.md` (prerequisites, run, test commands — sections filled later in T56)
  - Tests: `ALL` still green
  - Depends on: T6
  - Done when: README lists prerequisites and the commands above.
  - Commit: ff12601

## S2 · Formula engine + error types   (Must) · ~2.5 h

- [x] **T8 · Error types**
  - Covers: R2.AC6 (codes) · Step S2
  - Files: `src/Gaia.Core/Errors/*.cs` (`GaiaException` base with `Code`, `Field`; `ValidationException`, `NotFoundException`, `DomainException`, `ExternalServiceException`, `ConflictException`)
  - Tests: unit "T8: each exception type carries its error code"
  - Depends on: T4
  - Done when: `UNIT` green.
  - Commit: 38aa0df

- [x] **T9 · Tokenizer** [P]
  - Covers: R4.AC4 (positions) · Step S2
  - Files: `src/Gaia.Core/Formulas/Token.cs`, `Tokenizer.cs`, `tests/Gaia.Core.Tests/Formulas/TokenizerTests.cs`
  - Tests: unit "T9: tokenizes numbers, strings, A/B, operators, parentheses, commas with positions"; unit "R4.AC4: unexpected character '#' reports its position"; unit "R4.AC4: unterminated string reports its position"
  - Depends on: T8
  - Done when: `UNIT` green.
  - Commit: e2c0d56

- [x] **T10 · Input parser** [P]
  - Covers: R2.AC4, R3.AC5 · Step S2
  - Files: `src/Gaia.Core/Operations/InputType.cs`, `InputParser.cs`, `tests/…/InputParserTests.cs`
  - Tests: unit "R3.AC5: parses '-2.5' and '10' as numbers"; unit "R3.AC5: rejects '3,5', '1e3' and 'abc'"; unit "R2.AC4: rejects latitude 95 and longitude -181 with field name"; unit "R2.AC4: Text input accepts any string"
  - Depends on: T8
  - Done when: `UNIT` green.
  - Commit: a154dd8
  - Note: also created `Formulas/Value.cs` (planned for T12) because the parser returns typed values. Review fix: `[0-9]` instead of `\d` (Unicode digits).

- [x] **T11 · Parser (AST)**
  - Covers: R4.AC4, NFR6 · Step S2
  - Files: `src/Gaia.Core/Formulas/Ast.cs`, `FormulaParser.cs`, `tests/…/FormulaParserTests.cs`
  - Tests: unit "T11: precedence — 1 + 2 * 3 ^ 2 parses as 1 + (2 * (3 ^ 2))"; unit "T11: ^ is right-associative"; unit "T11: unary minus"; unit "R4.AC4: missing ')' reports position"; unit "R4.AC4: unknown identifier 'C' is an error"; unit "NFR6: 'File.Delete(\"x\")' is a parse error"; unit "NFR6: nesting deeper than 50 is rejected"; unit "NFR6: formula over 500 chars is rejected"
  - Depends on: T9
  - Done when: `UNIT` green.
  - Commit: 722b6ba
  - Note: review fix — numeric literals too large for a double are rejected at save time.

- [x] **T12 · Evaluator (operators)**
  - Covers: R2.AC5 · Step S2
  - Files: `src/Gaia.Core/Formulas/Value.cs`, `Evaluator.cs`, `tests/…/EvaluatorTests.cs`
  - Tests: unit "T12: arithmetic and comparison operators"; unit "T12: + concatenates two texts"; unit "R2.AC5: division by zero is a domain error"; unit "R2.AC5: modulo by zero is a domain error"; unit "R2.AC5: NaN or infinite result is a domain error"; unit "R2.AC5: number + text is a type (domain) error"
  - Depends on: T11
  - Done when: `UNIT` green.
  - Commit: c869ce6

- [x] **T13 · Function whitelist**
  - Covers: R3.AC2, NFR6 · Step S2
  - Files: `src/Gaia.Core/Formulas/Functions.cs`, `tests/…/FunctionsTests.cs`
  - Tests: unit "R3.AC2: concat('foo','bar') = 'foobar'"; unit "R3.AC2: count('banana','an') = 2"; unit "T13: sqrt abs round min max floor ceil upper lower len trim replace contains if"; unit "NFR6: unknown function 'exec' is rejected at parse time"; unit "T13: wrong arity is rejected at parse time"; unit "R2.AC5: sqrt(-1) is a domain error"
  - Depends on: T12
  - Done when: `UNIT` green.
  - Note: also created `Formulas/ResultFormatter.cs` (planned for T14) because `concat` formats numbers; modified `FormulaParser` (name/arity check) and `Evaluator` (calls, lazy `if`). Review fix: every produced text is capped at 1000 chars (= stored result size) so nested `replace` can't exhaust memory.

- [x] **T14 · FormulaEngine facade**
  - Covers: R3.AC1 · Step S2
  - Files: `src/Gaia.Core/Formulas/FormulaEngine.cs`, `ResultFormatter.cs`, `tests/…/FormulaEngineTests.cs`
  - Tests: unit "R3.AC1: with A=7, B=2 the seeded formulas give add 9, subtract 5, multiply 14, divide 3.5, power 49"; unit "T14: 0.1 + 0.2 formats as 0.3"; unit "T14: Validate returns error with position for a bad formula"
  - Depends on: T10, T13
  - Done when: `UNIT` green.
  - Commit: 37b0c9c

## S3 · Data layer   (Must) · ~0.75 h

- [x] **T15 · DbContext and Operation entity**
  - Covers: foundation for R1/R4 · Step S3
  - Files: `src/Gaia.Core/Operations/OperationDefinition.cs`, `src/Gaia.Infrastructure/Data/GaiaDbContext.cs`, `Data/OperationEntityConfig.cs` (incl. `Version` rowversion, `IsSystem`)
  - Tests: `dotnet build` + `UNIT` green
  - Depends on: T14
  - Done when: builds.
  - Commit: 5c52f35
  - Note: pinned `System.Security.Cryptography.Pkcs` 10.0.14 (transitive 9.0.11 from SqlClient has GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx); added `SeedOperations.cs` and `DesignTimeDbContextFactory.cs` (Infrastructure/Data, S3-owned). S3 review: UTC converter, case-sensitive id collation, enum check constraints, seed as factory method; Initial migration regenerated (not yet shipped).

- [x] **T16 · Initial migration with seed**
  - Covers: R3.AC1–3 data · Step S3
  - Files: `src/Gaia.Infrastructure/Data/Migrations/*Initial*`
  - Tests: integration "T16: migrated database contains the 10 seeded operations with correct IsSystem flags"
  - Also: `GaiaApiFactory` migrates its database on start and **drops it on dispose** (S1 review finding)
  - Depends on: T15
  - Done when: `INT` green.
  - Commit: 1efbf19

- [x] **T17 · Operation repository**
  - Covers: R1.AC2 (data) · Step S3
  - Files: `src/Gaia.Core/Operations/IOperationRepository.cs`, `src/Gaia.Infrastructure/Data/EfOperationRepository.cs`
  - Tests: integration "T17: GetEnabled excludes disabled and deleted"; integration "T17: GetById returns null for unknown id"
  - Depends on: T16
  - Done when: `INT` green.
  - Commit: 8b5de35

## S4 · Catalog + execute API, weather   (Must) · ~2 h

- [x] **T18 · OperationService (formula path)**
  - Covers: R2.AC1, R2.AC2, R2.AC3 · Step S4
  - Files: `src/Gaia.Core/Services/OperationService.cs`, `IOperationHandler.cs`, `IRunRecorder.cs` (+ `NoOpRunRecorder`), `tests/…/OperationServiceTests.cs`
  - Tests: unit "R2.AC1: executes a formula operation and returns its result"; unit "R2.AC2: unknown or disabled id throws NotFound"; unit "R2.AC3: missing field A throws Validation with field 'fieldA'"
  - Depends on: T17
  - Done when: `UNIT` green.
  - Commit: 40de0e5
  - Note: `IOperationHandler` lives in `Core/Operations`; `IRunRecorder`/`NoOpRunRecorder` deferred to S8 (would be unused code until then).

- [x] **T19 · Catalog endpoint**
  - Covers: R1.AC1, R1.AC2, R1.AC3 · Step S4
  - Files: `src/Gaia.Api/Controllers/OperationsController.cs` (GET), `src/Gaia.Api/Contracts/OperationDto.cs`, `Program.cs` (DI, EF, auto-migrate in Development/E2E)
  - Tests: integration "R1.AC1: GET /api/operations returns seeded ops with id, name, kind, labels"; integration "R1.AC2: disabled and deleted ops are absent"; integration "R1.AC3: all disabled returns [] with 200"
  - Depends on: T18
  - Done when: `INT` green.
  - Commit: 75bc25a
  - Note: added `appsettings.json` connection string (LocalDB `GaiaDev`) and `appsettings.E2E.json` (`GaiaE2E`, recreated each E2E run).

- [x] **T20 · Execute endpoint and error mapping**
  - Covers: R2.AC1–6, R6.AC2 · Step S4
  - Files: `OperationsController.cs` (execute), `src/Gaia.Api/Contracts/ExecuteRequest.cs`, `ExecuteResponse.cs`, `src/Gaia.Api/Errors/ProblemDetailsMapper.cs`
  - Tests: integration "R2.AC1: POST execute divide 7,2 returns 3.5"; integration "R2.AC2: unknown id → 404 operation_not_found naming the id"; integration "R2.AC3: missing fieldA → 400 with field"; integration "R2.AC4: 'abc' for a Number op → 400 validation_error"; integration "R2.AC5: divide by zero → 422 domain_error"; integration "R2.AC6: each error kind has a distinct code" (table-driven); integration "R6.AC2: HTTP result equals OperationService result for the same inputs"; integration "T20: database down → 503 database_unavailable"
  - Depends on: T19
  - Done when: `INT` green.
  - Commit: c04100d

- [x] **T21 · Open-Meteo weather handler** [P]
  - Covers: R3.AC3, R3.AC4, NFR2 · Step S4
  - Files: `src/Gaia.Infrastructure/Weather/OpenMeteoWeatherHandler.cs`, `WeatherCodes.cs`, `FakeWeatherHandler.cs`, `tests/…/OpenMeteoWeatherHandlerTests.cs` (fake `HttpMessageHandler`)
  - Tests: unit "R3.AC3: maps Open-Meteo JSON to 'Partly cloudy, 12–21 °C'"; unit "R3.AC4: HTTP 500 → ExternalServiceException"; unit "R3.AC4: malformed JSON → ExternalServiceException"; unit "NFR2: no answer within 5 s → external_service_timeout"
  - Depends on: T18
  - Done when: `UNIT` green.
  - Commit: dde3461

- [x] **T22 · Weather wired into the API**
  - Covers: R3.AC3, R3.AC4 · Step S4
  - Files: `OperationService.cs` (handler lookup), `Program.cs` (`Weather:Provider` config, typed HttpClient, startup check that every system op without a formula has a handler)
  - Tests: integration "R3.AC3: execute weather 32.08,34.78 with stubbed HTTP returns forecast line"; integration "R3.AC4: stubbed provider failure → 502 external_service_error"; unit "T22: startup check fails fast when a handler is missing"
  - Depends on: T20, T21
  - Done when: `UNIT` and `INT` green.
  - Commit: 035c10b

- [x] **T23 · Swagger UI**
  - Covers: (enables R4.AC5) · Step S4
  - Files: `Program.cs` (Swashbuckle), `Gaia.Api.csproj`
  - Tests: integration "T23: /swagger/v1/swagger.json lists GET and execute"
  - Depends on: T22
  - Done when: `INT` green; `/swagger` loads in a browser.
  - Commit: bcf1d57

- [x] **T24 · A34D marker**
  - Covers: NFR5, C4 · Step S4
  - Files: `src/Gaia.Api/Program.cs` (`// A34D` comment), `tests/Gaia.Core.Tests/MarkerTests.cs`
  - Tests: unit "NFR5: a '// A34D' comment exists under src/Gaia.Api"
  - Depends on: T23
  - Done when: `UNIT` green.
  - Commit: 1e4d6a1
  - S4 review (applies to T18–T24): JSON numbers accepted for fields; framework 404/405 get codes; client cancellation → 499 (not 500); handler check runs in every environment, ignores deleted ops and catches duplicate handlers; E2E only deletes a database named GaiaE2E*; startup moved to `DatabaseStartup.cs`. Integration tests now run one class at a time (parallel CREATE DATABASE timed out LocalDB ~1 run in 4).

## S5 · React calculator   (Must) · ~1.5 h

- [x] **T25 · API client layer**
  - Covers: R2.AC6 (client side), R5.AC5 · Step S5
  - Files: `client/src/api/errors.js`, `httpApi.js`, `fakeApi.js`, `ApiContext.jsx`, `client/src/api/httpApi.test.js`
  - Tests: component "T25: non-2xx problem response becomes ApiError with code, detail, field"; component "R5.AC5: network failure becomes ServiceUnavailableError"
  - Depends on: T3 (contract from plan; real API not needed)
  - Done when: `COMP` green.
  - Commit: a58faeb

- [x] **T26 · Calculator form**
  - Covers: R5.AC1, R5.AC2 · Step S5
  - Files: `client/src/App.jsx`, `client/src/views/CalculatorView.jsx`, `client/src/styles.css`, `client/src/views/CalculatorView.test.jsx`
  - Tests: component "R5.AC1: select lists operations from the API"; component "R5.AC2: field labels follow the selected operation"
  - Depends on: T25
  - Done when: `COMP` green.
  - Commit: 6740159

- [x] **T27 · Calculate result and errors**
  - Covers: R5.AC3, R5.AC4, R5.AC5, R6.AC1 · Step S5
  - Files: `CalculatorView.jsx`, `CalculatorView.test.jsx`
  - Tests: component "R5.AC3: Calculate shows the result"; component "R5.AC4: server error detail is shown and no result"; component "R5.AC5: unreachable server shows 'Service unavailable'"; component "R6.AC1: shown result is exactly the API value"
  - Depends on: T26
  - Done when: `COMP` green; manual check against the running API.
  - Commit: 4df092a
  - Real-app check (built-in browser, real API): 7÷2 = 3.5, 5÷0 → 'Division by zero.', live Open-Meteo weather for Tel Aviv, API stopped → 'Service unavailable'. Found and fixed: result box rendered as an inline sliver (dc9299c).
  - S5 review: 15 s client timeout, 'Try again' after a failed catalog load, non-JSON 2xx → Service unavailable, invalid mark clears on edit, no Content-Type on GET; `fakeApi.js` moved to `src/test/` (test-only, imports vitest).

## S6 · Management API   (Should) · ~1.75 h

- [x] **T28 · CatalogService create rules**
  - Covers: R4.AC4 · Step S6
  - Files: `src/Gaia.Core/Services/CatalogService.cs`, `tests/…/CatalogServiceTests.cs`
  - Tests: unit "R4.AC4: invalid slug, missing name, or bad formula is rejected"; unit "R4.AC4: duplicate id (incl. soft-deleted) → Conflict duplicate_id"; unit "T28: created operations are always custom"
  - Depends on: T24
  - Done when: `UNIT` green.
  - Commit: d669254
  - Extra files (needed to compile the new repository contract): `IOperationRepository.cs` (AddAsync, UpdateAsync), `EfOperationRepository.cs` (both implemented now; duplicate-key race → 409, rowversion check → 409), `GaiaException.cs` (ConflictException takes a field), `OperationServiceTests.cs` (fake repo). `TimeProvider` injected for deterministic timestamps.

- [x] **T29 · CatalogService update and delete rules**
  - Covers: R4.AC6 · Step S6
  - Files: `CatalogService.cs`, `CatalogServiceTests.cs`
  - Tests: unit "R4.AC6: system op — rename and disable allowed"; unit "R4.AC6: system op — formula/kind/input type change → system_logic_readonly"; unit "R4.AC6: deleting a system op → system_cannot_delete"
  - Depends on: T28
  - Done when: `UNIT` green.
  - Commit: 458c206
  - Also: unit tests for R4.AC2/AC3/AC7 rules (version required, stale version → edit_conflict, unknown/deleted → 404, body id must match), and `ListAllAsync` for the management list. Labels of system ops may change too (presentation, not logic).

- [x] **T30 · POST /operations**
  - Covers: R4.AC1, R4.AC4 · Step S6
  - Files: `OperationsController.cs` (POST), `src/Gaia.Api/Contracts/OperationUpsertDto.cs`
  - Tests: integration "R4.AC1: created op appears in GET and executes, no restart"; integration "R4.AC4: bad formula → 400 formula_invalid, catalog unchanged"; integration "R4.AC4: duplicate id → 409 duplicate_id"
  - Depends on: T29
  - Done when: `INT` green.
  - Commit: b984034 (that commit also carried a mis-encoded tasks.md; repaired in the T31 commit)
  - Also: CatalogService + TimeProvider registered in Program.cs; generic model-binding message (no longer mentions fieldA, since it now serves management bodies too).

- [x] **T31 · PUT /operations/{id} with concurrency**
  - Covers: R4.AC2, R4.AC6, R4.AC7 · Step S6
  - Files: `OperationsController.cs` (PUT), `EfOperationRepository.cs` (update with original `Version`)
  - Tests: integration "R4.AC2: changed formula is used by the next execute"; integration "R4.AC6: PUT formula on divide or weather → 400 system_logic_readonly; rename succeeds"; integration "R4.AC7: two PUTs with the same version → 200 then 409 edit_conflict, first values kept"; integration "R4.AC7: PUT with fresh version → 200"
  - Depends on: T30
  - Done when: `INT` green.
  - Commit: cfce4a5
  - Note: the EF update with the original `Version` was already written in T28 (needed to compile); this task adds the endpoint and proves it against real SQL Server rowversion.

- [x] **T32 · DELETE and management listing**
  - Covers: R4.AC3, R4.AC5 · Step S6
  - Files: `OperationsController.cs` (DELETE, `includeDisabled`)
  - Tests: integration "R4.AC3: DELETE hides op, execute → 404, its runs remain"; integration "R4.AC5: swagger.json lists POST, PUT and DELETE"; integration "T32: includeDisabled=true returns disabled ops"
  - Depends on: T31
  - Done when: `INT` green.
  - Commit: 98922e9
  - Note: "its runs remain" is proven as "the row is kept (soft delete)"; history rows arrive in S8, whose T41+ tests re-check it. `includeDisabled` is open until S12 adds the key.
  - S6 review (high): PUT without `isEnabled` no longer re-enables a disabled op (left out = unchanged); a DB failure while saving (`DbUpdateException` → `DbException`) is 503, not 500; bad query values (`?includeDisabled=yes`) name the parameter instead of blaming the body; DELETE documents its possible 409. Kept on purpose: `OperationUpsertDto` mirrors `OperationInput` (Swagger docs stay in the API layer, Core stays HTTP-free). Open management calls → S12. `/simplify` angles were covered inside the high review; `/security-review` runs with S12 (the auth step) and in T54.

## S7 · Manage operations view   (Should) · ~1.75 h

- [x] **T33 · View switch**
  - Covers: R12.AC1, R12.AC7 · Step S7
  - Files: `client/src/App.jsx`, `client/src/App.test.jsx`, `client/src/views/ManageView.jsx` (placeholder)
  - Tests: component "R12.AC1: top bar switches Calculator ↔ Manage operations"; component "R12.AC7: returning to Calculator re-fetches operations"
  - Depends on: T27
  - Done when: `COMP` green.
  - Commit: d11998d
  - Also: `styles.css` (top-bar styling). Only the visible view is mounted, so R12.AC7 needs no extra code.

- [x] **T34 · Operations table**
  - Covers: R12.AC2, R12.AC5 · Step S7
  - Files: `ManageView.jsx`, `ManageView.test.jsx`, `client/src/api/httpApi.js` + `fakeApi.js` (management calls)
  - Tests: component "R12.AC2: lists enabled and disabled ops, 'built-in' for system code ops"; component "R12.AC5: disable/enable toggle calls API and updates the row"
  - Depends on: T33
  - Done when: `COMP` green.
  - Commit: 021b18d
  - Also: Delete button with a confirmation for custom ops (R4.AC3 through the UI — no task had it, the plan's screen lists it); a rejected toggle/delete shows the server message and reloads the list; `src/api/definitions.js` (`toDefinition`, shared with the form); `httpApi.test.js` covers the new calls; table styles.

- [x] **T35 · Add-operation form**
  - Covers: R12.AC3, R12.AC6 · Step S7
  - Files: `client/src/components/OperationForm.jsx`, `OperationForm.test.jsx`, `ManageView.jsx` (Add button)
  - Tests: component "R12.AC3: saving a new op adds it to the list"; component "R12.AC6: formula_invalid message shown next to the form and inputs kept"
  - Depends on: T34
  - Done when: `COMP` green.
  - Commit: 323652d
  - Review: the Add button only appears once the list has loaded (saving into a not-yet-loaded list crashed). Also `styles.css` (form layout).

- [x] **T36 · Edit form with conflict handling**
  - Covers: R12.AC4, R12.AC8 · Step S7
  - Files: `OperationForm.jsx`, `OperationForm.test.jsx`, `ManageView.jsx` (Edit button)
  - Tests: component "R12.AC4: system op — formula, kind and input type are read-only"; component "R12.AC8: 409 edit_conflict shows the conflict message, keeps inputs, Reload loads the latest"
  - Depends on: T35
  - Done when: `COMP` green.
  - Commit: 39c301d
  - Also: edit form reuses `OperationForm` (Id locked when editing; code ops keep formula null); after a conflict, Reload replaces the inputs with the latest saved version; `styles.css` (hint).
  - S7 review (high): the row open in the form can't be toggled or deleted meanwhile (would make the form's version stale); a successful save clears an old error notice; opening the form moves keyboard focus into it. Kept: `change()` and the form's `run()` share a small busy/try/catch shape but feed different error displays. `/simplify` angles covered inside the review; no auth/secrets in this step (S12 next).
  - Real-app check (built-in browser, real API): table lists all 10 seed ops; add with a broken formula → server message + Formula marked + inputs kept; fixed formula → saved; Calculator shows "Midpoint" with labels From/To without reload; 4, 10 → 7. Test op then deleted (soft delete; id `midpoint` stays taken in the dev DB).

## S12 · Management key   (Should) · ~1 h

- [x] **T37 · Management key filter**
  - Covers: R13.AC1, R13.AC2, R13.AC3, R13.AC8 · Step S12
  - Files: `src/Gaia.Api/Security/RequireManagementKeyAttribute.cs`, `ManagementKeyOptions.cs`, `appsettings.Development.json` (`Management:Key`), `OperationsController.cs` (attributes), `tests/Gaia.Api.Tests/**` (R4 tests send the key)
  - Tests: integration "R13.AC1: POST/PUT/DELETE without key or with wrong key → 401 unauthorized, catalog unchanged"; integration "R13.AC2: with the key, management works as R4"; integration "R13.AC3: GET and execute need no key"; integration "R13.AC8: no key configured → 401 even with a header, startup warning logged"
  - Depends on: T36
  - Done when: `INT` green.
  - Commit: 96e6ffe
  - Also: `appsettings.E2E.json` gets its own test key (E2E unlocks the Manage view); `Program.cs` binds the options and logs the startup warning; `GaiaApiFactory` configures a test key and offers `CreateManagementClient()`. The full list uses `[RequireManagementKey(OnlyWhenQueryIsTrue = "includeDisabled")]`. Keys are compared as SHA-256 hashes with `FixedTimeEquals` (no timing or length leak).
  - Review: `?includeDisabled=true&includeDisabled=true` slipped past the check (the joined "true,true" doesn't parse) while binding still read `true` → now every value is checked; test added.

- [x] **T38 · Swagger Authorize button**
  - Covers: (keeps R4.AC5 usable) · Step S12
  - Files: `Program.cs` (security definition for `X-Management-Key`)
  - Tests: integration "T38: swagger.json declares the X-Management-Key scheme"
  - Depends on: T37
  - Done when: `INT` green.
  - Commit: 43387fa
  - Also: `Security/ManagementKeyOperationFilter.cs` (Swagger requirement + 401 response only on actions with the attribute). Red confirmed by running the test without the change.

- [x] **T39 · KeyGate in the UI**
  - Covers: R13.AC4, R13.AC5 · Step S12
  - Files: `client/src/components/KeyGate.jsx`, `KeyGate.test.jsx`, `ManageView.jsx` (wrapped), `httpApi.js` (sends header)
  - Tests: component "R13.AC4: wrong key shows 'Invalid management key' and stays locked"; component "R13.AC4: right key unlocks the view"; component "R13.AC5: key kept in sessionStorage; Lock clears it"
  - Depends on: T38
  - Done when: `COMP` green.
  - Commit: 9da85cb
  - Also: `httpApi.withManagementKey(key)` returns the same API with the header on every call (the plain API never sends it); `fakeApi` gets `withManagementKey`; `App.jsx` wraps the Manage view; `styles.css`. The key is checked by the server (full-list call), never compared in the browser.
  - Review: a remembered key the server later rejects (401) now locks the gate and asks again, instead of leaving an "unlocked" view whose calls all fail.

- [x] **T40 · Key-not-shipped check**
  - Covers: R13.AC6 · Step S12
  - Files: `client/src/security.test.js`
  - Tests: component "R13.AC6: the dev key from appsettings.Development.json appears nowhere in client/src or client/dist"
  - Depends on: T39
  - Done when: `COMP` green (after `npm --prefix client run build`).
  - Commit: 954af4d
  - S12 review (high, includes the security review done by hand — the bundled `/security-review` needs a GitHub remote, not set up yet): 401 responses now carry `WWW-Authenticate` (HTTP requires it). Kept by design, **to state in the README Security section (T56)**: the committed dev key works only in Development — anywhere else set a long random `Management__Key`; dev runs on http://localhost, so deploy behind HTTPS; wrong keys aren't rate-limited (a long random key makes guessing impractical); the key sits in `sessionStorage` per tab (R13.AC5), and React escapes all output. Masking the key in the request log is R13.AC7, built and tested in S9.
  - Real-app check (servers started by hand from the new project folder; the browser pane's preview launcher still runs the old session folder): full list without a key → 401; Manage view asks for the key; a wrong key → "Invalid management key." and stays locked; the dev key unlocks; disable + enable Average succeed with the key; Lock clears the stored key and shows the prompt again. Found and fixed: the Unlock button stretched across the row.
  - Also: reads every `Management:Key` from `src/Gaia.Api/appsettings*.json` (dev and E2E) and scans `client/src` and `client/dist`; `KeyGate.test.jsx` now uses a made-up key (it had the real dev value); `scripts/test-all.ps1` builds the client before the component tests so the dist scan always runs there (a plain `npm test` without a build skips that one case and reports it as skipped).

## S8 · History, last 3, monthly count   (Could) · ~1.75 h

- [x] **T41 · OperationRun storage** [P]
  - Covers: foundation for R7 · Step S8
  - Files: `src/Gaia.Core/History/IRunRepository.cs`, `RunRecord.cs`, `src/Gaia.Infrastructure/Data/EfRunRepository.cs`, `GaiaDbContext.cs` (OperationRun), migration `*AddOperationRuns*`
  - Tests: integration "T41: migration creates OperationRuns with index"
  - Depends on: T40
  - Done when: `INT` green.
  - Commit: 265f09c
  - Note: the entity is `Core/History/OperationRun.cs` (the plan's data-model name) instead of `RunRecord.cs`; `OperationRunEntityConfig.cs` holds the table shape (same pattern as Operations). Also a test that a run must point at an existing operation (foreign key). `IRunRepository` reads by "latest successful except this run" and "count successful since".

- [x] **T42 · Israel month boundary** [P]
  - Covers: R9.AC4 · Step S8
  - Files: `src/Gaia.Core/History/IsraelMonth.cs`, `tests/…/IsraelMonthTests.cs`
  - Tests: unit "R9.AC4: 1st 00:30 Israel time belongs to the new month (winter, UTC+2)"; unit "R9.AC4: same in summer (UTC+3)"
  - Depends on: T40
  - Done when: `UNIT` green.
  - Commit: 4636cea
  - Also: a test that the offset of the 1st is used, not today's (1 Oct 2026 was summer time, 30 Oct winter time); `StartUtc(DateTime)` plus a `TimeProvider` overload.

- [x] **T43 · Record every run**
  - Covers: R7.AC1, R7.AC2, R7.AC3 · Step S8
  - Files: `src/Gaia.Core/History/RunRecorder.cs`, `OperationService.cs`, `Program.cs` (swap `IRunRecorder`)
  - Tests: integration "R7.AC1: success and failure runs stored with all fields"; integration "R7.AC2: a new host on the same DB reads earlier runs"; unit "R7.AC3: run repository failure → result still returned, history null, error logged"
  - Depends on: T41
  - Done when: `UNIT` and `INT` green.
  - Commit: e6737d9
  - Notes: `RunRecorder` stores a run for every execution of an existing operation (success with result; failure with code + message; unexpected errors as `internal_error` with no internals). Inputs, result and message are shortened to their column sizes. A store failure is logged at Error and never changes what the caller gets (R7.AC3). Unknown ids record nothing (no operation to point at). No `IRunRecorder` swap was needed (no stub existed); Core now references `Microsoft.Extensions.Logging.Abstractions` 10.0.14. The red step here was a compile error (tests and the new constructor landed together). R7.AC2 (new host reads earlier runs) is tested in T44, where history is returned.
  - Test infra: `GaiaApiFactory` retries a timed-out database creation once and always drops a half-made database (LocalDB is sometimes very slow to create one; two runs had failed that way and left 3 test databases, removed by hand).

- [x] **T44 · History in the execute response**
  - Covers: R8.AC1–5, R9.AC1–3 · Step S8
  - Files: `src/Gaia.Core/History/HistoryService.cs`, `ExecuteResponse.cs`, `OperationsController.cs`
  - Tests: integration "R8.AC1: 4 prior runs → 3 newest prior, current excluded"; integration "R8.AC2: 0 and 2 prior runs"; integration "R8.AC3: other operations excluded"; integration "R8.AC4: history in API response"; integration "R8.AC5: failed runs excluded"; integration "R9.AC1: N this month + M last month → N"; integration "R9.AC2: other ops and failed runs not counted"; integration "R9.AC3: monthCount in API response"
  - Depends on: T42, T43
  - Done when: `INT` green.
  - Commit: 67b0743
  - Note: no separate `HistoryService` — reading history lives in `RunRecorder.RecordSuccessAsync` next to storing, so the one rule "history failure: log it, still return the result" is in one place. Integration tests run on a fixed clock (15 Nov 2026) via `FixedClockFactory`; the month boundary is tested with a run at 31 Oct 23:59 vs 1 Nov 00:00 Israel time. R7.AC2 (a new server on the same database sees earlier runs) is tested here. Unit test: a failing history read returns the result with `history: null` and logs an error. The red step was implicit: the response had no `history` field before.

- [x] **T45 · History panel**
  - Covers: R8.AC1, R8.AC2, R9.AC1 (UI) · Step S8
  - Files: `client/src/components/HistoryPanel.jsx`, `HistoryPanel.test.jsx`, `CalculatorView.jsx` (renders it)
  - Tests: component "R8.AC1: shows previous runs newest first"; component "R8.AC2: shows 'No previous runs' when empty"; component "R9.AC1: shows the monthly count"
  - Depends on: T44
  - Done when: `COMP` green.
  - Commit: 91fceda
  - Also: the table uses the operation's own Field A/B labels; times shown in Israel time (A7); "once" for a count of 1; `history: null` shows "History is not available right now." (R7.AC3); the panel keeps the operation of the run that produced it. Calculator tests: history shown after Calculate, and hidden after an error. `styles.css`.
  - S8 review (high): a run is now stored even if the caller disconnects while it runs (the insert no longer uses the request's cancellation; only the history read does) — test added, and the in-memory fake now honours cancellation like the database; the fixed-clock test helper is one shared `tests/Gaia.Core.Tests/FixedTime.cs` (was copied in 3 places). Noted, not changed: a successful execute is now 4 database round trips (find, insert, last 3, count) — measured against NFR1 in T55; the "History is not available right now." note is the honest R7.AC3 behaviour.
  - Real-app check (servers started by hand from this folder): the dev database got the new table automatically on startup; Divide 7÷2 (API), 9÷3, 5÷0 (error), 10÷4 → result 2.5, "Divide ran 3 times this month." (the failed run not counted), previous runs 9÷3 then 7÷2 (current left out), labelled Dividend/Divisor, times in Israel time.

## S9 · Server logs   (Could) · ~1 h

- [x] **T46 · Serilog and correlation id**
  - Covers: foundation for R10/R11 · Step S9
  - Files: `src/Gaia.Api/Logging/CorrelationIdMiddleware.cs`, `Program.cs` (Serilog), `appsettings.json` (two filtered file sinks)
  - Tests: integration "T46: every response has an X-Correlation-Id header"
  - Depends on: T45
  - Done when: `INT` green; `logs/` files appear when running.
  - Commit: 3e6f4e4
  - Notes: the two filtered file outputs are set up in code (`Logging/LogSetup.cs`: errors = Error and above, requests = lines from the request middleware) rather than in appsettings, so the filter rules are readable C# and the tests reuse the same predicates; appsettings keeps only the levels (`Serilog` section) and `LogFiles:Directory` (empty in tests = no files). A caller-sent `X-Correlation-Id` is kept if it is plain (letters, digits, - and _, up to 64), otherwise replaced. Error bodies now carry `correlationId` too (the plan's contract). The R13.AC8 startup-warning test now reads the Serilog in-memory sink (Serilog replaces .NET's logger). Package: Serilog.AspNetCore 10.0.0 (no known vulnerabilities).

- [x] **T47 · Error log**
  - Covers: R10.AC1, R10.AC2 · Step S9
  - Files: `src/Gaia.Api/Logging/ErrorLogging.cs` (exception handler), `ProblemDetailsMapper.cs`
  - Tests: integration "R10.AC1: unhandled exception → error log entry with stack and correlation id; response has no stack"; integration "R10.AC2: 400/404/422 write no error-log entry"
  - Depends on: T46
  - Done when: `INT` green.
  - Commit: 265fb5d
  - Notes: no separate `ErrorLogging.cs` — `ProblemDetailsMapper` (already the one place every exception passes) logs anything that is not a `GaiaException` at Error with the exception, method and path; .NET 10 no longer logs exceptions that an `IExceptionHandler` handles, so without this nothing reached the log (seen red). Tests cause the unexpected error by swapping in a crashing weather handler (no test-only endpoint in the product). During this task LocalDB degraded (test databases timing out); `sqllocaldb stop`/`start` fixed it — to mention in the README.

- [x] **T48 · Request/response log**
  - Covers: R11.AC1, R11.AC2, R13.AC7 · Step S9
  - Files: `src/Gaia.Api/Logging/RequestResponseLoggingMiddleware.cs`
  - Tests: integration "R11.AC1: one entry per request with method, path, bodies, status, duration"; integration "R11.AC2: same correlation id in request log and error log"; integration "R13.AC7: X-Management-Key logged as *** and the key text is in no sink"
  - Depends on: T47
  - Done when: `INT` green.
  - Commit: bee79b5
  - Also: only `/api/...` requests are logged (not Swagger or /health); request headers are logged with `X-Management-Key`, `Authorization` and `Cookie` as `***`; bodies cut at 16 KB with "…(truncated)"; error responses (4xx/5xx) are logged too. Extra tests: error response logged, /health not logged, 16 KB cut.
  - S9 review (high, with a security pass): **log injection fixed** — line breaks and other control characters from the caller (path, query, headers, bodies) are written escaped (`\n`), so a request can't forge log lines (test added); log files now kept for 14 days explicitly (`RetainedDays`); the logs folder is resolved from the app folder (`src/Gaia.Api/logs`) whatever folder the server starts from; the database-down test no longer writes log files. Kept by design, **for the README**: the request log holds what users type (R11 asks for bodies); a database failure gives two error lines (EF Core's and ours, same correlation id).
  - Real-app check: `src/Gaia.Api/logs/requests-YYYYMMDD.log` has one line per API call with method, path, status, duration, headers (`X-Management-Key: ***`), both bodies; the 422's correlation id matches the one in its error body; the dev key text appears 0 times in the logs; division by zero wrote nothing to the error log (business error). Cold first call ~400 ms, warm calls 55–110 ms.

## S10 · E2E + README + submission   (Must) · ~1.5 h

- [x] **T49 · E2E: calculate** [P]
  - Covers: R1.AC1, R2.AC1, R5.AC3 · Step S10
  - Files: `client/e2e/calculate.spec.js`
  - Tests: e2e "R5.AC3: 7 ÷ 2 shows 3.5"
  - Depends on: T48
  - Done when: `E2E` green.
  - Commit: 6e91661
  - Also: `client/e2e/helpers.js` (pick an operation, fill both fields, calculate; read the E2E management key from `appsettings.E2E.json` so it isn't copied into tests). All 5 E2E tests passed on the first run (E2E tests are written after the UI they drive — no red step, per the testing rules).

- [x] **T50 · E2E: error shown** [P]
  - Covers: R5.AC4 · Step S10
  - Files: `client/e2e/errors.spec.js`
  - Tests: e2e "R5.AC4: 5 ÷ 0 shows the domain error and no result"
  - Depends on: T48
  - Done when: `E2E` green.
  - Commit: 6b5340f

- [x] **T51 · E2E: add a formula and use it** [P]
  - Covers: R4.AC1, R12.AC3, R12.AC7, R13.AC4 · Step S10
  - Files: `client/e2e/manage.spec.js`
  - Tests: e2e "R12.AC3: unlock with key, add 'triple' = A * 3 + B, calculate it in the Calculator"
  - Depends on: T48
  - Done when: `E2E` green.
  - Commit: 6456356

- [x] **T52 · E2E: history panel** [P]
  - Covers: R8.AC1 · Step S10
  - Files: `client/e2e/history.spec.js`
  - Tests: e2e "R8.AC1: second calculation shows the first in the history panel"
  - Depends on: T48
  - Done when: `E2E` green.
  - Commit: cf6dd52
  - Note: uses Subtract, which no other E2E test runs, so the history in the fresh E2E database belongs to this test only.
  - S10 review (high): the E2E helper now finds Field A/B by role (`textbox`), not by CSS class (testing rule). Accepted: the history E2E would fail once if run exactly across an Israel month boundary; E2E and dev runs share `src/Gaia.Api/logs`. All levels green: unit 235, integration 104, component 65, E2E 5. Integration took ~2 min even right after a LocalDB restart → most of the time is creating one database per test class; T54 should try one shared database per run.

## S11 · CI   (Should) · ~0.5 h

- [x] **T53 · GitHub Actions workflow**
  - Covers: quality signal · Step S11
  - Files: `.github/workflows/ci.yml` (unit + component on push)
  - Tests: workflow passes on GitHub after push
  - Depends on: T52, T56 (repo exists)
  - Done when: green run on GitHub.
  - Commit: 52e9d88
  - **Setup.** Two parallel jobs on `ubuntu-latest`, on every push and pull request (read-only permissions; a newer push cancels an older run):
    - **Server:** `dotnet build Gaia.sln -c Release` (so the integration-test project must compile too), then the unit tests.
    - **Client:** Node 22 (the README minimum), `npm ci`, lint, build, then the component tests. The build comes first so the R13.AC6 scan of `client/dist` runs.
  - **Actions.** checkout@v7, setup-dotnet@v6, setup-node@v7 (latest majors, checked on GitHub).
  - **First run green** (run 42811438224, ~33 s): 235 unit; 66/66 component, none skipped. Release build checked locally first (0 warnings).
  - **README.** CI badge, plus a CI line in the Tests section.

## Review & verify (always last)

- [x] **T54 · Deep review**: `/code-review high`, `/simplify`, `/security-review` (formula input, management key). Fix the confirmed findings.
  - Commit: 258ebb0
  - Whole-codebase review (high; simplify and security done as angles of it — `/security-review` needs a GitHub remote). **Fixed:** connection strings wait up to 60 s for LocalDB to start (the 15 s default could crash the API at startup — test added); a management key typed in another keyboard layout now says "Invalid management key" instead of "Service unavailable" (test added). **For the README (T56), by design / known limits:** results smaller than 1e-15 show as 0 (15-decimal cap); very large numeric results (> 200 digits) can't be typed back as input; operation names need not be unique; outside Development/E2E the database must be migrated by hand (`dotnet ef database update`); Swagger is served in every environment. Security pass: no vulnerable NuGet/npm packages, no raw HTML/`eval`/raw SQL with input, no secrets beyond the documented dev/E2E/test keys.
  - Test speed: one test class takes ~4 s (database setup ~1.3 s), all 17 classes ~10–14 s; the multi-minute runs were LocalDB being slow, not the per-class databases — no restructure.
  - **For T55:** after a full integration run (≈18 databases created and dropped), LocalDB twice hung the E2E server's start (its database delete/create); `sqllocaldb stop`/`start` fixed it each time. A reviewer running `test-all.ps1` could hit this — decide in T55 (e.g. E2E resets its data instead of dropping the database, and/or a README note).
- [x] **T55 · Verify against the spec**: run `sdd-verify`.
  - Commit: 358a327
  - Result in `verify.md`: ⚠️ ready with known gaps. Every AC passes; fresh clone: unit 235, integration 106, component 66, E2E 5, lint and builds clean. NFR1 measured: warm execute with history, median 18 ms / p95 25 ms. `run.ps1` launched and the main flows walked by hand (API + browser). The LocalDB stall is low system memory (`FAIL_PAGE_ALLOCATION` in the LocalDB log), not the per-class test databases. Gaps G1–G5 became T57–T61. For T56: fix the plan's `fakeApi.js` path (it is `client/src/test/fakeApi.js`).
  - Carry-overs to check here (noted during S1–S5): measure **warm** execute latency vs NFR1 (< 200 ms; the first call after start took 1–3 s, cold start); actually launch `scripts/run.ps1` (not yet run — it opens windows); fresh-clone run per NFR3. From S8–S9: LocalDB degrades over a long session (test databases time out while being created; integration runs went from 10 s to 2–4 min) — a `sqllocaldb stop`/`start` fixes it; consider fewer test databases (one per run instead of one per test class) and a README troubleshooting note.
## Verify fixes (from verify.md, 2026-11-10)

- [x] **T57 · Client install works on a fresh clone (G1)**
  - Covers: NFR3 · Step S10
  - Files: `scripts/run.ps1`, `README.md` (Run section)
  - Tests: manual: in a fresh clone with no `client/node_modules`, `scripts/run.ps1` installs the packages and starts both servers; the README's manual steps work as written.
  - Depends on: —
  - Done when: `npm --prefix client install` is replaced by a form that works on npm 11 (e.g. `npm ci --prefix client`, which installs exactly the lock file) in both places; fresh-clone run passes.
  - Note: verified on a fresh clone with no `client/node_modules`: `run.ps1` ran `npm ci`, built and started both servers (API and UI answering 200) in 48 s.
  - Commit: 0e9806a

- [x] **T58 · test-all.ps1 installs client packages (G2)**
  - Covers: NFR3, R13.AC6 · Step S10
  - Files: `scripts/test-all.ps1`, `README.md` (Tests section)
  - Tests: manual: `test-all.ps1` in a fresh clone gets past "Client build" with no other step first.
  - Depends on: T57
  - Done when: the script runs `npm ci --prefix client` when `client/node_modules` is missing; the README says a first run installs the client packages and Playwright's Chromium (`npx --prefix client playwright install chromium`), and that the R13.AC6 build scan runs only after a client build.
  - Note: verified: `test-all.ps1` on a fresh clone installs the packages in its new "Client packages" step and passes every level. `npx --prefix client playwright …` works on npm 11 (checked `--version`).
  - Commit: efec81b

- [x] **T59 · LocalDB stall after the integration run (G3)**
  - Covers: NFR3 · Step S10
  - Files: `scripts/test-all.ps1`, `README.md` (Troubleshooting)
  - Tests: manual: `test-all.ps1` twice in a row passes on this machine.
  - Depends on: T58
  - Done when: before E2E the script frees memory held by idle build servers (`dotnet build-server shutdown`); the README has a Troubleshooting entry (symptoms: 503 `database_unavailable`, Playwright "Timed out waiting … webServer"; fix: `sqllocaldb stop MSSQLLocalDB` then `sqllocaldb start MSSQLLocalDB`; cause: low free memory). If it still fails twice in a row, record it as a known limitation instead.
  - Note: besides `dotnet build-server shutdown`, the new "Prepare E2E" step also restarts the LocalDB instance (skipped when `sqllocaldb` is not installed): that restart is what fixed the stall every time. Low review: a failing `sqllocaldb start` now fails this step instead of surfacing as an E2E timeout. Verified: `test-all.ps1` twice in a row on a fresh clone, both green (unit 235, integration 108, component 66, E2E 5; 6 m 18 s and 1 m 54 s).
  - Commit: efec81b

- [x] **T60 · Integration test: a disabled operation can't be executed (G4)**
  - Covers: R2.AC2, R4.AC3 · Step S6
  - Files: `tests/Gaia.Api.Tests/Api/ManagementTests.cs`
  - Tests: integration "R2_AC2_executing_a_disabled_operation_returns_404_operation_not_found"
  - Depends on: —
  - Done when: the test passes; `dotnet test tests/Gaia.Api.Tests` green.
  - Commit: c5d1845
  - Note: the behaviour already worked, so red was shown with a temporary mutation (execute ignoring "disabled" → expected 404, got 200), then reverted. Integration: 108/108.

- [x] **T61 · Integration test: deleting an operation keeps its runs (G5)**
  - Covers: R4.AC3, R7 · Step S8
  - Files: `tests/Gaia.Api.Tests/Api/HistoryTests.cs`
  - Tests: integration "R4_AC3_deleting_an_operation_keeps_its_runs"
  - Depends on: —
  - Done when: the test passes; `dotnet test tests/Gaia.Api.Tests` green.
  - Commit: 526d846
  - Note: red shown with a temporary mutation (delete also removing the runs → expected 2, got 0), then reverted.

## S13 · Formula checks on save + form help   (Should) · ~1.5 h — added 2026-11-10 after user testing

- [x] **T62 · Type check on save (TypeChecker)**
  - Covers: R4.AC8 · Step S13
  - Files: `src/Gaia.Core/Formulas/TypeChecker.cs`, `src/Gaia.Core/Formulas/OperatorRules.cs`, `src/Gaia.Core/Formulas/FormulaEngine.cs`, `src/Gaia.Core/Formulas/Evaluator.cs` (message), `src/Gaia.Core/Services/CatalogService.cs`, `tests/Gaia.Core.Tests/Formulas/TypeCheckerTests.cs`, `tests/Gaia.Core.Tests/Formulas/FormulaEngineTests.cs`, `tests/Gaia.Core.Tests/Operations/SeedOperationsTests.cs`, `tests/Gaia.Core.Tests/Services/CatalogServiceTests.cs`
  - Tests: unit "R4.AC8: <case> is rejected" (table), "R4.AC8: <case> is accepted" (table), "R4.AC8: seeded formulas pass with their input type", "R4.AC8: changing only the input type is checked"
  - Depends on: —
  - Done when: tests pass; `dotnet test tests/Gaia.Core.Tests` green.
  - Commit: —
  - Note: red seen first (26 tests: no error raised). The function table now also lists each function's parameter and result types (`Functions.cs`, not in the file list) so the whitelist stays the single source. The run-time message uses the same wording (`OperatorRules`). Unit 276/276.

- [x] **T63 · Always-fails check (ConstantCheck)**
  - Covers: R4.AC9 · Step S13
  - Files: `src/Gaia.Core/Formulas/ConstantCheck.cs`, `src/Gaia.Core/Formulas/FormulaEngine.cs`, `tests/Gaia.Core.Tests/Formulas/ConstantCheckTests.cs`
  - Tests: unit "R4.AC9: <case> is rejected" (table), "R4.AC9: <case> is accepted" (table)
  - Depends on: T62
  - Done when: tests pass; unit suite green.
  - Commit: —
  - Note: red seen first (9 rejected cases passed through). Fixed parts are run now by the same Evaluator/Functions, so messages match the Calculator's; a fixed `if` condition checks only the branch that can run; function calls with fixed arguments are tried with a sample (1 or "x") for A/B parts. Unit 296/296.

- [ ] **T64 · Integration: the API refuses formulas that can't work**
  - Covers: R4.AC8, R4.AC9 · Step S13
  - Files: `tests/Gaia.Api.Tests/Api/ManagementTests.cs`
  - Tests: integration "R4_AC8_POST_A_power_B_with_text_input_is_400_and_the_catalog_is_unchanged", "R4_AC8_PUT_changing_only_the_input_type_to_text_is_400", "R4_AC9_POST_dividing_by_a_fixed_zero_is_400"
  - Depends on: T63
  - Done when: tests pass; integration suite green.
  - Commit: —

- [ ] **T65 · Form field help**
  - Covers: R12.AC9 · Step S13
  - Files: `client/src/components/OperationForm.jsx`, `client/src/styles.css`, `client/src/components/OperationForm.test.jsx`
  - Tests: component "R12.AC9: the form explains Kind, Input type and the labels"
  - Depends on: —
  - Done when: test passes; component suite and lint green.
  - Commit: —

- [ ] **T66 · S13 review, all levels, README**
  - Covers: S13 · Step S13
  - Files: `README.md` (formula section, examples), fixes from the review
  - Tests: every level green (`scripts/test-all.ps1`); CI green after push
  - Depends on: T62–T65
  - Done when: review findings fixed; pushed; CI green.
  - Commit: —

## Submit (always last)

- [x] **T56 · Polish & submit**: README (setup, run, test commands, formula language reference, interpretations A5/A8/A9, Security section, known limitations, trade-offs), no secrets beyond the documented dev key, clean history, Submission notes in plan.md, create the **private** GitHub repo and push (needs your OK), invite the reviewer.
  - Part 1 done (README + Submission notes):
    - **README.** All sections are written: try-it tour, API with curl examples, formula language, how it works, configuration, logs, interpretations, security, known limitations, with more time, troubleshooting. Every curl example and formula example was run against the dev API.
    - **Fixed claim.** `dotnet ef database update` needs `--connection` (the design-time factory always points at `GaiaDev`); checked on a throwaway database.
    - **plan.md.** Submission notes written; the client fake's path corrected (`src/test/fakeApi.js`).
    - **Hygiene.** No personal paths in tracked files; the only keys are the documented dev/E2E/test ones; `assignment.md` never committed.
  - Commit: 31d7428 (part 1)
  - Part 2 done (2026-11-10, with the user's OK):
    - **Repo.** GitHub CLI installed (winget); the user logged in themselves; `gh repo create gaia-operations-platform --private --source . --push`.
    - **Checked.** Visibility is PRIVATE; remote `main` = local HEAD; 145/145 files; no `assignment.md`; the `// A34D` line is in `Program.cs`.
    - **Reviewer.** The user invites the reviewer as a collaborator themselves (Settings → Collaborators).
