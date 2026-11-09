# Gaia Operations Platform: Specification

**Status:** Approved (2026-11-09, re-approved after R13)
**Source:** [assignment.md](assignment.md) — "פרויקט גאיה – תרגיל פיתוח תוכנה", edited 01.09.2026 (received as pasted text; the UI mockup image was not included)

## Summary
A platform that runs *operations* on exactly two inputs (Field A and Field B) and returns a result. Operations come in different kinds — arithmetic, string, and "other" (for example today's weather forecast for a latitude/longitude pair). The platform is used by end users through a minimal UI and by external applications through an API. Success means: an end user or external app can list the available operations, run any of them on two values and get a correct result or a clear error, and an operator can add, change or remove operations without new development. Bonus: every run is stored as history, the UI shows recent same-operation history and a monthly count, and the server keeps an error log and a request/response log.

## Actors
| Actor | Needs |
|---|---|
| **End user** | Enter Field A, pick an operation, enter Field B, press Calculate, see the result (and, bonus, recent history). |
| **External application** | Discover the available operations and execute one programmatically, with machine-readable results and errors. |
| **Operator** | Whoever changes the catalog — not a user account; anyone who knows the management key (R13, A6). Adds, changes and removes operations without a code change or redeployment. |
| **Reviewer** (assignment evaluator) | Clone the repo, run it locally with documented steps, read the code, find the "A34D" comment. |
| **Weather provider** (external system) | Supplies today's forecast for a coordinate; may be slow or unavailable. |

## Scope
| In scope (Part A, Must) | Bonus / optional (Part B, Could) | Out of scope |
|---|---|---|
| Operation catalog API | Persistent history of every run | User accounts, login, roles; keys for listing/executing (see A6) |
| Operation management screen (R12, Should) | | |
| Management key protecting add/change/delete (R13, Should) | | |
| Execute-operation API | Last 3 runs of the same operation shown after Calculate | Multi-language UI |
| Arithmetic, string and weather operations | Count of same-operation runs since start of current month | Weather beyond "today" (multi-day, hourly) |
| Minimal end-user UI | Server error log | Production deployment / hosting |
| Business logic only on the server | Server request/response log | Load balancing, high availability |
| Runtime management of operations (Should) | | |

**Decision (2026-11-09):** all of R1–R13 are in this version. Part A first (R1–R3, R5, R6), then R4, R12, R13, then R7–R9, then R10–R11.

## Constraints
- **C1 · Server stack:** the server-side application is developed in **C# on .NET** [§ general, 2.1].
- **C2 · Database:** a database is optional for Part A; if one is used it must be **Oracle (XE)** or **Microsoft SQL Server (Developer/Express)** [2.1]. Part B history requires a database [B.1.1]. **Decision (2026-11-09): Microsoft SQL Server** — LocalDB on the dev machine, and it must also work against SQL Server Express/Developer by changing configuration only.
- **C3 · Client technology:** free choice [2.1, 2.2.2]. **Decision (2026-11-09): React** (needs Node.js on the dev and reviewer machines; README covers it).
- **C4 · Marker comment:** the code must contain the comment **"A34D"** — flagged "very important" by the assignment [2.1].
- **C5 · Tooling:** Microsoft Visual Studio is the expected IDE; the solution must open and run in it [general].
- **C6 · Submission:** a link to the project, preferably on GitHub [general]. **Decision (2026-11-09):** a **private** GitHub repo with the reviewer invited as a collaborator.
- **C7 · Confidentiality:** the exercise must not be shared with external parties [general] → the assignment text (`assignment.md`) is never committed to the repo.
- **C11 · Time budget:** about 12–16 working hours in total; deadline not given (see Q2).
- **C12 · Dev machine starts empty:** the development machine has no .NET SDK, Visual Studio, SQL Server, git or Node installed (checked 2026-11-09); the README must list everything a reviewer needs.
- **C8 · AI use:** allowed, but the candidate must be able to explain every line of code [general] → favour small, readable code over clever or generated bulk.
- **C9 · Evaluation:** creative solutions are preferred [general]; Part B counts only if Part A is complete, and partial Part B counts [B].
- **C10 · Two inputs:** every operation always takes exactly two parameters, Field A and Field B [1.1].

## Requirements

### R1 · Operation catalog   (Must)  [1.2.1, 2.2.1]
The platform exposes, through its API, the list of all currently available operations, so that the UI and external applications can discover them.

- **R1.AC1:** When a client requests the operation list, the system returns every enabled operation with at least: a unique identifier, a display name, its kind (arithmetic / string / other), and a description of what Field A and Field B mean (for example "Latitude" / "Longitude").
- **R1.AC2:** When an operation has been removed or disabled (R4), it no longer appears in the list.
- **R1.AC3:** When no operations are enabled, the list is empty and the call still succeeds.

### R2 · Execute an operation   (Must)  [1.1, 1.2.1, 2.2.1]
The API accepts Field A, Field B and an operation identifier, runs the operation on the server, and returns its result.

- **R2.AC1:** Given an enabled operation and valid inputs, when a client executes it, the system returns the result value and the operation identifier.
- **R2.AC2:** When the operation identifier is unknown or disabled, the system returns a "not found" error that names the identifier, and no result.
- **R2.AC3:** When Field A or Field B is missing, the system returns a validation error naming the missing field.
- **R2.AC4:** When an input is not valid for the operation (for example a non-number for an arithmetic operation, or a latitude outside −90…90), the system returns a validation error naming the field and the reason, and no result.
- **R2.AC5:** When the operation itself cannot produce a result (for example division by zero), the system returns a domain error with a readable message, not a crash or a generic server error.
- **R2.AC6:** Errors are machine-readable: an external application can tell validation, not-found, domain and external-service errors apart without parsing the message text.

### R3 · Built-in operation kinds   (Must)  [1.1]
The platform ships with operations of at least three kinds, all on exactly two inputs.

- **R3.AC1 (arithmetic):** At least add, subtract, multiply, divide and power are available. E.g. A=7, B=2 → add=9, subtract=5, multiply=14, divide=3.5, power=49.
- **R3.AC2 (string):** At least concatenate and one more string operation are available (see A3). E.g. concatenate("foo", "bar") = "foobar".
- **R3.AC3 (weather):** Given Field A = latitude and Field B = longitude, the "weather today" operation returns today's forecast for that location including at least min and max temperature and a short description of conditions.
- **R3.AC4 (weather failure):** When the weather provider is unreachable, slow beyond the timeout (NFR2) or returns an error, the system returns an external-service error with a readable message, not a crash.
- **R3.AC5 (numbers):** Arithmetic accepts integers and decimals, including negative numbers, using "." as the decimal separator.

### R4 · Manage operations without development   (Should)  [1.1 "רצוי…"]
An operator can add, change and remove operations at runtime, with no code change, rebuild or redeploy.

- **R4.AC1 (add):** When an operator adds a new operation (see A2 for what can be defined), it appears in the catalog (R1) and can be executed (R2) without restarting the server.
- **R4.AC2 (change):** When an operator changes an operation's name, description or definition, the next catalog request and the next execution use the new version.
- **R4.AC3 (remove):** When an operator removes or disables an operation, it disappears from the catalog and executing it returns "not found" (R2.AC2). Its past history (R7) is kept.
- **R4.AC4 (invalid definition):** When an operator submits a definition that can't be run (for example a malformed formula, or a duplicate identifier), the system rejects it with an error explaining why, and the existing catalog is unchanged.
- **R4.AC5 (through the API):** Add, change and remove are all available as API calls, and the API's built-in documentation page lets a reviewer try them from a browser without writing a client.
- **R4.AC6 (built-ins):** A built-in operation can be renamed or disabled through the API; a request to change its logic is rejected with an error saying built-in logic can't be changed.
- **R4.AC7 (concurrent edits):** Given two clients loaded the same version of an operation, when the first saves a change and the second then saves its own change, the second save is rejected with a conflict error ("changed by someone else, reload"), and the operation keeps the first client's values. A save based on the current version succeeds.

### R5 · End-user UI   (Must)  [1.2.2, 2.2.2]
A minimal UI where the user enters Field A, selects an operation, enters Field B, presses Calculate and sees the result.

- **R5.AC1:** When the UI loads, the operation selector lists the operations from the catalog (R1); it is never a hard-coded list.
- **R5.AC2:** When the user selects an operation, the Field A and Field B labels show that operation's input descriptions (e.g. "Latitude"/"Longitude" for weather).
- **R5.AC3:** When the user fills both fields, selects an operation and presses Calculate, the result is shown on the same screen.
- **R5.AC4:** When the server returns an error (any kind from R2.AC6), the UI shows its message near the form; no result is shown.
- **R5.AC5:** When the server is unreachable, the UI shows a "service unavailable" message instead of failing silently.

### R6 · Business logic lives on the server   (Must)  [2.2.1, 2.2.2]
All computation and validation rules live in the server; the client only displays.

- **R6.AC1:** Every result shown in the UI is the value returned by the server API for the same inputs; the client computes nothing.
- **R6.AC2:** An external application calling the API directly with the same inputs gets the same result and the same validation errors as the UI.

### R12 · Operation management screen   (Should)  [1.1 "רצוי…", 1.2.2]
The UI has a second view, separate from the calculator but in the same app, where an operator manages the catalog through the R4 API — so that a non-developer can add, change and remove operations.

- **R12.AC1 (navigation):** From the calculator, one click opens the "Manage operations" view, and one click returns; both views are part of the same app with no second login or URL to remember.
- **R12.AC2 (list):** The view lists all operations (enabled and disabled) with name, kind, Field A/B labels, formula (for formula operations) or "built-in", and enabled status.
- **R12.AC3 (add):** When the operator fills the add form (identifier, name, kind, Field A/B labels, input type, formula) and saves, the new operation appears in the list.
- **R12.AC4 (edit):** When the operator edits an operation and saves, the list shows the new values; for a built-in operation only the name, labels and enabled status are editable.
- **R12.AC5 (disable/enable):** When the operator disables an operation it is shown as disabled in the list; enabling it again restores it.
- **R12.AC6 (invalid):** When the server rejects a definition (R4.AC4), the form shows the server's error message next to the form and keeps what the operator typed.
- **R12.AC7 (live):** After adding, changing or disabling an operation, returning to the calculator shows the updated operation list without a page reload or server restart.
- **R12.AC8 (conflict):** When a save is rejected because someone else changed the operation first (R4.AC7), the form shows "This operation was changed by someone else. Reload to see the latest version.", keeps what the operator typed, and offers a Reload action that loads the latest version.

### R13 · Management key   (Should)  [not in the assignment; user decision 2026-11-09]
Adding, changing and removing operations requires a secret management key configured on the server. Listing and executing operations stay open to everyone.

- **R13.AC1 (rejected):** When an add, change or remove request has no key or a wrong key, the system rejects it with an "unauthorized" error, and the catalog is unchanged.
- **R13.AC2 (accepted):** When the request carries the correct key, add, change and remove behave as in R4.
- **R13.AC3 (open calls):** Listing enabled operations (R1) and executing operations (R2) never require a key.
- **R13.AC4 (UI unlock):** When the operator opens the Manage view, it asks for the management key before showing management actions; a correct key unlocks the view, and a wrong key shows "Invalid management key" and keeps it locked.
- **R13.AC5 (UI session):** The key is remembered only for the current browser tab; closing the tab, or pressing "Lock", requires entering it again.
- **R13.AC6 (not shipped to browsers):** The key is set only in server configuration; it doesn't appear in any file the server or the client build sends to a browser.
- **R13.AC7 (not logged):** The key never appears in plain text in any server log (R10, R11); the request log shows it masked.
- **R13.AC8 (no key configured):** When the server has no management key configured, management requests are rejected (closed by default), and the server logs a startup warning.

### R7 · Operation history   (Could)  [B.1.1]
Every execution and its outcome is stored in the database.

- **R7.AC1:** When an operation is executed, a history record is stored with: operation identifier, Field A, Field B, result (or error), success/failure, and timestamp.
- **R7.AC2:** History records survive a server restart.
- **R7.AC3:** When the history store is unavailable, the execution still returns its result to the caller, and the failure is written to the error log (R10). (see A9)

### R8 · Last 3 runs of the same operation   (Could)  [B.1.2.1]
After Calculate, the UI also shows the details of the 3 most recent runs of the same operation.

- **R8.AC1:** Given at least 3 earlier successful runs of operation X, when the user calculates X, the UI shows the 3 most recent successful runs of X **before the current one** (Field A, Field B, result, time), newest first (A5, A9).
- **R8.AC2:** Given fewer than 3 earlier successful runs of X, the UI shows the ones that exist, and with zero it shows "No previous runs".
- **R8.AC5:** Failed runs of X (validation, domain or external-service errors) never appear in the list (A9).
- **R8.AC3:** Runs of other operations never appear in X's list.
- **R8.AC4:** The same data is available to external applications through the API.

### R9 · Monthly count of the same operation   (Could)  [B.1.2.2]
After Calculate, the UI also shows how many runs of the same operation happened since the start of the current month.

- **R9.AC1:** Given N successful runs of X in the current calendar month in Israel time (A7), including the current run, and M runs in earlier months, the UI shows N.
- **R9.AC2:** Runs of other operations, and failed runs of X, are not counted (A9).
- **R9.AC4:** A run at 00:30 Israel time on the 1st of the month counts toward the new month, even though it is still the previous month in UTC.
- **R9.AC3:** The same count is available to external applications through the API.

### R10 · Server error log   (Could)  [B.1.3.1]
The server writes a log of system errors.

- **R10.AC1:** When an unexpected error occurs while handling a request, an entry with timestamp, request identifier, error message and stack trace is written to the error log, and the caller gets a generic error without internal details.
- **R10.AC2:** Expected business errors (validation, not found, division by zero) are not logged as system errors.

### R11 · Server request/response log   (Could)  [B.1.3.2]
The server writes a log of every API request and its response.

- **R11.AC1:** For every API request, the log contains: timestamp, request identifier, method, path, request body, response status, response body and duration.
- **R11.AC2:** The request identifier in the request/response log matches the one in the error log for the same request, so they can be correlated.

## Non-functional requirements
- **NFR1 · Latency (local ops):** arithmetic and string operations return in under 200 ms at the API, measured on the reviewer's machine with history enabled.
- **NFR2 · Latency (external ops):** the weather call times out after 5 s and returns R3.AC4's error.
- **NFR3 · Runnable by the reviewer:** with the prerequisites listed in the README installed (.NET SDK, SQL Server LocalDB/Express, Node.js), the README's steps take a fresh clone to a working server, database and UI in under 10 minutes, on Windows with Visual Studio.
- **NFR4 · Explainable code:** no file the candidate can't walk through; no unused generated code (C8).
- **NFR5 · Marker:** a search of the repository for "A34D" finds it as a code comment in the server code (C4).
- **NFR6 · Safe definitions:** an operation defined at runtime (R4) can't execute arbitrary code on the server, read files, or call the network beyond what its kind allows.

## Assumptions
| # | What's unclear | Choice | Reason | Status |
|---|---|---|---|---|
| A1 | What "operations provided by users of the system according to rules you set" means. | Operations are defined by the **operator** (admin) under the platform's rules (R4); end users only run them. | Simplest reading consistent with "add, change, remove without further development". | ✔ Confirmed 2026-11-09 |
| A2 | What an operator can define without development. | Arithmetic/string **formulas over A and B** (e.g. `A * B + 1`, `upper(A) + B`) from a fixed safe function set, plus parameterised calls to built-in kinds. Wholly new external integrations (like a new weather-like API) still need code. | The only safe way to add behaviour without deploying code (NFR6); creative and explainable. Operator-configured HTTP integrations are out of scope (security risk, hours). Built-in operations can be renamed and disabled but their logic can't be changed. | ✔ Confirmed 2026-11-09 |
| A3 | Which string operations ship. | concatenate, and "count occurrences of B in A"; more can be added as formulas. | Assignment gives no list; two shows the kind works. | ✔ Confirmed 2026-11-09 |
| A4 | Input types. | Both fields are sent as text; each operation declares how it parses them (number / text / coordinate). | Lets one API shape serve all kinds. | ✔ Confirmed 2026-11-09 |
| A5 | "Last 3 operations of the same type": does it include the run just made? | It **excludes** the current run — the current result is already shown; the list shows the 3 runs before it. The monthly count (R9) **includes** it. | Avoids showing the same run twice on screen. | ✔ Confirmed 2026-11-09 |
| A6 | Who may manage operations. | No user accounts or login. Listing and executing are open; add/change/remove require one shared **management key** (R13). README "Security" section: production would add per-app API keys, user login for the UI, and rate limiting. | The assignment mentions no users or security, but leaving destructive endpoints open is the one hole a reviewer would find; a server-side key typed in by the operator protects them without shipping a secret to browsers. | ✔ Changed 2026-11-09 (was: no auth at all) |
| A7 | Time zone for "start of the current month". | Israel time (Asia/Jerusalem); timestamps stored in UTC. | Assignment is Israeli; avoids off-by-one at month boundaries. | ✔ Confirmed 2026-11-09 |
| A8 | "Same type" in Part B: same operation or same kind? | Same **operation** (e.g. "divide"), not the same kind (arithmetic). | "פעולות מאותו סוג" most naturally reads as the chosen operation; "last 3 arithmetic" would be a mixed, less useful list. The README states this interpretation. | ✔ Confirmed 2026-11-09 |
| A9 | Do failed runs count in history? | Failed runs (validation/domain/external errors) are stored with their error, but R8 and R9 show/count **successful runs only**. | History is "every operation and its results"; the user-facing stats are about results. | ✔ Confirmed 2026-11-09 |
| A10 | UI language. | English, button labelled "Calculate". | Part B calls the button "CALCULATE". | ✔ Confirmed 2026-11-09 |

## Open questions
| # | Question | For whom | What it blocks |
|---|---|---|---|
| Q2 | What's the deadline? (Budget agreed: ~12–16 h.) | user | Nothing yet; scheduling only |

## Glossary
- **Operation:** a named, executable rule that takes Field A and Field B and returns a result or an error.
- **Kind:** the category of an operation — arithmetic, string, or other (e.g. weather).
- **Field A / Field B:** the two inputs every operation receives, sent as text.
- **Catalog:** the list of currently enabled operations.
- **Run / execution:** one call of an operation with specific inputs.
- **History:** stored records of runs.
- **Operator:** whoever manages the catalog (adds, changes, removes operations).

## Changelog
| Date | Change | Reason |
|---|---|---|
| 2026-11-09 | First draft from the assignment | Phase 1 start |
| 2026-11-09 | Grill round 1: private repo (C6/C7), SQL Server (C2), budget (C11), empty dev machine (C12), full scope R1–R11, confirmed A1/A3/A4/A5/A7/A9/A10 | User answers |
| 2026-11-09 | Client technology: React (C3) | User decision |
| 2026-11-09 | Confirmed A2 (formula operations, no HTTP templates) and A8 (same operation) | User answers |
| 2026-11-09 | A6: no auth/admin role; R4 management is API-only (R4.AC5–AC6), management UI out of scope; tightened R8/R9 ACs (A5, A7, A9); NFR3 prerequisites | User answers, self-check |
| 2026-11-09 | Added R12 (Should): management screen as a separate view in the same React app; build order updated | User decision — "without development" implies a non-developer, and reviewers judge from the UI |
| 2026-11-09 | Spec approved (weather stays Must) | User approval in conversation |
| 2026-11-09 | Added R4.AC7 and R12.AC8: concurrent edits are detected at save time (optimistic concurrency), never silently overwritten; status back to Draft | User decision during plan grilling — "last save wins" could silently lose changes; a real lock needs user identity and stale-lock handling |
| 2026-11-09 | Spec re-approved | User approval in conversation |
| 2026-11-09 | Added R13 (Should): management key for add/change/remove; A6 changed; Operator actor reworded; status back to Draft | User decision — destructive endpoints shouldn't be open; a per-call token would have to live in public browser JS |
| 2026-11-09 | Spec re-approved with R13 | User approval in conversation |
