# ADR-0002: Modular monolith with DDD

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Balboa's product domain is not yet defined, so its eventual boundaries are unknown. The team
is one owner plus agents, and the operating budget is close to zero (see ADR-0006 and
ADR-0018). We want the option to split into services later without paying the distributed
systems tax now.

## Decision drivers

- Domain boundaries are unknown, so they must be cheap to move.
- One person must be able to operate the whole system.
- Hosting cost must stay near zero, so fewer deployables is better.
- Boundaries must be real enough that extraction later is feasible.

## Considered options

1. Classic layered monolith (Controllers, Services, Repositories) with shared model.
2. Modular monolith: one deployable, modules aligned to DDD bounded contexts.
3. Microservices from day one.

## Decision

Build a **modular monolith** with **DDD**. There is one deployable (`Balboa.Host`). Each
bounded context is a module of four projects: `Contracts`, `Domain`, `Application`,
`Infrastructure`. Modules own their data (ADR-0005), talk through Contracts and the
transactional outbox (ADR-0004), and are held apart by tooling (ADR-0003).

Domain code depends only on `SharedKernel` and has no framework or IO dependencies. The
ubiquitous language lives in `docs/glossary.md` and in the specs.

## Consequences

### Good

- One build, one deploy, one database server: low cost and low operational load.
- Refactoring a boundary is a code change, not a cross-service migration.
- In-process calls and local transactions where they are safe; no network failure modes.
- A module with clean Contracts and its own schema can be extracted to a service later.

### Bad

- Boundaries hold only if enforcement stays strict; a monolith drifts easily.
- Four projects per module is ceremony that is wasteful for a tiny context.
- All modules scale and fail together; one noisy module affects all.
- A single deployable couples release cadence across modules.
- With no real domain yet, the module shape is validated only by the Sample placeholder.

## Revisit trigger

Reopen if any module needs an independent scaling profile, release cadence or runtime that
the shared deployable cannot provide, or if a team boundary appears that needs independent
deployment. Also reopen if the four-project layout proves too heavy once a second real
module exists.
