# ADR-0004: In-process DI handlers with Scrutor decorators, no MediatR, transactional outbox

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We need a way to dispatch commands and queries inside a module, apply cross-cutting concerns
(logging, validation, transactions), and let modules react to each other's events without
sharing a transaction. MediatR is the common default, but it has been commercially licensed
since v13, and this project is public with all rights reserved and a near-zero budget. The
app also runs on Container Apps with scale-to-zero (ADR-0006), where a background poller
cannot be relied on to be running.

## Decision drivers

- No commercial licence exposure in the core dispatch path.
- Cross-cutting behaviour must be applied uniformly and be testable.
- Cross-module events must not be lost if the process stops after commit.
- Scale-to-zero means the process can be terminated at any time.
- Keep the mechanism small enough to read in one sitting.

## Considered options

1. MediatR (v13+ commercial licence) or an older, unlicensed-risk version.
2. A third-party mediator library (for example Mediator source generator, Wolverine).
3. Plain interfaces `ICommandHandler<T>` and `IQueryHandler<T,R>` resolved from DI, with
   Scrutor decorators for cross-cutting concerns.
4. A message broker (Azure Service Bus) for cross-module events.

## Decision

Use **plain DI handlers**: `ICommandHandler<T>` and `IQueryHandler<T,R>` in
`Balboa.BuildingBlocks`, implemented as `internal sealed` classes. Cross-cutting concerns
(logging, validation, unit of work) are **Scrutor decorators** registered around the handler
interfaces. **MediatR is not used.**

Cross-module communication uses a **transactional outbox and inbox**:

- Domain events stay inside the module and dispatch before commit.
- Integration events are written to the module's outbox table in the same transaction as the
  aggregate change, then delivered in-process after commit.
- Subscribers record handled events in an inbox table, making delivery idempotent
  (at-least-once delivery, effectively-once handling).
- A **startup sweep** drains undelivered outbox rows, because a poller cannot be relied on
  under scale-to-zero. Delivery latency for leftover events is therefore bounded by the next
  start of the app.

## Consequences

### Good

- No licensing risk and no dependency whose terms can change under us.
- The call graph is explicit: a handler is injected and called, with full IDE navigation.
- Decorators give pipeline behaviour with plain DI, and each is unit-testable.
- The outbox removes dual-write loss between the database and event delivery.
- No broker to pay for or operate.

### Bad

- We own the handler abstractions, decorators and outbox code, including their bugs.
- No ecosystem of ready-made pipeline behaviours or notification features.
- In-process delivery means no durable queue between modules; a crash between commit and
  delivery is recovered only by the startup sweep.
- Events left in the outbox wait until the app next starts, which can be a long time if the
  app is scaled to zero with no traffic.
- Outbox and inbox tables add write volume and need cleanup of old rows.
- Scrutor decoration order is subtle and must be covered by tests.

## Revisit trigger

Reopen if event latency from the startup sweep becomes unacceptable (consider a scheduled
Container Apps job or KEDA-triggered drain), if a module is extracted to its own deployable
(replace in-process delivery with a broker), or if the handler and outbox code grows into a
framework that a maintained library would serve better.
