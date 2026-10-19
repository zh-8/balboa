---
id: SPEC-0001
title: Sample module - widget catalogue
status: Accepted
module: Sample
adrs: [ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0013, ADR-0014, ADR-0020]
owner: zh-8
created: 2026-10-19
---

# SPEC-0001: Sample module - widget catalogue

> **This module is a placeholder.** It exists only to demonstrate the module pattern
> (Contracts / Domain / Application / Infrastructure, schema per module, domain events,
> outbox, spec-to-test traceability). It has no product meaning. When the first real bounded
> context arrives it **replaces** Sample, and this spec moves to `Superseded`. Do not add
> features to it and do not build on it as if it were real.

**Reconciliation note (2026-10-19).** This spec was first written in parallel with the
implementation and described a different model (item code, retire, Active/Retired). The
backend was then built, tested and verified (70 tests), so this spec was rewritten once to
describe what was actually built. This was a deliberate, one-off exception: Sample is a
disposable placeholder and the code was the verified artefact, so renaming working code was
not worth it. It is **not** a precedent that code beats spec. The normal rule stands: the
spec is written first and wins, and any future divergence is fixed by changing the code. See
[ADR-0020](../../adr/0020-spec-and-implementation-reconciliation.md).

## Context / Problem

Balboa's product domain is undefined, but the architecture needs a working vertical slice so
that the tooling (boundary enforcement, migrations, Testcontainers, `make spec-check`) is
proven before real work starts. Sample provides that slice with a deliberately generic
catalogue-item aggregate.

## Goals

- Demonstrate one aggregate with an invariant, a value object and a domain event.
- Demonstrate command and query handlers, a minimal API and a schema owned by the module.
- Give backend and frontend tests concrete criteria to tag with `AC-0001-NN`. Backend tests
  cover all six; the frontend covers only the read side (AC-0001-06). Frontend plumbing
  (correlation id, error mapping, feature flags) has no criterion and carries no tag.

## Non-goals

- Any real product behaviour, pricing, inventory or ordering.
- Authentication and authorization rules (not defined yet; endpoints are unauthenticated in
  this slice).
- Subscribers to the integration event. No second module exists; the outbox delivery is
  exercised in the Sample integration tests with a recording subscriber.
- Deleting or unpublishing widgets.

## Ubiquitous language

| Term | Meaning |
|---|---|
| Widget | An entry in the catalogue describing one kind of widget (aggregate `WidgetCatalogItem`, identified by a `WidgetId`). |
| SKU | Human-readable unique identifier of a widget, e.g. `WGT-001`. 3 to 20 characters: letters, digits and hyphens. Trimmed and upper-cased on entry. |
| Name | Display name of the widget, 1 to 100 characters after trimming. |
| Published | The widget has been made visible in the catalogue. Widgets are created unpublished. Publishing is one-way: there is no unpublish. |

These terms are placeholders and are intentionally not added to `docs/glossary.md`.

## Domain model sketch

- **Aggregate root:** `WidgetCatalogItem` - identified by a `WidgetId` (strongly-typed id
  wrapping a `Guid`).
  - `Sku` (`Sku` value object)
  - `Name` (string, trimmed, 1 to 100 characters)
  - `IsPublished` (bool, `false` on creation)
- **Lifecycle:** create, then publish. Created via the `WidgetCatalogItem.Create(sku, name)`
  factory, which returns a `Result` and is the only way to build an item.
- **Invariants:**
  - The SKU is always valid (see value object) and the name is never blank and never longer
    than 100 characters.
  - A published widget cannot be published again (`Widget.AlreadyPublished`, a conflict).
  - SKUs are unique across the catalogue (enforced by the application layer plus a unique
    index; the aggregate cannot see other aggregates).
- **Value object:** `Sku` - trimmed and upper-cased on construction, then must be 3 to 20
  characters, each an ASCII letter, digit or hyphen; otherwise `Widget.InvalidSku`. Value
  equality.
- **Domain event:** `WidgetPublished(WidgetId, Sku)` - raised once, when an unpublished
  widget becomes published. Stays inside the module (dispatched before commit); the
  Application layer maps it to the `WidgetPublishedIntegrationEvent` and writes that to the
  outbox in the same transaction.

## Acceptance criteria

1. **AC-0001-01** - Given no existing widget, when a widget is created with SKU `" wgt-001 "`
   and name `"Standard widget"`, then it is stored with SKU `WGT-001`, unpublished, and the
   creation call returns the new widget id.
