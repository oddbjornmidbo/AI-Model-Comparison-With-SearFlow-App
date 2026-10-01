# SeatFlow evaluation

## Executive summary

**Opus ranks first, narrowly ahead of Luna.** Opus has the strongest evidence for race-safe seat allocation, the most convincing automated tests, and consistent behavior through the browser workflow. Luna has the clearest and most responsive interface, but I confirmed a schedule refresh bug: after a registration, the roster updates while the selected participant’s schedule remains stale until reselected.

Sonnet is a solid, compact implementation with strong business tests, held back mainly by mobile and keyboard usability. Sol completes the core flows, but its concurrency test provides weak race evidence, its API validation is thinner, and its Register control is clipped on mobile.

I tested the four supplied URLs in separate browser tabs at desktop, tablet, and mobile sizes **before** reading source code. Their existing demo databases had different starting registrations, so I used equivalent available-seat, full-seat, conflict, cancellation, and schedule actions rather than identical participant names. All four backend builds, frontend builds, and backend test suites passed with .NET 9 and Node 22. No unexpected browser console errors appeared during the tested flows. HTTP key races and final-seat safety were judged from tests and source, not inferred from sequential UI clicks. Browser zoom shortcuts did not change the in-app browser’s zoom, so zoom behavior is not directly verified.

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| -------------- | -------: | ---------------: | ---------: | --------: |
| Sonnet | 8.2 | 8.8 | 4.6 | 21.6 |
| Opus | 8.9 | 9.2 | 5.1 | **23.2** |
| Sol | 7.0 | 7.7 | 5.9 | 20.6 |
| Luna | 7.1 | 8.2 | 7.5 | 22.8 |

Design is the average of the three scores below, rounded to one decimal place.

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| Sonnet | 6.4 | 4.4 | 3.0 | 4.6 |
| Opus | 7.2 | 5.0 | 3.2 | 5.1 |
| Sol | 6.4 | 5.0 | 6.3 | 5.9 |
| Luna | 8.4 | 8.3 | 5.7 | 7.5 |

# Sonnet

## Browser findings

Workshops, remaining seats, confirmed registrations, the ordered waitlist, cancelled registrations, and participant schedules loaded. I registered Alice into an available one-seat workshop, received a clear duplicate error on a second attempt, waitlisted Bob after it filled, then cancelled Alice; Bob was promoted and the workshop count updated. A later attempt to confirm Bob in an overlapping workshop was rejected. The schedule reflected registration and cancellation changes. I also completed a registration from the mobile layout.

## Responsive Design

The desktop page is clear, and the tablet page remains usable. At 390 px, the five-column workshop table made the document **476 px wide**: the waitlist column moved offscreen and dates wrapped into narrow vertical blocks. Registration remained reachable below the table, but navigating the workshop list was awkward. Native form and Cancel controls were about 22 px high.

## Universal Design

Headings, lists, written status labels, an error alert, and visible browser focus are strengths. **Confirmed accessibility defect:** workshop selection is attached to a table row with no keyboard-focusable control, so a keyboard-only user cannot select a workshop and reach its registration flow. Both participant selects lack programmatic labels, and the error’s “×” dismissal has no descriptive name. See the [workshop row and form](/Users/oddbjornmidbo/Source/App-test/Sonnet/frontend/src/App.tsx:94).

## Build and automated tests

Backend build: **pass, zero warnings**. Frontend build: **pass**. Backend tests: **26 passed, 0 failed**. The suite checks FIFO promotion, conflict skipping, touching time boundaries, duplicate and keyed replay behavior, double cancellation, notification failure, and concurrent calls. Its races use fresh contexts but share the application’s static gate, so they do not independently stress database lock contention. The README covers the requested run instructions, architecture, assumptions, and limitations.

## Astra code-review findings

