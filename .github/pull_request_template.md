<!--
Keep PRs small and short-lived (ADR-0012). If this one has grown past a day's
work, consider splitting it and putting the unfinished part behind a feature flag.
-->

## What and why

<!-- One or two sentences. The "why" matters more than the "what" — the diff
     already shows the what. -->

## Spec

<!-- REQUIRED. CI checks that this line exists.
     Either a spec reference:   Spec: SPEC-0001
     or an explicit exemption:  Spec: n/a — dependency bump -->

Spec:

## Acceptance criteria covered

<!-- List the AC IDs this PR implements, e.g. AC-0001-03, AC-0001-04.
     `make spec-check` verifies each one has a test tagged [Spec("AC-...")]. -->

-

## Checklist

- [ ] A failing test came first, and it now passes
- [ ] Module boundaries respected — no cross-module import outside `*.Contracts`
- [ ] Docs updated in this PR, not a follow-up
- [ ] Diagrams re-exported (`make diagrams`) if any `.excalidraw` changed
- [ ] No secrets, keys or connection strings added
- [ ] Database migration is expand/contract (safe for the currently running revision)

## Risk

<!-- Delete the lines that do not apply. -->

- [ ] **Routine** — application code, tests, docs
- [ ] **High risk** — infrastructure, identity/RBAC, migrations, CI gates, or
      anything hard to reverse. Needs explicit approval before merge, and the
      rollback below must be filled in.

### Rollback

<!-- Required for high-risk changes. How is this undone, specifically? -->
