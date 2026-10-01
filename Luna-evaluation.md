# Executive summary

**Opus ranks first overall at 22.8/30**, with the strongest backend evidence, most convincing automated tests and a capable end-to-end UI. **Luna has the strongest design at 8.0/10**, but its participant schedule stayed stale after a cancellation, and its tests provide limited evidence for concurrency and idempotency. Sol is a simpler, generally usable implementation with weaker test evidence. Sonnet’s backend is substantially more complete than its interface: the UI did not expose the required workshop selection, registration, roster or cancellation workflows.

I completed the browser evaluation before the source review. All four backend builds, frontend builds and backend test suites passed. The browser flows also worked for several core behaviors, including registration, cancellation, promotion and schedule-conflict handling. Passing builds and tests do not establish that every requirement is implemented correctly.

The Phase 1 URL list gives Luna as port **8200**, while the later instructions give **5200**. Port 5200 refused the connection, so I evaluated the live Luna instance at **8200**.

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| -------------- | -------: | ---------------: | ---------: | --------: |
| Sonnet         | 7.8      | 5.0              | 4.2        | 17.0      |
| Opus           | 8.8      | 9.0              | 5.0        | 22.8      |
| Sol            | 6.8      | 7.5              | 7.0        | 21.3      |
| Luna           | 6.7      | 7.7              | 8.0        | 22.4      |

Design is the rounded average of Visual Design, Responsive Design and Universal Design. Total is Code + Requirements + Design.

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| Sonnet         | 5.2        | 3.0            | 4.5                  | 4.2                |
| Opus           | 7.0        | 4.0            | 4.0                  | 5.0                |
| Sol            | 7.3        | 6.5            | 7.3                  | 7.0                |
| Luna           | 8.8        | 8.2            | 7.1                  | 8.0                |

## Method and evidence notes

I tested each live application in a separate browser session where practical, using desktop, tablet and mobile viewport sizes. I exercised registration, cancellation, waitlist promotion, schedule views and conflicting registrations where the UI supported them. I also checked keyboard interaction, labels, visible focus, status feedback, layout overflow and browser errors.

