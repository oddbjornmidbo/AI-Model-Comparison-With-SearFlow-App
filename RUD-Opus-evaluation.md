# RUD-Opus SeatFlow evaluation

Evaluated on 2 October 2026 against the original SeatFlow assignment. This is an independent, single-implementation evaluation of the running app at http://localhost:5174/ and the RUD-Opus folder. No implementation files were edited, and no earlier evaluation was used.

## Executive summary

RUD-Opus fulfills the SeatFlow assignment particularly well. The live UI handled final-seat registration, waitlisting, duplicate rejection, a conflicting waitlist promotion, schedule overlap rejection, and mobile registration/cancellation correctly. The backend and frontend build, and all 18 backend tests pass. Source review found a genuine SQLite write-lock strategy and persistent idempotency records. The main weakness is frontend recovery after a successful mutation followed by a failed refresh: old counts or schedule entries can remain visible without an error. A lost registration response also cannot be retried with the same UI-generated idempotency key.

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| -------------- | -------: | ---------------: | ---------: | --------: |
| RUD-Opus | 9.0 | 9.4 | 8.3 | 26.7 |

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| RUD-Opus | 8.3 | 8.5 | 8.0 | 8.3 |

Design average is (8.3 + 8.5 + 8.0) / 3, rounded to one decimal. Total is 9.0 + 9.4 + 8.3.

## Browser findings

I inspected the running app through the browser and exercised its controls. The live database already contained registrations beyond the original seed, so I used the visible starting state without resetting it. Five workshops and six participants loaded. Workshop cards showed remaining seats or full status with waitlist counts. The selected workshop showed its date, capacity, confirmed list, ordered waitlist, registration form, and collapsed cancelled history. Participant schedules showed all three statuses.

- **Observed correct behavior — conflict-aware promotion:** Practical Domain-Driven Design started full with Ada and Grace confirmed; Alan was waitlist position 1 and Margaret position 2. Alan's schedule showed a confirmed Intro to Event Sourcing session that overlaps it. Cancelling Ada promoted Margaret, retained Alan on the waitlist at position 1, kept the workshop at 2/2 confirmed, and changed the waitlist count from two to one in both the card and detail view. Ada appeared in cancelled history.
- **Observed correct behavior — available seat and waitlist:** I registered Ada into Intro to Event Sourcing's last available seat. Its card and detail changed to 3/3 confirmed and zero free seats, and her selected schedule gained the confirmed session. Registering Barbara afterward produced a waitlisted registration at position 1 and updated the workshop count.
- **Observed correct behavior — duplicate and overlap:** Attempting to register Ada into Intro again showed a specific already-registered error and did not add a row. Registering Linus for Kubernetes Without Tears, which overlaps his confirmed Testing Concurrency in .NET session, showed a specific conflict error. Kubernetes remained at 20 free seats.
- **Observed correct behavior — mobile mutation:** At mobile width, registering Ada for Accessible Front-ends in Practice changed remaining seats from three to two and updated her selected schedule. Cancelling that registration restored three free seats, removed her from the confirmed list, and showed Cancelled in both history and schedule.
- **UI feedback:** Success and error notices were understandable and dismissible. The browser console inspection recorded no errors or warnings during the tested workflows. Expected duplicate and overlap rejections were presented as business errors, not unexplained failures.

**Usability weakness:** The registration action remains enabled for a participant already registered in the selected workshop. The backend correctly rejects the duplicate, but the UI invites an avoidable invalid action. The selected participant also carries over when switching workshops, so users should check the form before submitting.

**Not tested through the UI:** HTTP idempotency-key replay, concurrent final-seat requests, and a second cancellation of an already cancelled registration. The UI removes the cancel action from cancelled entries. These behaviors were assessed through automated tests and source review rather than marked as browser failures.

## Responsive Design

I tested a 1440 × 900 desktop viewport, 768 × 1024 tablet viewport, and 390 × 844 mobile viewport, with interactions at each size. Desktop uses a workshop column beside the detail and schedule column. Tablet and mobile stack the list, detail, and schedule in a single column. Measured document width equalled viewport width at both 768 and 390 pixels; no unintended horizontal page scrolling or clipped controls appeared. The tablet workshop selection worked. Mobile registration, cancellation, cancelled-history expansion, and schedule reading worked.