Registration, idempotency records, cancellation, and promotion are grouped into appropriate transactions; the service owns the business rules and database indexes back up active-registration and key uniqueness ([service](/Users/oddbjornmidbo/Source/App-test/Sonnet/src/SeatFlow.Api/Services/RegistrationService.cs:25), [indexes](/Users/oddbjornmidbo/Source/App-test/Sonnet/src/SeatFlow.Api/Data/SeatFlowDbContext.cs:25)). Astra found no demonstrated core backend rule failure. It identified a **likely frontend race**: older workshop or schedule requests can finish after a newer selection and overwrite its data ([refresh logic](/Users/oddbjornmidbo/Source/App-test/Sonnet/frontend/src/App.tsx:41)). Notification delivery is correctly isolated from committed changes, though it has no durable retry mechanism, which the assignment did not require.

## Important defects

- **Observed responsive defect:** horizontal overflow and hard-to-read workshop rows at 390 px.
- **Confirmed accessibility defect:** mouse-only workshop rows and unlabeled selects.
- **Likely state risk:** out-of-order fetch responses can show an older roster under a newer selection. I did not reproduce that timing race in the browser.

## Strengths

Clear domain separation, atomic state changes, meaningful tests, sensible API responses, and consistent updates in the exercised browser flow. It is the simplest implementation here that still covers the assignment well.

## Scores

- Code: 8.2/10
- Requirements: 8.8/10
- Visual Design: 6.4/10
- Responsive Design: 4.4/10
- Universal Design: 3.0/10
- Design average: 4.6/10
- Total: 21.6/30

# Opus

## Browser findings

Workshops and counts loaded, with confirmed, waitlisted, and cancelled rows clearly separated. Registering Alan into an available overlapping workshop produced a specific conflict message. Ada and Margaret filled that workshop; Linus joined its waitlist. Cancelling Ada promoted Linus, updated the counts and roster, and added both events to the development notification log. Linus’s schedule showed his confirmed bookings and a waitlisted booking elsewhere. Already registered participants were disabled in the registration selector. A mobile registration also succeeded.

## Responsive Design

The two-column desktop composition uses space well, and the tablet layout stacks its sections without overflow. At 390 px, the workshop table made the document **463 px wide**; its final column required horizontal movement. Registration and cancellation still worked, but the table and long notification log made mobile use cumbersome. Several controls were about 22 px high.

## Universal Design

Status badges include text, headings and data tables are structured sensibly, and keyboard focus is visible. **Confirmed accessibility defect:** selecting a workshop requires clicking a table row that cannot receive keyboard focus ([row implementation](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:178)). The registration and schedule selects have no programmatic labels. The success/error notice is a clickable `div` without an alert or status role, so dismissal and announcement are weak for keyboard and assistive-technology users.

## Build and automated tests

Backend build: **pass, zero warnings**. Frontend build: **pass**. Backend tests: **34 passed, 0 failed**. These are the strongest tests in the comparison: a start barrier releases tasks using separate database contexts and connections, covering final-seat, same-key, duplicate, and simultaneous-cancellation races ([concurrency tests](/Users/oddbjornmidbo/Source/App-test/Opus/backend/tests/SeatFlow.Tests/ConcurrencyTests.cs:14)). API tests also check HTTP behavior. The README covers all requested topics.

## Astra code-review findings

Opus explicitly takes a SQLite write transaction **before** its capacity, duplicate, and key reads ([transaction entry](/Users/oddbjornmidbo/Source/App-test/Opus/backend/src/SeatFlow.Api/Services/RegistrationService.cs:181)). Registration and cancellation/promotion are atomic; overlap checks use half-open boundaries; keys and active registrations have unique-index protection. It also reconsiders waitlists elsewhere when cancellation removes a participant’s conflict. Astra found no confirmed core backend defect. As in Sonnet, unguarded frontend fetches create a **likely** stale-selection race, including a path after workshop creation ([refresh and creation flow](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:35)). Per-item waitlist-position queries are a minor scale concern for this small app.