A GPT-6 Astra sub-agent reviewed all four codebases after the browser assessment and was given the assignment and build/test results. It made no code changes. I assessed its findings against the runtime evidence. A key point in that review is that the Microsoft.Data.Sqlite provider’s default serializable transaction path uses `BEGIN IMMEDIATE`, which serializes SQLite writers. This supports transactional capacity checks across implementations that cover the read/check/write operation in a transaction. [Microsoft.Data.Sqlite connection source](https://raw.githubusercontent.com/dotnet/efcore/v9.0.0/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs), [transaction source](https://raw.githubusercontent.com/dotnet/efcore/v9.0.0/src/Microsoft.Data.Sqlite.Core/SqliteTransaction.cs). Opus makes that strategy explicit in its service; several others rely more on provider transaction behavior, so their approach is less obvious from a quick code read.

---

# Sonnet

## Browser findings

The workshop table loaded, and the participant schedule control worked: selecting Alice showed “No registrations.” The interface did not expose workshop selection, registration, confirmed registrations, a waitlist or cancellation. Those missing workflows are the largest reason Sonnet’s requirement score is low, despite its more complete backend.

The table showed workshop information and capacity in a plain, readable format. No console errors or warnings were observed, and I saw no failed network requests during the checks performed.

## Responsive Design

At desktop and tablet sizes, the table fit without page-level horizontal scrolling. At 390 × 844, the document width was about 476 px, with the table about 444 px wide. Remaining-capacity and waitlist columns extended beyond the viewport, and wrapped text made rows unusually tall. This makes the main information awkward to use on mobile.

## Universal Design

The page has a sensible heading hierarchy and a semantic table. The schedule control is keyboard-operable with native focus behavior, and capacity/status information is conveyed with text. The participant select did not have an explicit label, and the page lacked main/header landmarks. Since the principal registration workflow is absent, I could not assess its keyboard or screen-reader behavior.

## Build and automated tests

- **Backend build:** Passed, no warnings or errors.
- **Backend tests:** 26/26 passed.
- **Frontend build:** Passed.
- **Test quality:** The suite covers the required business scenarios, including file-backed SQLite capacity tests, idempotency, FIFO promotion, conflict skipping and adjacent workshop times. It does not establish concurrent cancellation behavior or frontend state consistency.

## Astra code-review findings

- **Confirmed defects:** The UI does not offer the required registration, cancellation, workshop detail or roster workflows. The source also has stale asynchronous selection responses that can overwrite newer selections. Created-resource `Location` URLs point to routes without matching GET handlers. Relevant source: [App.tsx](/Users/oddbjornmidbo/Source/App-test/Sonnet/frontend/src/App.tsx:41) and [ApiEndpoints.cs](/Users/oddbjornmidbo/Source/App-test/Sonnet/src/SeatFlow.Api/Endpoints/ApiEndpoints.cs:51).
- **Likely risks:** Register and cancel actions lack in-flight protection. Participant email uniqueness is checked outside the transaction, leaving a possible race.
- **Missing requirements:** The backend review found no major missing business rule, but the browser evaluation confirms the frontend omits several required user workflows.
- **Test-quality observations:** 26 meaningful tests, including file-backed concurrency and idempotency cases. No synchronized concurrent-cancellation test or frontend stale-response test.
- **Architecture observations:** Clear separation among domain, data, service, endpoints and notification code.
- **Strengths:** Transactional domain logic, post-commit notification error isolation, and a broad business test suite.
- **Astra recommended code score:** 8.2/10. My 7.8 reflects the weak frontend implementation and the incomplete user-facing feature.

## Important defects

**Observed UI defect:** Users cannot complete registration or cancellation through the interface, or inspect a selected workshop’s roster. This is missing functionality, not merely a presentation issue.

**Source-level risk:** In-flight workshop responses are not guarded against a more recent selection. The risk is visible in the frontend source but was not independently reproduced in the limited UI.

## Strengths

The backend build and test evidence is strong, and the architecture keeps domain behavior outside the API layer. The mobile weakness and missing user workflows are concentrated in the frontend rather than the core registration service.

## Scores

- Code: 7.8/10
- Requirements: 5.0/10
- Visual Design: 5.2/10
- Responsive Design: 3.0/10
- Universal Design: 4.5/10
- Design average: 4.2/10
- Total: 17.0/30

---

# Opus

## Browser findings

Opus loaded workshops and showed a useful detail view after selecting one. I registered Ada into Intro to EF Core, then attempted to register her in the overlapping Async C# Deep Dive workshop. The UI displayed a clear conflict message. I then registered Alan and Grace to fill the workshop, added Ken and Linus to the waitlist, and cancelled Ada. Ken was promoted as the oldest eligible waitlisted participant, while Linus remained waitlisted. The counts and status panels updated, and the cancelled registration remained visible.

The UI did not offer a second-cancellation action, so I could not test repeat cancellation from the browser. Mobile registration and cancellation worked on another workshop. The notifications panel also showed registration and cancellation events.

## Responsive Design

The desktop layout is tidy and readable, but the detail panel starts blank until a workshop is selected. At tablet width the layout stacks, with considerable unused space to the right. At 390 × 844, the document was about 462 px wide and the table about 430 px wide. The workshop table’s columns became cramped, with text wrapping and some information extending past the viewport.

## Universal Design

The page uses headings and native table structure, and statuses are written as text. However, selecting a workshop is a confirmed keyboard barrier: the clickable table rows use `tabIndex=-1` and have no button or row-selection semantics. Pressing Enter on a row did not select it. The registration and schedule selects lack explicit labels, and the mutation message region lacks live-region semantics, so screen readers may not announce updates reliably.

## Build and automated tests

- **Backend build:** Passed, no warnings or errors.
- **Backend tests:** 34/34 passed.
- **Frontend build:** Passed.
- **Test quality:** Strongest of the four suites. It includes barrier-started, independent-context SQLite concurrency tests for final-seat contention and same-key retries, plus duplicate registration, cancellation concurrency, FIFO promotion, overlap boundaries and notification failure.

## Astra code-review findings

- **Confirmed defects:** Workshop selection has the keyboard issue described above. The source also has stale asynchronous selection responses that can populate the wrong workshop detail, potentially leaving an incorrect registration cancellable. Workshop creation returns a `Location` for an unmapped GET route. Relevant source: [App.tsx](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:35) and [SeatFlowEndpoints.cs](/Users/oddbjornmidbo/Source/App-test/Opus/backend/src/SeatFlow.Api/Endpoints/SeatFlowEndpoints.cs:20).
- **Likely risks:** A workshop-creation closure can trigger refreshes using stale selection state; forms clear on error; participant schedules use per-row queries; SQLite busy handling is not explicit.
- **Missing requirements:** No major business requirement was identified as missing. The browser checks confirmed the core workflows.
- **Test-quality observations:** 34 tests with realistic parallel callers and separate database contexts provide the best evidence for concurrency. Tests cover repeat cancellation and notification failure.
- **Architecture observations:** Cleanest overall separation, typed response models, centralized error handling and explicit SQLite transaction strategy.
- **Strengths:** Most convincing backend correctness evidence; successful UI promotion and conflict flows; notifications are isolated from committed domain operations.
- **Astra recommended code score:** 8.8/10, which I retained.

## Important defects

**Confirmed accessibility defect:** Workshop rows are mouse-clickable but not keyboard-selectable. The narrow mobile table is also a responsive defect. Stale selection responses are a source-confirmed UI race risk, though I did not trigger it during normal use.

## Strengths

Opus combines the best backend and test evidence with a working UI that exposes the main assignment workflows. Its service explicitly uses `BEGIN IMMEDIATE`, and the tests exercise SQLite concurrency with independent contexts rather than relying only on in-memory scheduling.

## Scores

- Code: 8.8/10
- Requirements: 9.0/10
- Visual Design: 7.0/10
- Responsive Design: 4.0/10
- Universal Design: 4.0/10
- Design average: 5.0/10
- Total: 22.8/30

---

# Sol

## Browser findings

Sol’s workshop cards, selected-workshop registration form, confirmed list, waitlist, cancelled list and participant schedule were all visible. I registered Jordan into a workshop with one remaining seat, then registered Casey and Sam to the waitlist. I registered Casey into an overlapping workshop and confirmed the registration there. When I cancelled Alex from the first workshop, Casey was skipped due to the schedule conflict and Sam was promoted. I then attempted a duplicate registration for Jordan and received an “already registered” message.

After cancellation, Alex’s schedule briefly showed the old confirmed workshop; on a later refresh it showed no confirmed workshops. This suggests a transient stale-state issue, though I did not establish a persistent schedule defect. Mobile registration and cancellation also worked. No console errors or failed requests were observed.

## Responsive Design

Tablet cards reflowed to a readable two-by-two grid without horizontal overflow. On mobile, cards stacked clearly, but the page was about 435 px wide at a 390 px viewport. The participant selector and Register button row pushed past the viewport by about 45 px. The controls remained usable, but required slight sideways scrolling.

## Universal Design

Sol has a main landmark, sensible heading levels, labelled controls, keyboard-operable card buttons and visible focus. Status messages use text and a status role, so state is not communicated by color alone. The main concern is mobile overflow; the form controls and buttons are just under a comfortable 44 px touch target. The mutation message handling is clearer than in Opus.

## Build and automated tests

- **Backend build:** Passed with one `NU1900` warning because the NuGet vulnerability-data feed could not be reached.
- **Backend tests:** 9/9 passed.
- **Frontend build:** Passed.
- **Test quality:** Covers a number of business scenarios, but concurrency tests use `Task.WhenAll` without a start barrier or independent file-backed database contexts. Several cases use the same tracked context. There are no HTTP integration tests, adjacent-time boundary test, or robust cancellation-with-promotion repeat test.

## Astra code-review findings

- **Confirmed defects:** Refresh failures are caught and can be replaced by a success message. Workshop selection has a stale-response race. Workshop creation lacks required start-time validation, and the created-resource URL points to an unmapped GET route. Relevant source: [App.tsx](/Users/oddbjornmidbo/Source/App-test/Sol/frontend/src/App.tsx:32) and [Program.cs](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/Program.cs:20).
- **Likely risks:** Notifications are sent while holding a global booking semaphore, so a slow notification service can block unrelated booking work. The idempotency key is scoped per workshop; this is not inherently a violation, but differs from global key handling. Error handling can return broad 409 responses and expose inner database messages.
- **Missing requirements:** Core registration behavior is present, but the source review found a workshop start-time validation gap.
- **Test-quality observations:** Nine tests pass, but the concurrency test setup does not convincingly prove true simultaneous independent database requests.
- **Architecture observations:** Simple, compact service code. Some endpoints, validation and seeding logic are compressed into long lines, which makes maintenance harder.
- **Strengths:** Usable end-to-end UI, labelled controls, visible focus and accurate waitlist skip/promotion behavior in the browser.
- **Astra recommended code score:** 6.8/10, which I retained.

## Important defects

**Observed responsive defect:** The mobile form row exceeds the viewport.

**Source-confirmed defect:** A failed refresh can still leave the user with a success message. This undermines trust in the state displayed after mutations.

**Test evidence gap:** The “concurrency” tests are weaker than their names suggest because they do not establish a synchronized contest among independent SQLite connections.

## Strengths

Sol is the simplest full workflow after Opus. Its card layout and keyboard behavior are easy to understand, and the waitlist conflict-skip flow worked correctly in the browser.

## Scores

- Code: 6.8/10
- Requirements: 7.5/10
- Visual Design: 7.3/10
- Responsive Design: 6.5/10
- Universal Design: 7.3/10
- Design average: 7.0/10
- Total: 21.3/30

---

# Luna

## Browser findings

Luna has the most polished interface: a workshop sidebar, selected workshop detail and participant schedule area. I registered Avery, Casey and Jordan until Designing Better APIs was full, then added Morgan and Riley to its waitlist. Practical TypeScript already had Morgan and Riley confirmed and overlapped with the first workshop. I cancelled Morgan from Practical TypeScript, then confirmed that Avery’s attempt to register there was rejected because of the schedule conflict.

I also attempted a duplicate registration for Riley and got an active-registration error. After cancelling Avery from Designing Better APIs, Morgan was promoted because Morgan no longer had the conflict; Riley remained waitlisted. Counts and roster states updated correctly. **The participant schedule panel remained stale after Avery’s cancellation**, still showing the cancelled workshop as confirmed. This was directly observed.

Mobile registration and cancellation worked on another workshop. No console errors or failed requests were observed.

## Responsive Design

Luna’s three-column desktop layout is spacious and cohesive. At tablet size it reflows into a workshop list with detail and schedule panels below or beside it, with no horizontal overflow. At 390 × 844, content stacks vertically, fits the viewport width and remained usable with scrolling. The cancellation buttons are very short, around 19 px tall, which makes them difficult touch targets.

## Universal Design

Luna provides useful landmarks, labelled participant and schedule controls, native buttons, visible focus and mutation feedback with a status role. Status labels use text. The strongest concerns are the small, muted secondary text, very small cancel targets and repeated cancel buttons whose accessible names are just “Cancel.” The directly observed stale schedule can also mislead someone using assistive technology or relying on the schedule panel.

## Build and automated tests

- **Backend build:** Passed, no warnings or errors.
- **Backend tests:** 12/12 passed.
- **Frontend build:** Passed.
- **Test quality:** Promotion and conflict-skip tests provide useful business-state evidence. The final-seat test uses asynchronous calls on a shared in-memory connection without a barrier or separate file-backed connections. Same-key concurrency, back-to-back time boundaries, HTTP behavior and repeat cancellation with waitlist promotion are not convincingly covered.

## Astra code-review findings

- **Confirmed defects:** The schedule refresh depends on participant selection but does not refresh after registration, cancellation or promotion. The browser test reproduced this after cancellation. The selection UI also has a stale-response race. Workshop time validation does not require a real start timestamp, and registration creation returns a URL with no matching GET route. Relevant source: [App.tsx](/Users/oddbjornmidbo/Source/App-test/Luna/frontend/src/App.tsx:69) and [ApiContracts.cs](/Users/oddbjornmidbo/Source/App-test/Luna/backend/SeatFlow.Api/Contracts/ApiContracts.cs:5).
- **Likely risks:** A post-commit reload can fail outside the notification error handler, resulting in a failed response after a successful write and possibly skipped notifications. There is no unique active-registration backstop. Requiring an idempotency key for every registration is restrictive, although it does honor supplied keys.
- **Missing requirements:** No major workflow was missing in the browser, but start-time validation is incomplete.
- **Test-quality observations:** Twelve tests pass; promotion and skip cases are useful. The concurrency and idempotency evidence is substantially weaker than Opus and Sonnet’s.
- **Architecture observations:** Simple, readable organization across contracts, data, domain and service. Some extra reload work contributes to fragile post-commit behavior.
- **Strengths:** Best visual and responsive design; usable complete workflow; the conflict skip and promotion behavior worked in the browser.
- **Astra recommended code score:** 6.7/10, which I retained.

## Important defects

**Observed UI defect:** The schedule panel can show a cancelled registration as confirmed until the participant is reselected or the page is otherwise refreshed.

**Source-confirmed validation issue:** Workshop start-time validation can accept a missing start time represented by the default `DateTime`.

## Strengths

Luna is the most polished and mobile-friendly interface. Its primary workflows are exposed clearly, and the browser checks confirmed conflict rejection, duplicate rejection, cancellation and promotion.

## Scores

- Code: 6.7/10
- Requirements: 7.7/10
- Visual Design: 8.8/10
- Responsive Design: 8.2/10
- Universal Design: 7.1/10
- Design average: 8.0/10
- Total: 22.4/30

---

# Cross-model comparison

## Functional correctness and waitlist behavior

Opus, Sol and Luna all exposed the key workflows and passed browser checks for registration and cancellation. Sol and Luna both demonstrated the important skip-conflicting-participant behavior directly: the oldest ineligible participant remained waitlisted, while the first eligible participant was promoted. Opus demonstrated FIFO promotion and schedule-conflict rejection. Sonnet’s backend implements the core domain, but the browser UI does not let a user exercise those workflows.

## Concurrency, idempotency and transactions

Astra found transactional capacity checks, cancellation and promotion in all four backends. In the EF Core SQLite provider, the default serializable transaction path uses `BEGIN IMMEDIATE`, so transactional read/check/write operations can serialize across processes rather than relying only on an in-process semaphore. [Microsoft.Data.Sqlite connection source](https://raw.githubusercontent.com/dotnet/efcore/v9.0.0/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs), [transaction source](https://raw.githubusercontent.com/dotnet/efcore/v9.0.0/src/Microsoft.Data.Sqlite.Core/SqliteTransaction.cs).

Opus provides the clearest implementation and strongest evidence for this behavior: it starts explicit immediate transactions and has tests using synchronized parallel requests with independent contexts. Sonnet also has meaningful file-backed tests, though without a start barrier and without concurrent cancellation coverage. Sol and Luna’s concurrency tests do not strongly establish simultaneous contention across independent database connections.

The code review found persisted idempotency keys in all four. Sol scopes keys per workshop, a reasonable endpoint-level interpretation rather than a definite defect. Opus has the strongest retry and same-key concurrency evidence; Luna’s tests do not provide equivalent proof.

## Scheduling, notifications and API quality

All four use half-open schedule overlap logic, so workshops that meet at the exact boundary should not conflict. Sonnet and Opus explicitly test that boundary; Sol and Luna do not.

All four have notification abstractions and isolate notification failures from committed operations. Sol sends notifications while holding a global booking semaphore, which creates an availability risk if notification delivery is slow. Luna can fail during post-commit reload outside the notification handler, creating a risk of a failed response after the write has succeeded.

Each implementation has at least one created-resource `Location` URL that does not map to a GET route. Sonnet, Sol and Luna have that issue on a resource creation route; Opus has it for workshop creation, while its registration location resolves.

## Frontend engineering and runtime reliability

The most significant common source-level concern in the full-featured frontends is an unguarded stale-response race: a slower response for a previously selected workshop can replace details for the current selection. Astra found this in all four frontends, though Sonnet does not expose workshop selection in its current UI. This race was not directly reproduced during the browser checks. Luna’s stale participant schedule, by contrast, was reproduced after cancellation.

Sol’s UI can display success after a refresh failure. Opus’s create forms clear fields on error, which can make recovery inconvenient. Luna disables actions during some requests and has typed client/error handling, but does not refresh the schedule after mutations. No console errors were observed in the tested flows.

## Automated-test quality

| Implementation | Backend tests | Assessment |
| -------------- | ------------: | ---------- |
| Sonnet         | 26/26 passed  | Broad business coverage and file-backed concurrency cases; no synchronized concurrent-cancellation test. |
| Opus           | 34/34 passed  | Strongest evidence: barriers, independent SQLite contexts, idempotency concurrency, cancellation concurrency and HTTP coverage. |
| Sol            | 9/9 passed    | Useful basic scenarios, but concurrency setup is weak and boundary/HTTP coverage is limited. |
| Luna           | 12/12 passed  | Useful promotion and skip cases; concurrency and same-key idempotency tests are limited. |

A test count by itself would not justify this comparison. The main difference is whether tests create genuinely concurrent calls against independent database contexts and whether they check resulting persistent state.

## Architecture and maintainability

Opus has the clearest separation and the most complete tests. Sonnet also has a clean separation of domain, data, services and endpoints, although that strength is not matched by its interface. Sol is deliberately compact, but compressed endpoint and seed code is harder to maintain. Luna’s overall organization is reasonable; its extra post-commit reload path adds a failure point.

## Design comparison

Luna is visually strongest and handles mobile layout best. Sol is clear and straightforward but has a small mobile overflow. Opus’s layout is orderly on desktop but the table is cramped on mobile and its selection interaction is not keyboard-accessible. Sonnet is the weakest mobile experience because its table extends beyond the viewport.

Sol has the strongest practical Universal Design score: labelled controls, a main landmark, keyboard-operable cards and visible status feedback. Luna is close, but its small cancel targets and repeated button names are concerns. Opus has the clearest keyboard barrier.

---

# Final ranking

1. **Opus — 22.8/30.** Best combined result: strong backend design, the most convincing concurrency and idempotency tests, and working registration, conflict and promotion flows. Its keyboard-only workshop selection and mobile table are substantial weaknesses.

2. **Luna — 22.4/30.** Best design and mobile layout, with working browser flows. Its stale schedule after cancellation and weak concurrency/idempotency evidence keep it just behind Opus.

3. **Sol — 21.3/30.** A simple, understandable application with good keyboard basics and correct conflict-skip promotion in the browser. Its concurrency tests are weak, and the mobile registration row overflows.

4. **Sonnet — 17.0/30.** A sound backend and 26 passing tests, but the UI omits most required user workflows and its workshop table is difficult to use on mobile.

## Category winners

- **Best backend/domain implementation:** Opus, narrowly; all four use transactional logic, but Opus makes the strategy clearest and tests it most convincingly.
- **Best automated tests:** Opus.
- **Best frontend engineering:** Opus, narrowly; its core UI refreshes after mutations, while Luna’s schedule becomes stale.
- **Best architecture:** Opus.
- **Best visual design:** Luna.
- **Best responsive design:** Luna.
- **Best Universal Design / accessibility:** Sol.
- **Best mobile experience:** Luna; it fits the viewport and preserves the main workflows, though its cancel targets are too small.
- **Best runtime robustness:** Opus, based on explicit transaction handling and the strongest independent-database concurrency tests.
- **Most serious directly observed hidden bug:** Luna’s schedule continued to show a cancelled registration as confirmed. Across the codebases, an unguarded stale selection response is another source-confirmed risk that could show the wrong roster.
- **Most polished overall application:** Luna.
- **Simplest implementation that still satisfies the task:** Sol, with the caveat that its concurrency evidence and endpoint formatting are weaker than Opus’s.

All backend builds, frontend builds and backend test suites passed. Sol’s backend build emitted one `NU1900` warning because the vulnerability-data feed was unreachable; the other backend builds had no reported warnings or errors. I used .NET 9 and the bundled current Node runtime for the builds.

The UI evaluation created and cancelled registrations in the running development applications, so their persisted local data changed. I did not reset those databases. **No implementation source files were modified.**
