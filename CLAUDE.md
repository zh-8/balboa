# CLAUDE.md — Balboa

Guidance for Claude Code (and any agent) working in this repo. Read this before touching
anything.

## 1. What Balboa is

A .NET 10 + Angular **modular monolith** deployed to Azure Container Apps.

> **The product domain is not yet defined.** `backend/src/Modules/Sample/` is a placeholder
> that exists only to demonstrate the module pattern. When the first real bounded context
> arrives, it replaces Sample — do not build on Sample as if it were real, and do not add
> features to it.

Status: skeleton. Phase A (local) is in progress; Phase B (Azure) is blocked on a billing
upgrade. See `docs/architecture/overview.md`.

## 2. Commands

Everything goes through `make`. CI calls the same targets, so local and CI cannot drift.
Run `make help` for the full list.

| Target | Purpose |
|---|---|
| `make setup` | Restore backend, frontend and tool dependencies |
| `make build` | Build backend + frontend |
| `make test` | Fast gate: unit + architecture + frontend. **No Docker needed** |
| `make test-int` | Integration + API tests against real Postgres. **Needs Docker** |
| `make coverage` | Coverage report and gate |
| `make lint` / `make fmt` | Lint / format. `make fmt-check` is the CI variant |
| `make run` | Whole stack locally via Aspire |
| `make spec-check` | Verify every acceptance criterion has a tagged test |
| `make diagrams` / `make diagrams-check` | Export / verify Excalidraw SVGs |
| `make infra-lint` | Build and lint Bicep. No Azure login required |
| `make ci` | Everything CI runs except integration tests |

Never invent a new script when a target exists. If you need a new capability, add a target.

## 3. Repo map

```
backend/          .NET 10 modular monolith
  src/
    Balboa.Host/            composition root, minimal APIs, health, `migrate` command
    Balboa.AppHost/         Aspire orchestration, LOCAL DEV ONLY (never deployed)
    Balboa.ServiceDefaults/ OpenTelemetry, health checks, resilience
    Balboa.SharedKernel/    Entity, AggregateRoot, ValueObject, IDomainEvent, Result
    Balboa.BuildingBlocks/  handler abstractions, outbox/inbox, EF conventions
    Modules/<Context>/      one folder per bounded context
      *.Contracts/          PUBLIC surface: integration events, query interfaces, DTOs
      *.Domain/             aggregates, value objects, domain events
      *.Application/        command/query handlers, ports
      *.Infrastructure/     DbContext, migrations, endpoints, AddXModule/MapXEndpoints
  tests/
    Balboa.ArchitectureTests/  boundary and layering rules
    Balboa.Host.ApiTests/      end-to-end HTTP against real Postgres
    Balboa.TestKit/            fixtures, builders, the [Spec] attribute
    Modules/<Context>/{UnitTests,IntegrationTests}/
frontend/         Angular workspace (standalone, signals, zoneless)
infra/
  bootstrap/      one-time subscription-scope setup, run by a human
  bicep/          per-environment infrastructure, applied by CI
docs/
  specs/          SPEC-NNNN-slug/spec.md — write BEFORE code
  adr/            architecture decision records (MADR format)
  architecture/   overview + diagrams/*.excalidraw + exported *.svg
  runbooks/       operational procedures
  glossary.md     ubiquitous language
tools/
  spec-check/     spec-to-test traceability checker
  diagrams/       Excalidraw -> SVG exporter and freshness check
.github/workflows/  ci.yml, main.yml, codeql.yml + reusable _*.yml
```

## 4. Non-negotiables

1. **Spec first.** Non-trivial change → write or update `docs/specs/SPEC-NNNN-*/spec.md`
   with numbered acceptance criteria (`AC-NNNN-01`) before writing code. Architectural
   choices get an ADR in `docs/adr/`.
2. **TDD.** Write the failing test first, then the code. The PR history should show it.
3. **Module boundaries are law.** See §5.
4. **No secrets in git, ever.** Postgres uses Entra managed-identity tokens, so there is no
   connection-string password to leak. Config goes in environment variables or Key Vault.
   `.env` is gitignored; `.env.example` is the documented template.
5. **Infrastructure as code only.** No portal clicks. If a resource exists, it is in
   `infra/bicep/`.
6. **Trunk-based.** Short-lived branches, small PRs into `main`, squash merge. `main` is
   always releasable. Long-lived branches are not allowed — use a feature flag instead.
