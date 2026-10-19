# ADR-0001: Record decisions in MADR-format ADRs

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Balboa is a greenfield project built by one owner working with AI agents. Agents start each
session without memory of earlier reasoning, and a solo owner forgets why a choice was made
within months. Several decisions here are cost traps that look wrong in isolation (no VNet,
ghcr.io instead of ACR, Consumption-only environment). Without a written reason, a
well-meaning change could undo them.

## Decision drivers

- Reasoning must survive across agent sessions and across time.
- Low ceremony: one file per decision, reviewable in a normal PR.
- Decisions must be diffable and live next to the code.
- A trade-off must be recorded together with the trigger that reopens it.

## Considered options

1. No formal record; rely on commit messages and README prose.
2. A wiki or Notion page outside the repository.
3. Nygard-style ADRs (Context, Decision, Consequences).
4. MADR (Markdown Architectural Decision Records) in `docs/adr/`.

## Decision

Use **MADR-style ADRs** in `docs/adr/`, numbered `NNNN-slug.md`, from
[`_template.md`](_template.md). MADR adds explicit decision drivers and considered options
to the Nygard form, which forces the rejected alternatives to be written down. We add one
section, "Revisit trigger", so each decision carries its own expiry condition.

Rules:

- One decision per ADR. Architecture-significant or hard-to-reverse choices need an ADR.
- Accepted ADRs are immutable. To change one, write a new ADR and set the old status to
  `Superseded by ADR-NNNN`. That status line is the only permitted edit.
- The ADR is written before or in the same PR as the change it justifies.

## Consequences

### Good

- Rejected options and cost traps are documented where agents read them.
- Immutability gives an honest history, including decisions that later proved wrong.
- Plain Markdown is reviewable in PRs and lintable.

### Bad

- Writing costs time, which is a real cost for a solo project.
- Immutability means finding the current answer can require following a supersession chain.
- Nothing mechanically enforces that an ADR exists; this depends on review discipline.

## Revisit trigger

Reopen if ADRs are routinely written after the fact, or if more than a few supersession
chains make the current state hard to find. At that point add an index with a "current
decision" column, or adopt tooling such as adr-tools or log4brains.
