# Gaia Operations Platform: Verification

**Date:** 2026-11-10 · **Commit:** 6f3a514 · **Spec:** spec.md (approved 2026-11-09, with R13)

## Verdict: ⚠️ Ready with known gaps

Every acceptance criterion has passing tests, and the real app behaves as the spec says. Three things block a smooth review: on npm 11, a fresh clone can't install the client packages the way the README says; `test-all.ps1` doesn't install them either; and on a machine short of memory, LocalDB can stall right after the integration run. These are tasks T57–T59. Two small test gaps are T60–T61. The README, Submission notes, CI and the GitHub repo are still open, as planned (T56, T53).

## 1. Clean-clone run

A fresh `git clone` into the scratchpad. `assignment.md` is not in the clone, and no commit has ever contained it (C7 ✅).

| Level | Command | Result |
|---|---|---|
| Unit (server) | `dotnet test tests/Gaia.Core.Tests` | ✅ 235 / 235 |
| Integration (server) | `dotnet test tests/Gaia.Api.Tests` | ✅ 106 / 106 (29 s; 1 m 48 s on the second run, see G3) |
| Server build | `dotnet build Gaia.sln` | ✅ 0 warnings, 0 errors |
| Client install | `npm --prefix client install` (README "Run") | ❌ **G1**: npm 11.1 looks for `package.json` in the repo root (ENOENT). `npm --prefix client ci` works. |
| Lint | `npm --prefix client run lint` | ✅ clean |
| Client build | `npm --prefix client run build` | ✅ |
| Component | `npm --prefix client test -- --run` | ✅ 66 / 66 (9 files) |
| E2E | `npm --prefix client run e2e` | ✅ 5 / 5 when run alone (15–33 s) |
| All | `scripts/test-all.ps1` | ❌ **G2**: on a fresh clone it stops at "Client build" (`vite` not found: nothing installs the client packages). With packages installed, ❌ **G3**: the E2E server timed out (120 s) right after the integration run; E2E passed alone straight afterwards. |

## 2. Traceability matrix

Counts are test names that carry the AC id (a `[Theory]` counts once). Expected levels come from the plan's test strategy.

