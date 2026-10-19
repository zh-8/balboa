---
id: SPEC-0001
title: Sample module - widget catalogue
status: Accepted
module: Sample
adrs: [ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0013, ADR-0014]
owner: zh-8
created: 2026-10-19
---

# SPEC-0001: Sample module - widget catalogue

> **This module is a placeholder.** It exists only to demonstrate the module pattern
> (Contracts / Domain / Application / Infrastructure, schema per module, domain events,
> outbox, spec-to-test traceability). It has no product meaning. When the first real bounded
> context arrives it **replaces** Sample, and this spec moves to `Superseded`. Do not add
> features to it and do not build on it as if it were real.

## Context / Problem

Balboa's product domain is undefined, but the architecture needs a working vertical slice so
that the tooling (boundary enforcement, migrations, Testcontainers, `make spec-check`) is
proven before real work starts. Sample provides that slice with a deliberately generic
catalogue-item aggregate.

## Goals

- Demonstrate one aggregate with an invariant, a value object and a domain event.
- Demonstrate command and query handlers, a minimal API and a schema owned by the module.
- Give backend and frontend tests concrete criteria to tag with `AC-0001-NN`.

## Non-goals

- Any real product behaviour, pricing, inventory or ordering.
- Authentication and authorization rules (not defined yet; endpoints are unauthenticated in
  this slice).
- Integration events to other modules. No second module exists; the outbox is exercised by
  the BuildingBlocks tests, not by Sample.
- Deleting items. Items are retired, never removed.

## Ubiquitous language

| Term | Meaning |
|---|---|
| Widget catalogue item | An entry in the catalogue describing one kind of widget. |
| Item code | Human-readable unique identifier of an item, e.g. `WGT-001`. |
| Name | Display name of the item, 1 to 100 characters after trimming. |
| Active | Item is visible as available. Initial state on creation. |
| Retired | Item is no longer offered. Terminal state; it cannot be re-activated. |

These terms are placeholders and are intentionally not added to `docs/glossary.md`.

## Domain model sketch

- **Aggregate root:** `WidgetCatalogItem` - identified by a `Guid` id.
  - `Code` (`ItemCode` value object)
  - `Name` (string, trimmed, 1 to 100 characters)
  - `Status` (`Active` | `Retired`)
- **Invariants:**
  - The name is never blank and never longer than 100 characters.
  - A retired item stays retired (no transition back to `Active`).
  - Item codes are unique across the catalogue (enforced by the application layer plus a
    unique index; the aggregate cannot see other aggregates).
- **Value object:** `ItemCode` - upper-case, matches `^WGT-[0-9]{3,6}$`. Input is trimmed and
  upper-cased on construction; value equality.
- **Domain event:** `WidgetCatalogItemRetired(ItemId, OccurredOn)` - raised once, when an
  active item becomes retired. Stays inside the module (dispatched before commit).

## Acceptance criteria

1. **AC-0001-01** - Given no existing item, when a widget item is created with code
   `" wgt-001 "` and name `"Standard widget"`, then it is stored with code `WGT-001`, status
   `Active`, and the creation call returns the new item id.
2. **AC-0001-02** - Given a name that is empty, whitespace-only, or 101 characters long, when
   a widget item is created, then creation fails with a validation error naming the `name`
   field and nothing is stored. A name of exactly 100 characters succeeds.
3. **AC-0001-03** - Given a code that does not match `^WGT-[0-9]{3,6}$` after trimming and
   upper-casing (for example `"WGT-12"` or `"GADGET-001"`), when a widget item is created,
   then creation fails with a validation error naming the `code` field.
4. **AC-0001-04** - Given an existing item with code `WGT-001`, when another item is created
   with code `wgt-001`, then creation fails with a conflict error and the catalogue still
   contains exactly one item with that code.
5. **AC-0001-05** - Given an `Active` item, when it is retired, then its status becomes
   `Retired` and exactly one `WidgetCatalogItemRetired` domain event is raised carrying the
   item id; retiring the same item again is a no-op that raises no further event and does
   not fail.
6. **AC-0001-06** - Given a catalogue with items, when a client requests the list of items
   over HTTP (`GET /sample/widgets`), then it receives `200` with every item ordered by code
   ascending; and when it requests `GET /sample/widgets/{id}` for an unknown id, it receives
   `404` with a problem-details body. The frontend shows the same list ordering, with an
   empty state when the list is empty.

## API / contract

All routes are under `/sample/widgets`. Bodies are JSON; errors use RFC 9457 problem details.

| Method and route | Request | Success | Errors |
|---|---|---|---|
| `POST /sample/widgets` | `{ "code": string, "name": string }` | `201`, `Location` header, `{ "id": guid }` | `400` validation (AC-02, AC-03), `409` duplicate code (AC-04) |
| `POST /sample/widgets/{id}/retire` | none | `204` (also when already retired, AC-05) | `404` unknown id |
| `GET /sample/widgets` | none | `200`, `[{ id, code, name, status }]` ordered by code | none |
| `GET /sample/widgets/{id}` | none | `200`, `{ id, code, name, status }` | `404` |

`Sample.Contracts` publishes only DTO records for the read side (`WidgetCatalogItemDto`) and
an `IWidgetCatalogQueries` interface. It publishes no integration events.

## Data and migration impact

- New schema `sample` with table `widget_catalog_items` (`id uuid pk`, `code text not null`,
  `name text not null`, `status text not null`).
- Unique index on `code`.
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
