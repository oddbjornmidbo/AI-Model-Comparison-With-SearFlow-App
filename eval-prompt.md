# SeatFlow benchmark evaluator

You are the lead evaluator for four independently generated implementations of the same SeatFlow assignment.

The implementations are:

- `Sonnet` — http://localhost:5173/
- `Opus` — http://localhost:57325/
- `Sol` — http://localhost:5174/
- `Luna` — http://localhost:8200/

Your job is to evaluate all four implementations fairly and consistently.

Use:

1. Playwright/browser inspection of all four running applications
2. Functional testing through the UI
3. Responsive-design testing
4. Universal Design / accessibility testing
5. Backend/frontend builds and automated tests
6. A GPT-6 Astra sub-agent for the source-code review
7. Your own synthesis of all evidence into the final report

Do NOT modify, fix, refactor, or improve any implementation.

Do not favor or penalize an implementation because of the model name.
Treat `Sonnet`, `Opus`, `Sol`, and `Luna` only as identifiers.

The goal is to determine which implementation best satisfies the original SeatFlow assignment and how the implementations differ in correctness, engineering quality, usability and design.

---

# Original SeatFlow assignment

Build a complete small application called SeatFlow.

The goal is to implement and verify a realistic full-stack feature.

## Technology stack

Backend:
- .NET 9
- ASP.NET Core Web API
- EF Core
- SQLite
- xUnit

Frontend:
- React
- TypeScript
- Vite

Do not require:
- Docker
- authentication
- cloud services
- external databases
- elaborate styling

---

# Domain

A workshop has:
- id
- title
- start time
- end time
- capacity

A participant has:
- id
- name
- email

A registration has:
- id
- workshop id
- participant id
- status: Confirmed, Waitlisted, or Cancelled
- created timestamp

---

# Business rules

1. A participant may register for a workshop only once unless their previous registration was cancelled.

2. If the workshop has available capacity, a new registration becomes Confirmed.

3. If the workshop is full, the registration becomes Waitlisted.

4. Waitlist ordering is FIFO by registration creation time.

5. When a confirmed registration is cancelled, the oldest eligible waitlisted registration must automatically be promoted to Confirmed.

6. A participant must never have two Confirmed workshops whose time ranges overlap.

7. When promoting somebody from a waitlist:
   - skip participants who would have a schedule conflict
   - leave skipped registrations on the waitlist
   - promote the first eligible participant

8. Registration creation must support an `Idempotency-Key` HTTP header.
   Repeating the same request with the same key must return the same registration and must not create duplicates.

9. Capacity must be safe under concurrent registration attempts.
   If two users attempt to take the final available seat at the same time, only one may become Confirmed.

10. Cancelling an already cancelled registration must be idempotent.

---

# Notifications

Create an `INotificationService` abstraction.

Notifications should be sent when:
- a registration becomes Confirmed
- a waitlisted participant is promoted
- a registration is cancelled

A development implementation may record notifications in memory or log them.

Notification failures must NOT roll back a successful registration or cancellation.

---

# Required API

At minimum:

- GET `/api/workshops`
- POST `/api/workshops`
- GET `/api/workshops/{id}/registrations`
- POST `/api/workshops/{id}/registrations`
- POST `/api/registrations/{id}/cancel`
- GET `/api/participants/{id}/schedule`

Use sensible validation responses and HTTP status codes.

---

# Required frontend

The UI should allow a user to:

- see all workshops and remaining capacity
- select a workshop
- see confirmed registrations and the waitlist
- register an existing participant
- cancel a registration
- see a participant's schedule
- clearly distinguish Confirmed, Waitlisted, and Cancelled states

The UI does not need elaborate styling, but it must work.

---

# Seed data

Seed at least:

- 4 workshops
- 5 participants
- 2 partially overlapping workshops

---

# Required automated tests

At minimum cover:

- registering into an available workshop
- registering into a full workshop
- FIFO promotion after cancellation
- skipping an ineligible waitlisted participant because of a schedule conflict
- prevention of overlapping confirmed registrations
- duplicate registration prevention
- Idempotency-Key behavior
- cancelling twice
- concurrent attempts for the final seat

Prefer tests of business behavior rather than implementation details.

---

# Architecture requirements

- Keep the solution reasonably simple.
- Avoid unnecessary abstractions and enterprise-style boilerplate.
- Domain logic should not live entirely in API controllers.
- Backend should build successfully.
- Backend tests should pass.
- Frontend should build successfully.
- README should explain:
  - how to run the backend
  - how to run the frontend
  - architecture summary
  - important assumptions
  - known limitations

---

# Phase 1 — Browser evaluation

Use Playwright against these exact applications:

- Sonnet: http://localhost:5173/
- Opus: http://localhost:57325/
- Sol: http://localhost:5174/
- Luna: http://localhost:8200/

Use a fresh browser context for each implementation where practical.

Evaluate all four applications using equivalent actions.

IMPORTANT:
Complete the initial browser/UI evaluation before performing the source-code review so that implementation knowledge does not bias the initial user-facing assessment.

## Functional browser checks

Test as much of the following as practical:

### General functionality
- workshops load correctly
- remaining capacity is understandable
- selecting a workshop works
- confirmed registrations are visible
- waitlist is visible
- cancelled registrations are represented sensibly
- participant schedule works

### Registration
- register into a workshop with available capacity
- fill a workshop
- register another participant and verify waitlisting
- attempt a duplicate registration where practical

### Cancellation and promotion
- cancel a confirmed registration
- verify automatic waitlist promotion
- verify resulting workshop counts
- verify UI state updates consistently
- attempt a second cancellation if practical

### Schedule conflicts
Where seed data permits:
- attempt to register a participant into overlapping workshops
- verify that conflicting confirmed registrations are prevented

### UI state consistency

Look specifically for:

- stale counts
- stale selected-workshop data
- inconsistencies between panels
- incorrect capacity values
- errors after mutations
- failure banners
- browser console errors
- failed network requests
- incorrect status labels
- buttons enabled for invalid actions
- UI not refreshing after changes
- misleading success/error messages

If concurrency or HTTP idempotency cannot reasonably be tested through the UI, do NOT mark them as failed solely for that reason.
Those requirements should be evaluated later through tests and source-code review.

Capture screenshots for notable strengths or defects where useful.

---

# Phase 2 — Responsive Design and Universal Design

Test every application at approximately:

- Desktop: 1440 × 900
- Tablet: 768 × 1024
- Mobile: 390 × 844

Interact with the application at each size.
Do not judge responsiveness from screenshots alone.

---

## Responsive Design checks

Check:

- no unintended horizontal scrolling
- content reflows sensibly
- cards, panels, tables and forms stack appropriately
- no clipped text
- no overlapping controls
- readable typography
- important actions remain visible and usable
- forms remain usable
- workshop information remains understandable
- registration/cancellation still works
- participant schedule remains usable
- status indicators remain readable
- buttons and inputs remain practical on touch screens
- spacing remains reasonable
- layout makes appropriate use of available width
- functionality is preserved across viewport sizes

Flag interfaces that technically fit on mobile but become awkward or unpleasant to use.

---

## Universal Design / accessibility checks

Evaluate practical inclusive usability.

Do NOT require formal WCAG certification.

Check where applicable:

- semantic heading structure
- meaningful page structure
- form controls have clear labels
- keyboard operation works
- logical tab order
- visible keyboard focus
- understandable button and link names
- status is not communicated by color alone
- states have sufficient visual distinction
- error messages are understandable
- errors are associated with the relevant action or control
- reasonable text contrast
- usable touch target sizes
- browser zoom remains usable
- content reflows instead of becoming inaccessible
- important information does not require hover
- disabled controls are understandable
- screen-reader-oriented DOM semantics appear reasonable
- tables/lists use sensible semantic structure
- dynamic changes provide understandable feedback
- the interface remains understandable for users with differing vision, motor ability, input method and technical familiarity