The cards, form, status badges, and registration rows remained readable at mobile width. Workshop buttons were approximately 98 pixels tall. The single-column order does require scrolling past the workshop list to reach details and the schedule, and selecting an early card does not jump to its detail. This is a modest mobile navigation cost, not a functional failure. Form buttons were about 39 pixels high; they remained usable in testing but are smaller than an ideal generous touch target.

I attempted a browser zoom shortcut, but this browser surface did not change its reported zoom or viewport. Zoom behavior was therefore not directly verified. The narrow-width reflow is positive evidence, but it is not a substitute for a full zoom test.

## Universal Design

**Confirmed strengths:** The page has a main landmark, a sensible heading hierarchy, named workshop and schedule regions, labelled native select controls, semantic lists, and a definition list for workshop facts. Workshop choices are real buttons with a pressed state. Keyboard tabbing reached the controls in a usable order and showed a solid visible focus outline. Statuses use words, icons, and differing badge shapes/borders as well as color. A persistent live region announces success and error notices. Empty-form validation is connected to the participant select through an error description. The interface also defines reduced-motion behavior.

**Likely accessibility concerns:** The measured Register, Cancel, and select controls were roughly 39–40 pixels high, and the Cancelled-history summary about 24 pixels high. The summary's small hit area may be harder for some touch or motor users. The duplicate-registration action is still enabled despite the participant already being registered, adding an avoidable error path. Global API errors are announced but are not attached to the participant select, which is reasonable for a business-rule error but gives less direct form guidance.

No confirmed keyboard barrier, color-only status, inaccessible hidden action, or horizontal-overflow defect was observed. Screen-reader behavior was inferred from the DOM semantics and live-region attributes, not tested with a screen reader.

## Build and automated tests

| Check | Result |
| --- | --- |
| Backend solution build | Passed with .NET SDK 9.0.318; zero warnings and zero errors |
| Frontend production build | Passed with Node 22.22.3; TypeScript and Vite completed |
| Backend xUnit tests | 18 total, 18 passed, 0 failed |

The machine's default dotnet pointed to older SDKs and its default Node was v12, so the first build attempts could not use this project's required tooling. The documented local .NET 9 SDK and installed Node 22 resolved that environment issue without changing project files. The first solution build stalled; a serial run with build servers disabled completed successfully. The sandbox initially blocked the .NET test runner's local listener; the permitted rerun completed successfully. Expected exception and deliberately failing-notification logs appeared during negative-path tests, but the suite reported no failures.

The tests use the real HTTP pipeline and temporary SQLite files ([test fixture](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Tests/SeatFlowApp.cs:15)). They meaningfully assert the required behavior: free/full registration, FIFO promotion, keeping a conflicted person waitlisted, overlap prevention including a shared boundary, duplicate prevention, sequential and concurrent idempotency replay, cancellation twice, final-seat contention across 20 requests, and notification failure isolation ([registration tests](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Tests/RegistrationTests.cs:16)). The concurrency tests dispatch concurrently but do not force every request to meet at the exact database contention point. The source's lock strategy supplies stronger evidence than test names alone. Frontend Storybook stories are examples, not automated behavior tests; there is no frontend test suite.

## Astra code-review findings

The GPT-6 Astra sub-agent reviewed only this folder after the browser evaluation. I checked its main conclusions against the implementation and observed results.

