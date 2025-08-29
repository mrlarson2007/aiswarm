# Event Bus + WorkItem Notifications Review Request

Goal: Review code for readability, clean design, and robustness. Produce concise, actionable refactor suggestions and TDD-driven tests.

Targets:

- src/AISwarm.Infrastructure/Eventing/InMemoryEventBus.cs
- src/AISwarm.Infrastructure/Eventing/WorkItemNotificationService.cs
- tests/AISwarm.Tests/Services/WorkItemNotificationServiceTests.cs

Focus Areas:

- Cancellation: graceful completion, unsubscribe cleanup, no OCE
- Disposal: subscriptions complete; publish/subscribe after dispose throws
- Ordering: per-subscriber FIFO under concurrent PublishAsync
- Backpressure: risk of unbounded growth; bounded buffer/drop policy options
- Routing: persona subscription must ignore agent-assigned events

Deliverables:

- Write findings to: docs/reviews/event-bus-review.md
- Include: 1) Quick wins, 2) Suggested RED tests, 3) Minimal changes to pass

Acceptance:

- The output file exists and contains specific, minimal, test-first recommendations.