Clearly distinguish between:

- confirmed accessibility defect
- likely accessibility concern
- responsive-layout defect
- usability weakness
- purely aesthetic preference

---

# Phase 3 — Builds and automated tests

For each folder:

- `Sonnet`
- `Opus`
- `Sol`
- `Luna`

Determine the documented or obvious commands and run, where feasible:

- backend build
- backend automated tests
- frontend build

Do NOT edit code or dependencies merely to rescue a failing implementation.

Record:

- backend build result
- frontend build result
- total number of backend tests
- passing tests
- failing tests
- meaningful build warnings
- suspicious or superficial tests
- important required scenarios that are not tested

A passing test suite is evidence of correctness, not proof.

---

# Phase 4 — GPT-6 Astra code-review sub-agent

Delegate the source-code review to a GPT-6 Astra sub-agent.

Astra should review all four folders:

- `Sonnet`
- `Opus`
- `Sol`
- `Luna`

Provide Astra with:

- the original SeatFlow requirements from this prompt
- relevant build results
- relevant automated-test results

Astra must NOT modify any code.

Astra does NOT need to perform the UI/design review.
Its role is source-code correctness, architecture, tests and engineering quality.

Tell Astra to:

- treat folder names only as identifiers
- review every implementation independently
- base findings on concrete source-code evidence
- distinguish confirmed defects from plausible risks
- distinguish correctness issues from stylistic preferences
- avoid giving credit simply because a test with the right name exists

---

## Astra review focus

### Concurrency

Determine whether final-seat allocation is genuinely race-safe.

Do not give full credit merely because there is a concurrency test.

Inspect whether simultaneous registration requests can oversubscribe workshop capacity.

Check the actual database/transaction/concurrency strategy.

---

### Idempotency

Verify that:

- `Idempotency-Key` is actually honored
- retries cannot create duplicate registrations
- repeated requests return the appropriate existing registration
- the key is persisted or otherwise reliably remembered
- behavior remains meaningful under realistic timing/race conditions

---

### Transactions

Check whether operations that must be atomic actually are.

Especially:

- capacity checking + registration
- cancellation
- waitlist promotion

Look for partial-state failure possibilities.

---

### Waitlist promotion

Verify:

- FIFO ordering
- conflicting participants are skipped
- skipped participants remain waitlisted
- first eligible participant is promoted
- promotion cannot violate workshop capacity

---

### Scheduling conflicts

Verify overlap logic.

Include boundary conditions.

A workshop ending exactly when another begins should normally NOT count as overlapping.

---

### Notifications

Verify:

- `INotificationService` or equivalent abstraction exists
- correct events generate notifications
- confirmed registration generates notification
- waitlist promotion generates notification
- cancellation generates notification
- notification failure does not roll back the successful domain operation

---

### API quality

Review:

- validation
- HTTP status codes
- error handling
- separation of concerns
- controller/service boundaries
- obvious API inconsistencies

---

### Frontend engineering

Review:

- API interaction
- state synchronization
- TypeScript quality
- error handling
- duplicated logic
- fragile state management
- obvious race/staleness problems

Do NOT score visual appearance from source code.

---

### Automated tests

Determine whether tests genuinely verify the required behavior.

Pay particular attention to:

- concurrency tests
- idempotency tests
- waitlist promotion
- skipped conflicting participant
- overlap prevention
- second cancellation

Flag tests that pass without actually proving the intended requirement.

---

### Architecture

Assess:

- simplicity
- maintainability
- clarity
- domain logic placement
- unnecessary abstractions
- excessive boilerplate
- cohesion
- appropriate separation of responsibilities

Do not penalize reasonable approaches merely because Astra would personally choose another architecture.

---

# Astra output format

Require Astra to produce:

## Sonnet

### Confirmed defects
### Likely risks
### Missing requirements
### Test-quality observations
### Architecture observations
### Strengths
### Recommended code-quality/correctness score: x/10