2. **AC-0001-02** - Given a SKU that is invalid (blank, shorter than 3 or longer than 20
   characters, or containing characters other than letters, digits and hyphens) or a name
   that is empty, whitespace-only, or 101 characters long, when a widget is created, then
   creation fails with a validation error (`400` over HTTP) and nothing is stored. A name of
   exactly 100 characters succeeds.
3. **AC-0001-03** - Given an existing widget with SKU `WGT-001`, when another widget is created
   with SKU `wgt-001`, then creation fails with a conflict error (`409` over HTTP) and the
   catalogue still contains exactly one widget with that SKU. The rule is enforced by the
   create handler and, as a backstop, by a unique index on the SKU.
4. **AC-0001-04** - Given an unpublished widget, when it is published, then `IsPublished`
   becomes `true` and exactly one `WidgetPublished` domain event is raised carrying the widget
   id and SKU. Publishing the same widget again fails with a conflict (`409`) and raises no
   further event. Publishing an unknown id fails with not found (`404`).
5. **AC-0001-05** - Given a widget is published, then a `WidgetPublishedIntegrationEvent` is
   written to the module's outbox in the same transaction and delivered after commit, exactly
   once per publish. If delivery fails, the outbox row stays pending and the startup sweep
   delivers it later; publishing an already-published widget writes no second outbox row.
6. **AC-0001-06** - Given widgets exist, when a client requests a widget by id, then it
   receives that widget as a `WidgetDto`; an unknown id yields not found (`404` with a
   problem-details body). When a client requests the list, it receives every widget as
   `WidgetDto`s ordered by SKU ascending. The frontend renders the list (with an empty state)
   and the detail view, including a "not found" message for `404`.

## API / contract

All routes are under `/api/sample/widgets`. Bodies are JSON; errors use RFC 9457 problem
details whose `title` is the domain error code (for example `Widget.DuplicateSku`).

| Method and route | Request | Success | Errors |
|---|---|---|---|
| `POST /api/sample/widgets` | `{ "sku": string, "name": string }` | `201`, `Location` header, `{ "id": guid }` | `400` validation (AC-02), `409` duplicate SKU (AC-03) |
| `POST /api/sample/widgets/{id}/publish` | none | `204` | `404` unknown id, `409` already published (AC-04) |
| `GET /api/sample/widgets` | none | `200`, `[{ id, sku, name, isPublished }]` ordered by SKU | none |
| `GET /api/sample/widgets/{id}` | none | `200`, `{ id, sku, name, isPublished }` | `404` (AC-06) |

`Sample.Contracts` publishes:

- `WidgetDto(Guid Id, string Sku, string Name, bool IsPublished)` - the read model.
- `CreateWidgetRequest(string Sku, string Name)` - the create request body.
- `ISampleQueries` - `GetWidgetAsync(Guid id)` and `ListWidgetsAsync()`, both returning
  `Result<T>` (`Result<WidgetDto>` and `Result<IReadOnlyList<WidgetDto>>`).
- `WidgetPublishedIntegrationEvent(Guid EventId, DateTimeOffset OccurredOn, Guid WidgetId, string Sku)`
  - delivered through the transactional outbox (AC-05).

## Data and migration impact

- New schema `sample` with table `widgets` (`Id` uuid primary key, `Sku` text(20) not null,
  `Name` text(100) not null, `IsPublished` boolean not null), plus the module's outbox and
  inbox tables.
- Unique index on `Sku`.
- Migrations history table lives in the same schema (`sample.__EFMigrationsHistory`).
- Greenfield: no data to migrate. Removing the module later means dropping the `sample`
  schema in the same change that supersedes this spec.

## Non-functional requirements

- Unit tests for Domain and Application run without Docker and in under 5 seconds total.
- Coverage on `Sample.Domain` and `Sample.Application` meets the 85% gate (ADR-0014).
- List endpoint is unpaginated; acceptable only because this is a demonstration module.
- No personal data is stored.

## Observability

- Standard request logging, traces and metrics from `Balboa.ServiceDefaults`.
- Each command handler runs inside the common logging decorator; no module-specific metrics.

## Feature flag

None. The module is a skeleton demonstrator and is not exposed to any user.

## Rollout and rollback

Ships with the skeleton. Rollback is a normal revert of the PR; because there is no deployed
data, no data rollback is needed. Replacement by the first real context follows the "Adding a
module" recipe in `CLAUDE.md`.

## Open questions

- None for this placeholder. Question for the future: whether Sample is deleted outright or
  kept as a reference fixture for the architecture tests when the first real context lands.
