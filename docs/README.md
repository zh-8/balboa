# Balboa documentation

| Area | What is in it |
|---|---|
| [architecture/overview.md](architecture/overview.md) | Start here: the architecture, topology, environments and current status |
| [specs/](specs/) | Feature specs with numbered acceptance criteria. Write before code. Use [`_template.md`](specs/_template.md) |
| [adr/](adr/) | Architecture decision records (MADR). Use [`_template.md`](adr/_template.md) |
| [runbooks/](runbooks/) | Operator procedures for the planned Azure setup |
| [glossary.md](glossary.md) | Ubiquitous language |

## Specs

| ID | Title | Status |
|---|---|---|
| [SPEC-0001](specs/SPEC-0001-sample-module/spec.md) | Sample module - widget catalogue (placeholder) | Accepted |

## Runbooks

All runbooks describe the **planned** Azure setup, which does not exist yet.

- [bootstrap.md](runbooks/bootstrap.md) - one-time subscription-scope setup
- [deploy-rollback.md](runbooks/deploy-rollback.md) - deploys and rollback
- [teardown.md](runbooks/teardown.md) - destroying Balboa safely
- [db-restore.md](runbooks/db-restore.md) - Postgres point-in-time restore
- [rotate-identity.md](runbooks/rotate-identity.md) - rotating federated credentials and identities

## ADR index

| ADR | Decision |
|---|---|
| [0001](adr/0001-record-decisions-in-madr-adrs.md) | Record decisions in MADR-format ADRs |
| [0002](adr/0002-modular-monolith-with-ddd.md) | Modular monolith with DDD |
| [0003](adr/0003-three-layer-boundary-enforcement.md) | Three-layer boundary enforcement |
| [0004](adr/0004-in-process-handlers-no-mediatr-outbox.md) | In-process handlers, no MediatR, outbox/inbox |
| [0005](adr/0005-postgresql-schema-per-module.md) | PostgreSQL, schema per module, EF Core |
| [0006](adr/0006-container-apps-consumption-only.md) | Container Apps, Consumption-only |
| [0007](adr/0007-angular-on-static-web-apps-free.md) | Angular on Static Web Apps Free |
| [0008](adr/0008-region-italy-north.md) | Region Italy North |
| [0009](adr/0009-github-oidc-user-assigned-managed-identities.md) | GitHub OIDC on managed identities |
| [0010](adr/0010-entra-only-postgres-authentication.md) | Entra-only Postgres authentication |
| [0011](adr/0011-no-vnet-in-phase-1.md) | No VNet in phase 1 |
| [0012](adr/0012-trunk-based-development.md) | Trunk-based development |
| [0013](adr/0013-spec-driven-development.md) | Spec-driven development |
| [0014](adr/0014-test-strategy.md) | Test strategy |
| [0015](adr/0015-excalidraw-diagrams.md) | Excalidraw diagrams |
| [0016](adr/0016-ghcr-instead-of-acr.md) | ghcr.io instead of ACR |
| [0017](adr/0017-bicep-layering-and-naming.md) | Bicep layering and naming |
| [0018](adr/0018-defer-production-environment.md) | Defer the production environment |