## Opus

Same structure.

## Sol

Same structure.

## Luna

Same structure.

Then:

# Cross-implementation code comparison

Compare:

- concurrency safety
- idempotency
- transaction correctness
- waitlist logic
- scheduling logic
- notification handling
- automated-test quality
- API quality
- frontend engineering
- architecture
- maintainability

Explain important score differences using concrete evidence.

The lead evaluator must critically assess Astra's findings rather than accepting them blindly.

---

# Scoring

Give every implementation three main scores:

A. Code quality and correctness — 0 to 10

B. Requirement fulfillment — 0 to 10

C. Design — 0 to 10

The maximum total score is 30.

Use one decimal place where useful.

---

# A. Code quality and correctness — 0 to 10

Base this score on:

- Astra's source-code review
- build/test evidence
- runtime evidence relevant to correctness
- robustness
- architecture
- automated-test quality

Suggested interpretation:

10:
Exceptional implementation.
Requirements are implemented robustly, difficult concurrency/idempotency behavior is correct, architecture is clean, tests are meaningful, and no significant defects are found.

8–9:
Strong implementation with only minor weaknesses.

6–7:
Generally good but contains meaningful weaknesses, omissions, fragile logic, or architectural problems.

4–5:
Partially correct with several important defects or missing requirements.

2–3:
Major correctness or architectural problems.

0–1:
Fundamentally broken or largely incomplete.

Do NOT inflate this score merely because the application runs.

---

# B. Requirement fulfillment — 0 to 10

Evaluate how completely and correctly the finished application follows the original SeatFlow assignment.

Consider:

- required business rules
- API
- notifications
- backend
- frontend
- required technologies
- seed data
- automated tests
- README
- observed runtime behavior

A visually unattractive application can still score highly here if it fulfills the specification correctly.

Suggested interpretation:

9–10:
Essentially all requirements satisfied correctly.

7–8:
Most requirements satisfied with minor omissions or defects.

5–6:
Substantial implementation exists, but several requirements are incomplete or incorrect.

3–4:
Many important requirements are missing or broken.

1–2:
Only a small subset of the assignment is fulfilled.

0:
Assignment effectively not implemented.

---

# C. Design — 0 to 10

Design is composed of THREE independent 0–10 scores:

1. Visual Design
2. Responsive Design
3. Universal Design

Calculate:

Design score =
(Visual Design + Responsive Design + Universal Design) / 3

Round the final Design score to one decimal place.

Do NOT allow strength in one area to hide weakness in another.

For example:
an attractive desktop UI with poor mobile behavior may receive a high Visual Design score but a low Responsive Design score.

---

# C1. Visual Design — 0 to 10

Evaluate:

- visual hierarchy
- layout composition
- readability
- spacing
- typography
- consistency
- clarity of states and statuses
- form presentation
- use of available screen space
- perceived polish
- whether the interface feels coherent and intentional

Suggested interpretation:

9–10:
Highly polished and coherent.
Strong hierarchy, excellent spacing and clarity, with very few visual weaknesses.

7–8:
Good professional-looking interface with some minor rough edges.

5–6:
Functional and reasonably clear but visually ordinary, inconsistent or developer-oriented.

3–4:
Noticeably weak layout or presentation although still usable.

1–2:
Poor visual structure that significantly harms usability.

0:
Effectively unusable visually.

Do not penalize an implementation for lacking elaborate graphics or decorative design because sophisticated styling was not required.

---

# C2. Responsive Design — 0 to 10

Evaluate behavior across:

- desktop
- tablet
- mobile

Consider:

- sensible reflow
- stacking
- clipping
- overflow
- horizontal scrolling
- readability
- forms
- controls
- mobile interaction
- use of available space
- preservation of functionality
- layout stability when viewport changes

Suggested interpretation:

9–10:
Excellent responsive behavior across all tested sizes with no meaningful usability problems.

7–8:
Strong responsiveness with only minor layout/component issues.