7. **Conventional commits** (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`,
   `ci:`, `infra:`). The PR title is linted.
8. **Docs ship with the change**, in the same PR. A stale diagram fails `make diagrams-check`.
9. **Never force-push `main`.** Never rewrite shared history.

## 5. Module rules

A module is a bounded context. Boundaries are enforced by tooling, not goodwill — three
layers, fastest feedback first:

1. **Build guard** (`backend/Directory.Build.targets`): a `ProjectReference` from
   `Modules/X/**` to `Modules/Y/**` fails the build unless the target ends in `.Contracts`.
   `*.Domain` may reference only `SharedKernel`.
2. **Architecture tests** (ArchUnitNET, in `Balboa.ArchitectureTests`): Domain free of EF and
   ASP.NET; Application free of Infrastructure; no cross-module non-Contracts dependency;
   Contracts contain only records and interfaces; Host touches only `AddXModule` /
   `MapXEndpoints`; each `DbContext` maps only its own entities; handlers are
   `internal sealed`.
3. **Visibility**: implementation types are `internal`. `InternalsVisibleTo` is granted only
   to a module's own sibling projects and its tests.

**Data.** One database, one **schema per module**, one `DbContext` per module with its own
migrations history table in that schema. No cross-schema foreign keys and no cross-schema
joins. Modules reference each other by ID only.

**Talking between modules.**

| Need | Mechanism |
|---|---|
| Inside a module | `ICommandHandler<T>` / `IQueryHandler<T,R>` resolved from DI |
| Read another module | a query interface published in that module's `.Contracts` |
| Tell another module something happened | integration event via the transactional outbox |

- **No MediatR** (commercially licensed since v13). Cross-cutting concerns are Scrutor
  decorators around the handler interfaces.
- Domain events stay inside the module and dispatch before commit. Integration events are
  written to the module's outbox in the same transaction as the aggregate change, then
  delivered in-process after commit; subscribers keep an inbox table for idempotency. A
  startup sweep drains leftovers, because scale-to-zero makes background pollers unreliable.

### Adding a module

1. Write `docs/specs/SPEC-NNNN-<context>/spec.md` and the glossary entries.
2. Create the four projects under `backend/src/Modules/<Context>/` and add them to
   `Balboa.slnx`.
3. Write failing tests in `backend/tests/Modules/<Context>/`.
4. Implement Domain → Application → Infrastructure.
5. Add `AddXModule` / `MapXEndpoints`, and register them in `Balboa.Host`.
6. Add the schema and the EF migration.
7. Update the module-map diagram.

## 6. Test conventions

- **xUnit v3** on Microsoft.Testing.Platform. **Shouldly** for assertions (FluentAssertions
  v8 is commercial). NSubstitute, sparingly. Bogus for test data.
- Integration tests carry `[Trait("Category", "Integration")]` so `make test` can skip them.
  They use Testcontainers `postgres:17`, one container per assembly, Respawn between tests,
  and apply the real migrations — so migrations are tested too.
- Name tests `Method_Scenario_ExpectedOutcome`.
- **Traceability is mandatory.** Tag each test with the criterion it proves:
  - .NET: `[Spec("AC-0001-02")]`
  - Vitest / Playwright: prefix the title `[AC-0001-02]`
  `make spec-check` fails if a criterion has no test or a tag names a criterion that does not
  exist.
- Coverage gate: **≥85% line coverage on `*.Domain` and `*.Application`**. Infrastructure and
  Host are excluded — integration tests cover those.
- Playwright is for a handful of post-deploy smoke tests only, never in the PR gate.

## 7. Docs and diagrams

- Diagrams are **Excalidraw**. Commit the `.excalidraw` source *and* the exported `.svg`,
  and embed the SVG in Markdown.
- `make diagrams` exports; `make diagrams-check` verifies freshness by comparing a
  `<!-- src-sha256:… -->` marker in the SVG against the source hash. Edit a diagram → re-run
  `make diagrams` → commit both files.
- Specs use `docs/specs/_template.md`; ADRs use `docs/adr/_template.md` (MADR). ADRs are
  immutable once Accepted — supersede, never rewrite.

## 8. Model and risk policy

Defined in `.claude/agents/bingo.md`. The short version:

- Routine work runs on **Sonnet 5.5** to control cost.
- **Planning and high-risk work must go to Opus 5.5.**
- **High risk** = anything creating, changing or destroying cloud resources; identity, RBAC,
  OIDC or secrets; production deploys and rollbacks; database migrations;
  security-sensitive code; CI changes that gate releases; anything hard to reverse.
- High-risk steps need **explicit user approval before execution**, every time. Approval for
  one step is not approval for the next.

## 9. Azure conventions

- Naming: `{type}-balboa-{env}-itn`, e.g. `ca-balboa-api-dev-itn`. Globally unique names get
  a 4-character `uniqueString` suffix.
- Required tags: `app=balboa`, `env=<env>`, `owner=zh-8`, `managedBy=bicep`.
- Region **Italy North** (`itn`), the closest region to Cairo offering Container Apps and
  Postgres Flexible Server. Fallback: Germany West Central, then West Europe. Static Web Apps
  is not offered in Italy North, so it sits in West Europe and serves from the edge.
- Images live on **ghcr.io**, not ACR: free for public repos, where ACR Basic would be about
  $5/month. See ADR-0016.
- **Two cost traps, both already encoded in the Bicep — do not undo them:**
  1. The Container Apps environment must be **Consumption-only**. A workload-profile
     environment bills about **$95/month** in management fees.
  2. Only **one** Postgres B1ms fits the 750 h/month free allowance. A second server running
     24/7 is billable.
- Environments: `dev` exists; **prod is deliberately deferred** until the first real feature.

## 10. Do not touch

- **`rg-cairobits`** and the `cairobits` Static Web App — an unrelated project in the same
  subscription. No script may name it or operate above it in scope.
- **`main` history** — no force-push, no rewrite.
- **Prod** — no deploy without explicit approval, once it exists.
- **`infra/bicep/*.json`** — generated from Bicep, gitignored. Never hand-edit.
- The **spending limit / billing settings** — report problems, do not try to change them.
