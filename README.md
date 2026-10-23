# Balboa

A .NET 10 + Angular **modular monolith** on Azure Container Apps, built with DDD, TDD,
trunk-based development and spec-driven development.

> **Status: skeleton.** The product domain is not yet defined.
> `backend/src/Modules/Sample/` is a placeholder demonstrating the module pattern; it will be
> replaced by the first real bounded context. Azure provisioning is pending a subscription
> upgrade — see [docs/architecture/overview.md](docs/architecture/overview.md).

## Quick start

Requires .NET 10 SDK, Node 24 and Docker.

```bash
make setup    # restore backend, frontend and tooling
make test     # unit + architecture + frontend tests (no Docker needed)
make test-int # integration tests against a real Postgres container
make run      # API + Postgres container locally via docker compose
```

`make help` lists every target. CI calls the same targets, so local and CI cannot drift.

The local Postgres binds **127.0.0.1:55432**, not 5432 — most machines already have something on
the default port, and `make db` refuses to start rather than let Balboa's migrations run against
a database that is not ours. Override with `make db BALBOA_PG_PORT=<port>`, and set
`ConnectionStrings__balboa` to match.

## Architecture at a glance

One deployable unit, strictly separated modules. Each module is a bounded context with its
own Domain, Application, Infrastructure and a published Contracts surface, plus its own
Postgres schema. Modules talk through Contracts interfaces for reads and a transactional
outbox for events — never by reaching into each other's internals. Three enforcement layers
(a build guard, architecture tests and `internal` visibility) make a boundary violation fail
the build rather than a code review.

| Concern | Choice |
|---|---|
| Backend | .NET 10, ASP.NET Core minimal APIs, EF Core |
| Frontend | Angular (standalone, signals, zoneless) |
| Database | PostgreSQL 17, one schema per module |
| Hosting | Azure Container Apps (consumption, scale-to-zero) + Static Web Apps |
| Registry | ghcr.io |
| IaC | Bicep |
| CI/CD | GitHub Actions, OIDC to Azure, no stored credentials |

Full detail: [docs/architecture/overview.md](docs/architecture/overview.md).
Decisions and their rationale: [docs/adr/](docs/adr/).

## How work happens here

1. **Spec first** — `docs/specs/SPEC-NNNN-*/spec.md` with numbered acceptance criteria.
2. **Failing test** — tagged `[Spec("AC-NNNN-01")]` so `make spec-check` can trace it.
3. **Code** — smallest change that passes.
4. **Small PR to `main`** — squash merge, `ci-ok` must be green.

Conventions, module recipe and the risk policy live in [CLAUDE.md](CLAUDE.md).

## Repository layout

```text
backend/   .NET solution: host, shared kernel, building blocks, modules, tests
frontend/  Angular workspace
infra/     Bicep — bootstrap (run once by a human) and per-environment stacks
docs/      specs, ADRs, architecture + Excalidraw diagrams, runbooks, glossary
tools/     spec-to-test traceability checker, Excalidraw SVG exporter
```

## Licence

**All rights reserved.** The source is public for reference only; no licence is granted.
See [LICENSE](LICENSE).