5–6:
Mostly usable across sizes but with noticeable compromises or awkward layouts.

3–4:
Significant mobile/tablet problems while core functionality remains technically usable.

1–2:
Major responsive failures that make substantial functionality difficult to use.

0:
Effectively unusable outside the primary viewport.

A desktop-only implementation should score substantially lower even if the desktop appearance is excellent.

---

# C3. Universal Design / Accessibility — 0 to 10

Evaluate practical inclusive usability.

Consider:

- semantic structure
- headings
- form labels
- keyboard operation
- tab order
- visible focus
- understandable control names
- status not communicated by color alone
- contrast
- error feedback
- touch target sizes
- zoom/reflow
- avoidance of hover-only information
- disabled-state clarity
- screen-reader-friendly semantics
- meaningful dynamic feedback
- general usability across differing input methods and abilities

Suggested interpretation:

9–10:
Very strong inclusive design with thoughtful semantics, keyboard behavior, feedback and accessibility throughout.

7–8:
Good accessibility with relatively minor omissions.

5–6:
Reasonably usable but with several accessibility or inclusive-design weaknesses.

3–4:
Multiple significant accessibility problems although basic use remains possible.

1–2:
Serious barriers for keyboard, zoom, assistive-technology or alternative-input users.

0:
Fundamentally inaccessible.

---

# Evaluation rules

Be strict but fair.

Do NOT:

- favor an implementation because of the underlying model's reputation
- assume passing tests prove correctness
- assume more tests automatically mean better tests
- treat prettier UI as evidence of backend correctness
- treat more code as better code
- penalize reasonable architectural choices simply because they differ from your preference
- silently repair applications before scoring them
- allow Astra's opinion to override contradictory direct evidence without investigation

Clearly distinguish:

- Observed UI bug
- Confirmed code defect
- Likely issue/risk
- Missing requirement
- Maintainability/style concern
- Accessibility concern
- Pure aesthetic preference

If evidence is uncertain, say so explicitly.

---

# Final report

Start with a concise executive summary.

Then provide the main score table:

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
|---|---:|---:|---:|---:|
| Sonnet | | | | |
| Opus | | | | |
| Sol | | | | |
| Luna | | | | |

Where:

Design =
(Visual + Responsive + Universal Design) / 3

Total =
Code + Requirements + Design

---

Then provide the design breakdown:

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
|---|---:|---:|---:|---:|
| Sonnet | | | | |
| Opus | | | | |
| Sol | | | | |
| Luna | | | | |

---

Then create one detailed section per implementation.

# Sonnet

## Browser findings
## Responsive Design
## Universal Design
## Build and automated tests
## Astra code-review findings
## Important defects
## Strengths

## Scores
- Code: x/10
- Requirements: x/10
- Visual Design: x/10
- Responsive Design: x/10
- Universal Design: x/10
- Design average: x/10
- Total: x/30

Repeat the same structure for:

# Opus
# Sol
# Luna

---

# Cross-model comparison

Compare concrete differences in:

- functional correctness
- concurrency
- idempotency
- waitlist behavior
- schedule-conflict handling
- transaction handling
- notification robustness
- API design
- automated-test quality
- frontend engineering
- runtime reliability
- architecture
- maintainability
- unnecessary complexity
- visual quality
- responsive behavior
- Universal Design / accessibility

Prefer concrete evidence over vague impressions.

---

# Final ranking

Rank the four implementations from strongest to weakest based on the combined evidence.

For each position provide a concise explanation.

Do not use model identity or reputation as justification.

Also identify:

- Best backend/domain implementation
- Best automated tests
- Best frontend engineering
- Best architecture
- Best visual design
- Best responsive design
- Best Universal Design / accessibility
- Best mobile experience
- Best runtime robustness
- Most serious hidden bug
- Most polished overall application
- Simplest implementation that still satisfies the task

Where appropriate, mention if the winner of a category is only narrowly ahead.

End with a short summary of the most important differences between the four implementations.
