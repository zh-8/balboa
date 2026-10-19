# ADR-0020: Spec and implementation reconciliation

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

SPEC-0001 (the Sample module) was written in parallel with the backend and frontend by
separate agents. The result diverged: the spec described an item code with an Active and
Retired lifecycle, while the code built a SKU with a create-then-publish lifecycle, and the
frontend guessed a third shape. The backend was implemented, tested (70 tests) and verified.
Because `tools/spec-check` only checks that criterion IDs exist, the divergence passed
mechanically while tests vouched for rules they did not exercise.

ADR-0013 says the spec is written first. Here, following that rule literally would mean
rewriting verified code to rename concepts in a module that is an explicitly disposable
placeholder.

## Decision drivers

- Do not spend effort rewriting verified code to satisfy a spec of a throwaway module.
- Keep "spec first, spec wins" as the rule, not an optional guideline.
- Tags must be true: a test may only claim a criterion it exercises.
- Make the exception visible so it is not copied.

## Considered options

1. Change the code to match the original spec.
2. Reconcile the spec to the implementation, once, and record why.
3. Leave both as they are.

## Decision

We chose **to reconcile SPEC-0001 to the implementation, exactly once** (on 2026-10-19),
because the module is a placeholder and the code was the verified artefact. The spec now
carries a note saying so, frontend tests were retagged so each `[AC-...]` tag is true, and
plumbing tests carry no tag.

This is an exception, not a precedent. The normal rule stands: the spec is written first and
wins. Any future divergence between a spec and its implementation is resolved by changing the
code (or by an explicit, reviewed spec change made before the code), never by rewriting the
spec to match after the fact.

## Consequences

### Good

- A single consistent description of what the Sample module does.
- Traceability tags are honest; criteria with only backend coverage are shown as such.
- The exception is documented, bounded and visible in the spec itself.

### Bad

- It weakens the claim that specs lead, and could be cited as precedent by mistake.
- `tools/spec-check` still cannot detect a tag on a test that does not exercise the rule;
  that remains a review responsibility.

## Revisit trigger

Any future divergence between a spec and its implementation. It must be resolved by changing
the code, and a spec-to-code reconciliation like this one requires a new ADR.
