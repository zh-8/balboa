# ADR-0019: Local development uses docker compose, Aspire deferred

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Running the whole stack locally needs an orchestrator for the API and a PostgreSQL 17
database. .NET Aspire (13.6.1) was the intended choice. Its restore pulls
`Aspire.Hosting.Orchestration.linux-x64`, `Grpc.Tools` and `KubernetesClient`, and on this
network that restore repeatedly timed out after about 25 minutes with "no data received for
60000ms". A stuck restore also blocked `dotnet format` across the whole solution, because
the AppHost project is part of it. Today there is exactly one service to run beside the
database.

## Decision drivers

- A restore that finishes reliably on the network we actually have.
- Local startup that works with one command (`make run`).
- Not letting an optional dev tool break the formatter, build or CI for everyone.
- Reversibility: the choice should be cheap to change later.

## Considered options

1. .NET Aspire AppHost (the original plan).
2. docker compose with a Postgres 17 container, API run with `dotnet run`.
3. Developers install PostgreSQL natively.

## Decision

We chose **docker compose** (`backend/docker-compose.yml`, Postgres 17), driven by
`make db`, `make run` and `make migrate`, because it has no restore cost, needs only Docker
(already required for integration tests, ADR-0014) and cannot block the solution build.
`Balboa.AppHost` is not part of the solution for now. Aspire is deferred, not rejected.

## Consequences

### Good

- Local database in seconds with no NuGet downloads.
- `dotnet restore`, `build` and `format` no longer depend on the Aspire packages.
- One mechanism for the database locally; Testcontainers covers tests separately.

### Bad

- No Aspire dashboard or service discovery for local traces and logs.
- Orchestration of several services will need more compose configuration by hand.
- Configuration for the API is set by environment and appsettings rather than Aspire wiring.

## Revisit trigger

Reopen when the network allows the Aspire restore to complete reliably, or when local
development needs to orchestrate more than one service (for example a second deployable or
a message broker).