## Important defects

- **Confirmed accessibility defect:** mouse-only workshop selection and unlabeled selects.
- **Observed responsive defect:** horizontal table overflow on mobile.
- **Likely state risk:** an older asynchronous refresh can overwrite data for a newer selection; this was not observed during the tested interactions.

## Strengths

The best evidenced backend correctness, test quality, API detail, notification visibility, and runtime state consistency. Its added structure remains proportionate to the task.

## Scores

- Code: 8.9/10
- Requirements: 9.2/10
- Visual Design: 7.2/10
- Responsive Design: 5.0/10
- Universal Design: 3.2/10
- Design average: 5.1/10
- Total: 23.2/30

# Sol

## Browser findings

Workshop cards and remaining seats loaded. Jordan’s attempt at an overlapping confirmation was rejected. Alex and Taylor took the remaining seats in Practical TypeScript; a duplicate attempt was rejected and Jordan joined the waitlist. Cancelling Alex left a free seat while the conflicted Jordan correctly remained waitlisted. After Casey’s conflicting booking was cancelled, cancelling a confirmed seat in the other workshop promoted Casey. Counts and selected-workshop rows followed those changes. The schedule shows confirmed bookings and shares its participant selection with the registration form.

## Responsive Design

Workshop cards stack sensibly on mobile and are easy to select. The registration form does not reflow fully: at 390 px the document was **435 px wide**, and part of the Register button sat beyond the visible edge. The long date strings include seconds, which increases wrapping. Tablet layout remained usable.

## Universal Design

Native button cards make workshop selection keyboard accessible; form controls have visible labels, status words accompany color, and focus is visible. These give Sol the narrow lead for practical accessibility. The clipped mobile Register control remains an inclusive-use barrier, and success/error feedback is not tied to a specific form control.

## Build and automated tests

Backend build: **pass**. Frontend build: **pass**. Backend tests: **9 passed, 0 failed**. Backend build/test output included `NU1900`, caused by an unreachable NuGet vulnerability feed; it was not a compilation or test failure. The suite covers the named business scenarios, but its “concurrent” test passes two calls directly to `Task.WhenAll` without scheduling or a start barrier; they may execute sequentially ([test](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Tests/RegistrationTests.cs:78)). Several assertions use already-tracked entities rather than rereading persistence. The README contains the required sections.

## Astra code-review findings

The core service begins a transaction before checking seats, uses deterministic FIFO order, skips conflicts, and catches notification errors after commit ([service](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/RegistrationService.cs:15)). Its concise API has weaker validation and error mapping. The workshop endpoint does not explicitly require both time fields; omitted values can reach default-value handling ([API request and validation](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/Program.cs:20)). A successful mutation followed by a failed refresh can still display “Registration saved” because refresh errors are swallowed before the success message is set ([frontend update](/Users/oddbjornmidbo/Source/App-test/Sol/frontend/src/App.tsx:26)). Keys are persisted per workshop rather than globally; the assignment does not define cross-workshop key scope, so that is an assumption to document, not a proven violation.

## Important defects

- **Observed responsive defect:** the mobile Register control is partly clipped.
- **Confirmed code defect:** refresh failure can leave stale data beside a success message.
- **Source-based API validation gap:** missing workshop start/end values are not explicitly rejected.
- **Test limitation:** the passing final-seat test does not establish that its requests actually contend.

## Strengths

A small, workable domain service; accessible workshop buttons; working conflict skipping in the browser; and all required core routes and UI actions.

## Scores

- Code: 7.0/10
- Requirements: 7.7/10
- Visual Design: 6.4/10
- Responsive Design: 5.0/10
- Universal Design: 6.3/10
- Design average: 5.9/10
- Total: 20.6/30

# Luna

## Browser findings