| AC | Pri | Expected (plan) | Unit | Integ | Comp | E2E | Result |
|---|---|---|---|---|---|---|---|
| R1.AC1 | Must | int, e2e | | 1 | | (calculate) | ✅ |
| R1.AC2 | Must | int | | 1 | | | ✅ |
| R1.AC3 | Must | int | | 1 | | | ✅ |
| R2.AC1 | Must | unit, int, e2e | 2 | 2 | | (R5.AC3) | ✅ |
| R2.AC2 | Must | unit, int (unknown **and disabled**) | 1 | 1 | | | ⚠️ **G4**: the integration test covers an unknown id only; "disabled → 404" is unit-only (checked by hand ✅) |
| R2.AC3 | Must | unit, int | 2 | 1 | | | ✅ |
| R2.AC4 | Must | unit, int | 4 | 2 | | | ✅ |
| R2.AC5 | Must | unit, int | 7 | 1 | | | ✅ |
| R2.AC6 | Must | int | | 3 | | | ✅ |
| R3.AC1 | Must | unit | 1 | | | | ✅ |
| R3.AC2 | Must | unit | 4 | | | | ✅ |
| R3.AC3 | Must | unit, int | 3 | 1 | | | ✅ |
| R3.AC4 | Must | unit, int | 3 | 2 | | | ✅ |
| R3.AC5 | Must | unit | 2 | | | | ✅ |
| R4.AC1 | Should | int, e2e | | 1 | | 1 | ✅ |
| R4.AC2 | Should | int | 1 | 1 | | | ✅ |
| R4.AC3 | Should | int (incl. "runs still in DB") | 3 | 1 | 2 | | ⚠️ **G5**: the test checks the operation row is kept, not that its runs are (checked by hand ✅) |
| R4.AC4 | Should | unit, int | 16 | 3 | | | ✅ |
| R4.AC5 | Should | int | | 1 | | | ✅ |
| R4.AC6 | Should | unit, int | 4 | 3 | | | ✅ |
| R4.AC7 | Should | int | 2 | 2 | | | ✅ |
| R5.AC1 | Must | comp | | | 1 | | ✅ |
| R5.AC2 | Must | comp | | | 1 | 1 | ✅ |
| R5.AC3 | Must | comp, e2e | | | 1 | 1 | ✅ |
| R5.AC4 | Must | comp | | | 3 | 1 | ✅ |
| R5.AC5 | Must | comp | | | 7 | | ✅ |
| R6.AC1 | Must | comp | | | 1 | | ✅ |
| R6.AC2 | Must | int | | 1 | | | ✅ |
| R12.AC1 | Should | comp | | | 2 | | ✅ |
| R12.AC2 | Should | comp | | | 2 | | ✅ |
| R12.AC3 | Should | comp, e2e | | | 1 | 1 | ✅ |
| R12.AC4 | Should | comp | | | 3 | | ✅ |
| R12.AC5 | Should | comp | | | 2 | | ✅ |
| R12.AC6 | Should | comp | | | 1 | | ✅ |
| R12.AC7 | Should | comp, e2e | | | 1 | 1 | ✅ |
| R12.AC8 | Should | comp | | | 1 | | ✅ |
| R13.AC1 | Should | int | | 1 | | | ✅ |
| R13.AC2 | Should | int | | 2 | | | ✅ |
| R13.AC3 | Should | int | | 1 | | | ✅ |
| R13.AC4 | Should | comp, e2e | | | 4 | 1 | ✅ |
| R13.AC5 | Should | comp | | | 3 | | ✅ |
| R13.AC6 | Should | scan | | | 4 | | ✅ (the `client/dist` scan is skipped when no build exists; `test-all.ps1` builds first, see G2) |
| R13.AC7 | Should | int | | 1 | | | ✅ |
| R13.AC8 | Should | int | | 3 | | | ✅ |
| R7.AC1 | Could | int | 3 | 1 | | | ✅ |
| R7.AC2 | Could | int | | 1 | | | ✅ |
| R7.AC3 | Could | unit | 3 | | 1 | | ✅ |
| R8.AC1 | Could | int, e2e | 1 | 1 | 3 | 1 | ✅ |
| R8.AC2 | Could | int, comp | | 1 | 1 | | ✅ |
| R8.AC3 | Could | int | | 1 | | | ✅ |
| R8.AC4 | Could | int | | 1 | | | ✅ |
| R8.AC5 | Could | int | | 1 | | | ✅ |
| R9.AC1 | Could | int | | 1 | 2 | | ✅ |
| R9.AC2 | Could | int | | 1 | | | ✅ |
| R9.AC3 | Could | int | | 1 | | | ✅ |
| R9.AC4 | Could | unit | 4 | | | | ✅ |
| R10.AC1 | Could | int | | 1 | | | ✅ |
| R10.AC2 | Could | int | | 1 | | | ✅ |
| R11.AC1 | Could | int | | 2 | | | ✅ |
| R11.AC2 | Could | int | | 1 | | | ✅ |
| NFR2 | — | unit | 2 | | | | ✅ |
| NFR5 | — | unit | 2 | | | | ✅ (`// A34D` is line 1 of `src/Gaia.Api/Program.cs`) |
| NFR6 | — | unit | 7 | | | | ✅ |

Sampled test bodies (R1.AC1, R4.AC3, R6.AC2, R7.AC2, R8.AC1, R13.AC5, R13.AC7, R12.AC8, R6.AC1): the assertions check what the names claim, apart from G5.

## 3. Real app

Started with `scripts/run.ps1` from the clone: both windows opened, `/health` answered and the browser opened, in about 10 s with build caches warm.

**API (curl):**
- 7 ÷ 2 → 200 `3.5`, with history (3 earlier runs, newest first, plus the month count).
- 5 ÷ 0 → 422 `domain_error`.
- "abc" → 400 `validation_error`, `field: fieldA`.
- "3,5" → 400.
- Missing B → 400, `field: fieldB`.
- Unknown id → 404 `operation_not_found`, naming the id.
- Latitude 95 → 400.
- Real weather (Tel Aviv) → "Clear sky, 9–23 °C".
- concat → "foobar"; −2.5 ^ 2 → 6.25.

