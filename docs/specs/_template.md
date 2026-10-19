---
id: SPEC-NNNN
title: <Short, noun-phrase title>
status: Draft # Draft | Accepted | Implemented | Superseded
module: <Context name, or "cross-cutting">
adrs: [] # e.g. [ADR-0002, ADR-0004]
owner: <github-handle>
created: YYYY-MM-DD
---

# SPEC-NNNN: <Title>

> Copy this file to `docs/specs/SPEC-NNNN-<slug>/spec.md`. Write the spec before any code.
> Acceptance criteria IDs (`AC-NNNN-01`, ...) are referenced by tests: `[Spec("AC-NNNN-01")]`
> in .NET and a `[AC-NNNN-01]` title prefix in Vitest/Playwright. `make spec-check` fails if a
> criterion has no test, or a tag names a criterion that does not exist. Delete these hints
> when filling in the spec.

## Context / Problem

What problem exists, for whom, and why now? Link related specs and ADRs.

## Goals

- <Observable outcome this spec delivers>

## Non-goals

- <Thing that is explicitly out of scope, and why>

## Ubiquitous language

Terms used by this spec. Each must also exist in [`docs/glossary.md`](../glossary.md).

| Term | Meaning |
|---|---|
| <Term> | <Definition in the language of the domain> |

## Domain model sketch

Aggregates, entities, value objects, invariants and domain events. A short table or list is
enough; link a diagram if one helps.

- **Aggregate:** <name> - <one line>
- **Invariants:** <rules that must always hold>
- **Value objects:** <names>
- **Domain events:** <names, raised when>

## Acceptance criteria

Number each criterion `AC-NNNN-01`, `AC-NNNN-02`, ... in Given/When/Then form. Each must be
concrete enough to be proven by one automated test.

1. **AC-NNNN-01** - Given <state>, when <action>, then <observable outcome>.
2. **AC-NNNN-02** - Given <state>, when <action>, then <observable outcome>.

## API / contract

HTTP endpoints, request/response shapes, error codes, and any published `.Contracts` query
interfaces or integration events. State `n/a` if there is none.

## Data and migration impact

Schema, tables, indexes, migrations, backfills. Note anything that is not backwards
compatible with the previous deployed version.

## Non-functional requirements

Performance, security, accessibility, scalability, cost limits. State measurable targets.

## Observability

Logs, metrics, traces and alerts this change adds or relies on.

## Feature flag

Flag name and default, or `none` with a reason. Unfinished work merges to `main` behind a
flag (see ADR-0012).

## Rollout and rollback

How the change reaches users, how it is verified, and how to undo it.

## Open questions

- <Question, owner, and what is blocked until it is answered>
