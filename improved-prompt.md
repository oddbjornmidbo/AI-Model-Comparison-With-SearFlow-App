## Build a complete small application called SeatFlow.

The goal is to test your ability to design, implement, test, and debug a realistic full-stack feature. Work autonomously. Do not ask clarifying questions unless you are truly blocked; make reasonable assumptions and document them briefly in README.md.

Tech stack

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
- Keep styling simple; functionality matters more than appearance.

Do not use Docker, authentication, cloud services, or external databases.

The application

SeatFlow manages workshop registrations for a small conference.

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

Business rules

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
8. Registration creation must support an Idempotency-Key HTTP header.
   Repeating the same request with the same key must return the same registration and must not create duplicates.
9. Capacity must be safe under concurrent registration attempts.
   If two users attempt to take the final available seat at the same time, only one may become Confirmed.
10. Cancelling an already cancelled registration must be idempotent.

Integration boundary

Create an INotificationService abstraction.

Notifications should be sent when:
- a registration becomes Confirmed
- a waitlisted participant is promoted
- a registration is cancelled

Provide a simple development implementation that records notifications in memory or logs them.

Notification failures must NOT roll back a successful registration or cancellation.

API

Implement at least:

GET /api/workshops
POST /api/workshops
GET /api/workshops/{id}/registrations
POST /api/workshops/{id}/registrations
POST /api/registrations/{id}/cancel
GET /api/participants/{id}/schedule

Use sensible HTTP status codes and validation responses.

Frontend

Create a simple UI where I can:
- see all workshops and remaining capacity
- select a workshop and see confirmed registrations and the waitlist
- register an existing participant
- cancel a registration
- see a participant's schedule
- clearly distinguish Confirmed, Waitlisted, and Cancelled states

The UI does not need to be beautiful, but it must work.

The UI should be responsive and follow basic Universal Design/accessibility principles, including keyboard usability, labelled controls, readable contrast, and mobile reflow.

Use Storybook for the frontend and create stories for each user-facing component. Use Storybook to test and inspect the relevant states of each component during implementation.

Seed data

Seed:
- at least 4 workshops
- at least 5 participants
- at least two partially overlapping workshops

Tests

Write meaningful automated tests.

At minimum cover:
- registering into an available workshop
- registering into a full workshop
- FIFO promotion after cancellation
- skipping an ineligible waitlisted participant because of a schedule conflict
- prevention of overlapping confirmed registrations
- duplicate registration prevention
- idempotency-key behavior
- cancelling twice
- concurrent attempts for the final seat

Prefer testing business behavior rather than implementation details.

Architecture

Keep the solution reasonably simple.

Avoid unnecessary abstractions and enterprise-style boilerplate.

The domain logic should not live entirely in API controllers.

Generated code should compile without warnings that indicate obvious problems.

Sub-agents

Delegate suitable work to sub-agents where doing so improves efficiency.

Use Sonnet 5.5 for easier but still substantive implementation tasks when appropriate, such as well-defined frontend components, Storybook stories, straightforward API work, documentation, focused tests, or other bounded development tasks.

Use Haiku 4.5 for simple, mechanical, and verification-oriented tasks. In particular, use Haiku 4.5 to run tests and report the results. It may also run builds, linting, type checks, Storybook checks, or perform other straightforward repository inspection and verification tasks.

Choose between Sonnet 5.5 and Haiku 4.5 based on the complexity of the task. Do not delegate architectural decisions, concurrency-sensitive design, transaction boundaries, idempotency design, or other correctness-critical decisions to Haiku 4.5.

You remain responsible for the complete solution. Review sub-agent output, integrate it correctly, and fix any issues they uncover.

Before finishing

1. Build the backend.
2. Run the backend tests.
3. Build the frontend.
4. Fix failures you encounter.
5. Add a README containing:
   - how to run the backend
   - how to run the frontend
   - architecture summary
   - important assumptions
   - any known limitations

Do not spend time on:
- authentication
- authorization
- Docker
- CI/CD
- deployment
- elaborate CSS
- accessibility audits
- production observability

Stop once the requirements are implemented and the relevant builds/tests pass.

Keep your commentary concise while working. Spend tokens primarily on implementation and verification rather than explaining every action.