The initial roster, waitlist, cancelled registrations, and remaining seats were clear. Casey’s overlapping confirmation was rejected. Avery filled Practical TypeScript, a duplicate was rejected, Taylor joined the waitlist, and cancelling Avery promoted Taylor. Counts and roster refreshed. Registration also worked on mobile.

**Observed UI bug:** with Jordan selected in the schedule, I registered Jordan into Product Discovery Lab. The workshop count and roster immediately showed the confirmation, but Jordan’s schedule still showed only his earlier two workshops. A second browser read showed no change. Switching the schedule selector to Taylor and back to Jordan then displayed the third workshop.

## Responsive Design

The desktop three-column layout is cohesive. At tablet size the schedule moves below the main panels; at 390 px the cards and panels form a usable single column with **no horizontal document overflow**. The form and roster remained operable on mobile. Text and actions are unusually small, however. A separate 320 px narrow-reflow stress check produced 54 px of overflow; that check is not a substitute for an actual browser-zoom test.

## Universal Design

Luna uses labeled selects, button-based workshop cards, meaningful headings, written statuses, a live status notice, and visible focus. Its main weakness is readability and motor access: roster secondary text and Cancel labels measured about **9 px**, status pills about **8 px**, and Cancel targets about **38 × 19 px**. Sample gray text on white measured roughly **2.5–3.1:1** contrast. These are concrete low-vision and touch-use concerns despite the strong page structure.

## Build and automated tests

Backend build: **pass, zero warnings**. Frontend build: **pass**. Backend tests: **12 passed, 0 failed**. FIFO, skip, replay, duplicate, and repeat cancellation have meaningful assertions. Its final-seat test creates calls against one service and one shared in-memory SQLite connection without a start barrier or independent connection contention, so a pass does **not** prove a race was exercised ([test setup and race](/Users/oddbjornmidbo/Source/App-test/Luna/backend/SeatFlow.Tests/RegistrationServiceTests.cs:145)). The README covers the requested topics.

## Astra code-review findings

The service keeps registration plus its persisted idempotency record in one transaction, and cancellation plus promotion in another ([service](/Users/oddbjornmidbo/Source/App-test/Luna/backend/SeatFlow.Api/Services/RegistrationService.cs:28)). The schedule bug follows directly from a fetch effect that runs only when the selected participant changes; mutation handlers reload workshops and registrations but not the schedule ([frontend effects and handlers](/Users/oddbjornmidbo/Source/App-test/Luna/frontend/src/App.tsx:69)). Post-commit entity reloads introduce a **likely risk** that a successful commit followed by a cancelled or failed read suppresses notification dispatch. Workshop time presence is not explicitly validated ([API validation](/Users/oddbjornmidbo/Source/App-test/Luna/backend/SeatFlow.Api/Program.cs:41)). Requiring an `Idempotency-Key` is stricter than the other APIs but still supports the assignment’s keyed requests.

## Important defects

- **Observed UI bug and confirmed code defect:** the selected schedule stays stale after registration; source indicates the same issue after cancellation and promotion.
- **Confirmed inclusive-use concern:** very small, low-contrast roster text and Cancel targets.
- **Test limitation:** the concurrency test does not convincingly exercise simultaneous database writers.
- **Likely robustness risk:** fallible reads after commit can prevent a notification from being attempted.

## Strengths

The strongest visual hierarchy and mobile layout, clear status presentation, readable backend organization, atomic domain transitions, and persistent global idempotency handling.

## Scores

- Code: 7.1/10
- Requirements: 8.2/10
- Visual Design: 8.4/10
- Responsive Design: 8.3/10
- Universal Design: 5.7/10
- Design average: 7.5/10
- Total: 22.8/30

# Cross-model comparison

