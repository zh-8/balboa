# ADR-0014: Test strategy

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We practice TDD (red, green, refactor) and need a test suite that is fast enough to run
constantly, trustworthy against real infrastructure, and licence-safe. Several popular test
libraries have turned commercial: FluentAssertions v8 requires a paid licence for commercial
use, and MediatR is likewise affected (ADR-0004). xUnit v2 is in maintenance while v3 is the
current line.

## Decision drivers

- Fast default gate that needs no Docker.
- Real database in integration tests, including real migrations.
- No licence risk in test dependencies.
- A measurable quality bar on the code that holds the business rules.

## Considered options

1. In-memory fakes (EF InMemory or SQLite) for integration tests.
2. Testcontainers with real PostgreSQL.
3. FluentAssertions v8 versus Shouldly for assertions.
4. Coverage gates everywhere versus targeted gates.

## Decision

- **TDD**: failing test first; the PR history should show it.
- **Pyramid**: many unit tests (Domain, Application, no IO), fewer integration tests
  (Infrastructure and API against real Postgres), a handful of Playwright smoke tests after
  deploy, never in the PR gate.
- **Frameworks**: **xUnit v3** on Microsoft.Testing.Platform; **Shouldly** for assertions
  (**not FluentAssertions v8**, which is commercial); NSubstitute sparingly; Bogus for data.
  Frontend uses Vitest.
- **Integration tests** carry `[Trait("Category", "Integration")]` so `make test` skips them.
  They use **Testcontainers** `postgres:17`, one container per assembly, Respawn between
  tests, and apply the real migrations.
- **Coverage gate**: at least **85% line coverage on `*.Domain` and `*.Application`**.
  Infrastructure and Host are excluded because integration tests cover them.
- Test names follow `Method_Scenario_ExpectedOutcome`; traceability per ADR-0013.

## Consequences

### Good

- `make test` is fast and Docker-free, so it runs on every save and every PR.
- Integration tests catch provider-specific SQL and migration errors that fakes hide.
- No commercial licence surprises in the test stack.
- The coverage gate sits where the business logic is, without chasing generated code.

### Bad

- Integration tests need Docker and are slower, so they run in a separate target.
- Coverage is a floor, not a quality measure; it can be met by weak tests.
- Shouldly has a smaller ecosystem and fewer features than FluentAssertions.
- xUnit v3 and Microsoft.Testing.Platform tooling support is newer and less documented.
- Excluding Infrastructure and Host from the gate relies on integration tests being
  thorough, which the gate does not verify.

## Revisit trigger

Reopen if FluentAssertions changes its licensing back, if Testcontainers cannot run on CI
runners, if the 85% gate causes low-value tests or is routinely met trivially, or if
integration tests exceed an agreed time budget (for example 5 minutes).