- **Concurrency and transactions — confirmed strength:** Registration and cancellation start a nondeferred SQLite transaction before reading capacity, conflicts, or idempotency records ([RegistrationService.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:225)). The capacity check and insertion share that write transaction. Cancellation and promotion commit together. This genuinely serializes competing writers across requests and protects the final seat and cross-workshop overlap checks. The 20-request final-seat test supports this conclusion.
- **Idempotency — confirmed strength:** The key is persisted in a table with the registration ID and request identity, inside the same transaction as registration creation ([RegistrationService.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:31), [SeatFlowDbContext.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Data/SeatFlowDbContext.cs:45)). Same-key replay returns the existing registration; mismatched use is rejected. The API returns an explicit replay header. Concurrent replay has an integration test.
- **Waitlist and schedule logic — confirmed strength:** Promotion orders by creation time then ID, skips a conflicted participant without changing their waitlist status, and saves a promotion before checking later candidates ([RegistrationService.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:172)). Overlap uses half-open comparisons, so a workshop ending exactly when another begins does not conflict ([RegistrationService.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:204)). The browser promotion case and focused tests agree with the source.
- **Notifications — confirmed strength with a documented limitation:** An INotificationService abstraction sends confirmation, cancellation, and promotion events after commit; exceptions are caught and logged, so a failed notification does not undo the domain change ([notification interface](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Notifications/INotificationService.cs:21), [RegistrationService.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:234)). Delivery is in-process rather than durable across a crash. The README acknowledges this, and durable delivery was not required.
- **API and architecture — confirmed strength:** Required endpoints exist with useful validation and problem responses ([Endpoints.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Api/Endpoints.cs:14)). Domain logic is concentrated in a cohesive service; endpoints handle transport and DTO mapping without an unnecessary repository layer. The [README](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/README.md) explains running, architecture, assumptions, and limitations.
- **Frontend engineering — strength with a failure-path defect:** Typed API calls and a sequence-aware fetching hook support normal synchronization. After successful mutations, all open panels reload, matching the live UI checks ([App.tsx](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/App.tsx:30)). The same hook hides refresh failures when old data exists, as detailed below.

## Important defects and risks

1. **Confirmed code defect, conditional on a failed refetch:** The fetching hook catches errors but retains old data and resolves the reload promise ([useAsync.ts](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/hooks/useAsync.ts:21)). The workshop, registration, and schedule components show errors only when they have no data ([WorkshopList.tsx](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/WorkshopList.tsx:16), [RegistrationList.tsx](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/RegistrationList.tsx:48), [ScheduleView.tsx](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/ScheduleView.tsx:21)). A registration or cancellation can succeed, its subsequent GET can fail, and the UI will keep old counts or statuses beside the earlier success notice. This was established from source; it did not occur in the observed browser workflows.
2. **Confirmed frontend retry weakness, conditional on a lost response:** Each registration click generates a new idempotency key ([App.tsx](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/App.tsx:44)). If the first POST committed but its response was lost, the user retry would use a new key and receive a duplicate-registration error instead of replaying that success. This does not undermine the required API same-key behavior, which is implemented and tested.
3. **Likely secondary API risk:** Concurrent creation of participants with the same email can race between the existence check and insert, leading the unique database index to throw an untranslated server error rather than the documented duplicate response ([Endpoints.cs](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Api/Endpoints.cs:129)). This is outside the required registration concurrency rule and was not reproduced.
4. **Documented limitation:** In-process notifications have a crash window after commit and before send. Notification *failure* is handled correctly; guaranteed delivery was not specified.

No core seat-allocation, FIFO, overlap, or idempotency defect was found. I do not count the absence of frontend automated tests as a missing required test scenario, since the specified automated business cases are covered by backend integration tests.

## Strengths

The strongest aspects are the actual database concurrency strategy, transactionally persisted idempotency, clear conflict-aware FIFO promotion, meaningful integration tests, coherent small architecture, detailed README, understandable API errors, and a UI whose normal mutation flows update all visible panels. The live application also has good status clarity and works across the tested screen sizes.

## Scores

- Code quality and correctness: **9.0/10**
- Requirement fulfillment: **9.4/10**
- Visual Design: **8.3/10**
- Responsive Design: **8.5/10**
- Universal Design: **8.0/10**
- Design average: **8.3/10**
- Total: **26.7/30**

## Final verdict

RUD-Opus is a strong, near-complete SeatFlow implementation. Its difficult backend requirements have credible source and test support, and the browser results are consistent with them. The most serious hidden issue is stale frontend data after a successful mutation when a subsequent refresh fails. The other important client weakness is that a retry after a lost POST response uses a new idempotency key. Neither issue was seen during the successful live workflows, so the report separates those source-confirmed failure paths from observed browser bugs.