| Area | Evidence-based comparison |
| --- | --- |
| Functional correctness | All four completed the tested available-seat, waitlist, conflict, cancellation, and promotion flows. Luna alone showed a directly observed cross-panel inconsistency: a stale selected schedule. |
| Concurrency | All start SQLite transactions before seat reads. Opus makes the immediate write lock explicit and tests coordinated requests on separate connections. Sonnet uses real concurrent tasks behind a process gate; Sol and Luna have tests that may run sequentially. A weak test is **not** proof that its implementation oversubscribes. Microsoft’s [SQLite transaction documentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions) and [provider source](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/efcore/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs) support the distinction between normal immediate transactions and explicitly deferred ones. |
| Idempotency | All persist a key or key record with registration creation and replay the registration. Opus and Sonnet test simultaneous same-key calls; Sol and Luna test sequential replay. Sol scopes keys to a workshop, a policy the assignment leaves unspecified. |
| Waitlist behavior | All sort by creation time with an ID tie-breaker and skip conflicted waiters without removing them. Opus also revisits eligible waitlists after a conflicting booking elsewhere is cancelled. |
| Schedule conflicts | All use strict, half-open overlap comparisons. Sonnet and Opus test the exact touching-time boundary; Sol and Luna implement it but lack that boundary test. |
| Transactions and notifications | All commit registration/cancellation state independently of notification success. Opus has the clearest transaction strategy; Sol holds its process gate during notification delivery, while Luna adds fallible reloads before dispatch. Durable notification delivery was not required. |
| API quality | Sonnet and Opus have stronger field validation and HTTP tests. Sol returns 200 for new registrations and broadly maps implementation exceptions to conflicts. Sol and Luna lack explicit presence checks for workshop times. |
| Frontend engineering | Opus has the strongest component and mutation-result handling; Sonnet is simpler. Sol can mask a failed refresh with success. Luna does not invalidate the selected schedule after mutations. All four have some potential for out-of-order selection fetches; those races were not reproduced. |
| Tests and maintainability | Opus’s tests supply the best independent race evidence, followed by Sonnet’s substantial behavioral coverage. Sol’s compact one-line API is harder to maintain; Luna’s extra post-commit loads complicate an otherwise readable service. |
| Visual, responsive, and Universal Design | Luna leads visual quality and 390 px reflow. Sol narrowly leads practical accessibility because its core selection controls are keyboard buttons and its text is larger. Sonnet and Opus have mouse-only workshop rows; all three non-Luna apps overflow at 390 px. |

# Final ranking

1. **Opus — 23.2/30.** Best supported correctness, tests, API behavior, and runtime consistency; keyboard access and mobile tables are its clear weaknesses.
2. **Luna — 22.8/30.** Best presentation and mobile experience, narrowly behind overall because the selected schedule can mislead users and concurrency test evidence is weak.
3. **Sonnet — 21.6/30.** Strong, relatively simple full-stack implementation with good tests; mobile table layout and keyboard barriers lower its combined score.
4. **Sol — 20.6/30.** Working core behavior and the best keyboard-operable workshop selection, but weaker validation, thinner tests, and a clipped mobile registration action.

| Category | Best implementation |
| --- | --- |
| Backend/domain implementation | **Opus** |
| Automated tests | **Opus** |
| Frontend engineering | **Opus** |
| Architecture | **Opus**, narrowly ahead of Sonnet |
| Visual design | **Luna** |
| Responsive design | **Luna** |
| Universal Design / accessibility | **Sol**, narrowly; its clipped mobile action remains a defect |
| Mobile experience | **Luna** |
| Runtime robustness | **Opus** |
| Most serious hidden bug | **Luna’s stale schedule** after a confirmed registration |
| Most polished overall application | **Luna** for interface coherence and mobile presentation; Opus is the stronger complete system |
| Simplest implementation that still satisfies the task | **Sonnet** |

The decisive differences are test credibility and state consistency. Opus provides the strongest evidence that difficult backend behavior holds under contention. Luna presents the work best but misses a required schedule update in the live interface. Sonnet delivers a sound simpler system with major keyboard and mobile costs. Sol implements the core rules, yet its passing tests establish less than their names suggest.
