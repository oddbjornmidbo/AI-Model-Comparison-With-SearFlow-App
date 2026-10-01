# SeatFlow AI Coding Benchmark

This experiment compared four coding models by asking each of them to build the same small full-stack application, **SeatFlow**, from the same specification. The goal was not only to see whether the models could produce a working application, but also to compare implementation quality, requirement coverage, UI quality, speed, and practical cost.

The full implementation task is defined in [`prompt.md`](./prompt.md). In short, the assignment required a .NET 9 / ASP.NET Core backend with EF Core and SQLite, a React + TypeScript frontend, automated tests, concurrency-safe seat allocation, idempotency, waitlist promotion, schedule-conflict handling, notifications, and a usable responsive interface.

## Models tested

Four implementations were generated independently:

- **Claude Opus 5.5**
- **Claude Sonnet 5.5**
- **GPT-6 Sol**
- **GPT-6 Luna**

Each model started from the same task specification and produced its own implementation in a separate folder. The models were allowed to build, run tests, debug failures, and self-correct without manual coding assistance.

The resulting applications were then evaluated separately. The judge used Playwright to exercise the running applications at desktop, tablet, and mobile sizes, ran backend/frontend builds and automated tests, and reviewed functional behavior. A GPT-6 Astra sub-agent was used for source-code review, with particular attention to concurrency, idempotency, transactions, waitlist logic, test quality, API design, frontend state handling, and architecture.

Scores were divided into three equally weighted categories:

- **Code** — correctness, architecture, robustness, and automated-test quality
- **Requirements** — how completely the original assignment was satisfied
- **Design** — average of visual design, responsive design, and Universal Design/accessibility

The final score is the sum of these three 0–10 scores, for a maximum of 30.

## Results

| Model | Cost | Time | Code | Requirements | Design | Total |
|---|---:|---:|---:|---:|---:|---:|
| **Opus 5.5** | ~$4.00* | 14m 38s | **8.9** | **9.2** | 5.1 | **23.2** |
| **Luna** | 0–1% weekly, <5% 5h | 17m 24s | 7.1 | 8.2 | **7.5** | **22.8** |
| **Sonnet 5.5** | ~$0.80 | **5m 07s** | 8.2 | 8.8 | 4.6 | **21.6** |
| **Sol** | ~2% weekly, ~25–30% 5h** | 35m 35s | 7.0 | 7.7 | 5.9 | **20.6** |

## Detailed design results

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| Sonnet         | 6.4        | 4.4            | 3.0                  | 4.6                |
| Opus           | 7.2        | 5.0            | 3.2                  | 5.1                |
| Sol            | 6.4        | 5.0            | 6.3                  | 5.9                |
| Luna           | 8.4        | 8.3            | 5.7                  | 7.5                |

\* Opus showed about $6 total usage, but roughly $2 came from an unrelated Claude session that was accidentally triggered during the run.  
\** Sol's five-hour usage window reset during the run, so the short-window percentage is approximate. Its weekly usage was about 2%.  
\*** ChatGPT models were run under a private ChatGPT Plus subscription, while Claude models were run under an Enterprise license.

## What stood out

All four models produced applications that built successfully and passed their own backend test suites. The differences therefore appeared less in basic task completion and more in **how convincingly the difficult parts were implemented and verified**.

**Opus 5.5** produced the strongest overall engineering result. Its automated tests gave the best evidence for real concurrency behavior, including coordinated requests using separate SQLite contexts and connections. It also handled API behavior, domain rules, and runtime state consistently. Its main weakness was the UI: responsive behavior and keyboard accessibility were noticeably behind Luna and Sol.

**Luna** was the biggest surprise. Its code and tests were weaker than Opus, especially around concurrency evidence, but it produced the best-looking and most responsive interface. It also delivered excellent practical value relative to its very small quota usage. The main observed defect was stale participant-schedule state after some mutations, showing that attractive UI and complete-looking workflows can still hide state-management bugs.

**Sonnet 5.5** was by far the fastest model and extremely cost-effective at roughly $0.80. Its backend and tests were strong, and it came relatively close to Opus on code and requirements. Its main weaknesses were mobile layout and keyboard accessibility. For bounded full-stack implementation work, it delivered an unusually high amount of quality per dollar and per minute.

**GPT-6 Sol** produced a working and generally understandable implementation, with good accessibility fundamentals, but took the longest to finish. Its test suite gave weaker evidence for true database contention, and the evaluator found thinner validation and a frontend case where a failed refresh could still leave a success message visible.

## Conclusion

There was no single model that dominated every dimension. **Opus 5.5 produced the strongest complete engineering solution**, while **Luna produced the strongest interface**, and **Sonnet 5.5 delivered the best speed/cost efficiency among the directly token-priced models**. Sol was capable, but in this particular task its extra runtime did not translate into the highest score.

The experiment also suggests that model selection can be task-specific: a cheaper model may be very effective for UI generation, scaffolding, or bounded implementation work, while a stronger model can add value when correctness depends on concurrency, edge cases, and high-quality automated tests.

### Very brief summary from the Sol evaluation

The Sol-based evaluation ranked **Opus first (23.2/30), Luna second (22.8), Sonnet third (21.6), and Sol fourth (20.6)**. Opus had the strongest backend and test evidence, Luna led visual and responsive design, Sonnet offered a strong and simple implementation at very low cost and runtime, and Sol completed the core flows but had weaker validation and test evidence.

### Difference between Luna and Sol evaluation


The two evaluations were broadly consistent, but there was one important difference: the Luna-based evaluation initially scored Sonnet much lower because it failed to discover several UI workflows that were in fact present. The Sol-based evaluation navigated the application more successfully and raised Sonnet from 17.0 to 21.6 overall. For Opus, Luna, and Sol, however, the scores were quite close between evaluators, and both agreed on the main pattern: Opus had the strongest engineering, while Luna had the strongest design. The Sol evaluation appeared more thorough, but it also consumed substantially more ChatGPT usage quota than the Luna evaluation.

\* The rest of this report is based on the Sol evaluation

## Bonus: Code lines written

| Implementation | Backend API | Backend tests | Frontend | Total |
| -------------- | ----------: | ------------: | -------: | ----: |
| Sonnet         | 410         | 430           | 233      | 1,073 |
| Opus           | 576         | 555           | 606      | 1,737 |
| Sol            | 137         | 98            | 75       | 310 |
| Luna           | 415         | 197           | 388      | 1,000 |