**Management (dev key):**
- Create → 201; run it → 9.
- Disable → executing it gives 404.
- PUT with a stale version → 409 `edit_conflict`.
- Bad formula → 400 `formula_invalid` with its position.
- Changing `divide`'s formula → 400 `system_logic_readonly`.
- Delete → 204; its 1 run is still in `OperationRuns`.
- No key → 401; `includeDisabled=true` with no key → 401.
- Swagger UI → 200.

**Logs:**
- The key appears in no log file; the request log shows `X-Management-Key: ***`.
- Unexpected errors (the LocalDB timeouts, see G3) reached `errors-*.log` with the correlation id and stack trace, and the caller got a 503 `database_unavailable` with no internals.

**UI (browser):**
- Choosing Weather turns the labels into Latitude/Longitude.
- Calculate shows the result plus "Weather today ran 2 times this month." and the history table.
- Manage asks for the key: a wrong key gives "Invalid management key."; the dev key unlocks it.
- The list shows formulas, and built-in operations have no Delete.
- Disabling Power removes it from the calculator with no reload; re-enabled afterwards.
- With the API stopped, the calculator shows "Service unavailable. Please try again in a moment." and a Try again button.

## 4. Drift

- **Spec → code:** all R1–R13 are built. No `TODO`/`FIXME`/`not implemented` left. The only conditional skip is the R13.AC6 build scan (`skipIf` no `client/dist`).
- **Code → spec:** `GET /health` (used by `run.ps1` and Playwright) and the Swagger UI are the only extras. Both are tooling, not features.
- **Plan → code:** endpoints, JSON shapes, error codes, `X-Correlation-Id` and the shared names match. One doc slip: the plan names `client/src/api/fakeApi.js`, but the fake lives in `client/src/test/fakeApi.js` (it's test-only), and `api/definitions.js` was added. Update the plan text in T56.
- **NFRs:**
  - NFR1 ✅: warm execute over HTTP with history on. multiply: median 18 ms, p95 25 ms (20 calls); concat: median 15 ms, p95 19 ms. Cold first call 300–450 ms.
  - NFR2 ✅: unit tests.
  - NFR3 ❌ until G1 is fixed: the README's install step fails on npm 11. With it fixed, a clone reaches a running app in a few minutes.
  - NFR4 ✅: the largest source file is 195 lines; no template leftovers.
  - NFR5 ✅. NFR6 ✅.

## 5. Submission readiness

- [ ] README covers what, setup, run, tests, assumptions, trade-offs and "with more time". Sections are still marked _(coming)_ (T56); the run and test steps need G1/G2.
- [x] No secrets committed beyond the documented dev/E2E/test keys; `assignment.md` never committed. (.NET project: instead of `.env.example`, the README must show `Management__Key` as an environment variable, T56.)
- [x] Lint and both builds are clean; no debug output.
- [x] History is readable: 67 task-sized commits (`T<n>: …`, `S<n>: review fixes`).
- [x] Constraints C1–C10 addressed (C6, the private repo, is pending T56).
- [ ] Submission notes in plan.md (T56).

## Gaps

| # | Gap | Severity | Task |
|---|---|---|---|
| G1 | `npm --prefix client install` (README Run step, and `run.ps1` on first run) fails on npm 11: it reads `package.json` from the repo root. | High: a reviewer can't start the client | T57 |
| G2 | `test-all.ps1` never installs client packages, so on a fresh clone it fails at "Client build". The README Tests section doesn't mention installing them, or that the R13.AC6 scan needs a build. | Medium | T58 |
| G3 | Right after the integration run, LocalDB can stall. On this machine (7.7 GB RAM, 0.7 GB free) the LocalDB log shows `FAIL_PAGE_ALLOCATION` and a 48 KB buffer pool, so the E2E server misses its 120 s start window and the dev API returns 503s. `sqllocaldb stop`/`start` (plus `dotnet build-server shutdown`) fixes it. Cause: low system memory, not the code. | Medium: shows up on low-memory machines | T59 |
| G4 | No integration test executes a *disabled* operation over HTTP (R2.AC2, R4.AC3). | Low: unit-tested and checked by hand | T60 |
| G5 | No test checks that a deleted operation's runs are kept (R4.AC3 "past history is kept"). | Low: FK `Restrict` + soft delete; checked by hand | T61 |
