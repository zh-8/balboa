# ADR-0013: Spec-driven development with enforced spec-to-test traceability

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

AI agents implement what they are told. When intent lives only in a chat or a PR description,
it is lost, and tests tend to describe what the code does rather than what was wanted.
Written, numbered acceptance criteria give humans and agents a shared definition of done. But
specs that are not connected to tests rot into fiction.

## Decision drivers

- Intent must be written down before code and survive the session.
- Every requirement must be provably tested, mechanically.
- Dead or misspelt references must be caught automatically.
- Low overhead: a template and a checker, not a requirements tool.

## Considered options

1. Ad hoc issues and PR descriptions.
2. Specs as prose with no link to tests.
3. Specs with numbered criteria plus a CI-enforced traceability check.
4. A full BDD framework (SpecFlow or Reqnroll with Gherkin files).

## Decision

Adopt **spec-driven development**:

- Every non-trivial change starts with `docs/specs/SPEC-NNNN-slug/spec.md`, from
  `docs/specs/_template.md`, with front matter and numbered acceptance criteria
  `AC-NNNN-01` in Given/When/Then form.
- Tests carry the criterion they prove: `[Spec("AC-0001-02")]` in .NET, and a
  `[AC-0001-02]` title prefix in Vitest and Playwright.
- `tools/spec-check` (run as `make spec-check`, and in CI) fails if an `Accepted` or
  `Implemented` criterion has no tagged test, or if a tag names a criterion that does not
  exist.
- Spec status moves Draft, Accepted, Implemented, Superseded. Specs are updated in the same
  PR as behaviour changes.

We chose plain Markdown and attributes over Gherkin to avoid a second language and runner.

## Consequences

### Good

- Requirements, tests and code are linked by ID and checked mechanically.
- Agents get unambiguous, testable instructions.
- A removed requirement shows up as an orphan tag and is cleaned up.

### Bad

- Traceability proves a test exists with the tag, not that the test is good. A tag on a weak
  test passes the checker.
- Writing a spec first is overhead on small changes; "non-trivial" is a judgement call.
- Criteria renumbering breaks tags, so IDs must never be reused or reordered.
- Custom tooling in `tools/spec-check` is ours to maintain.

## Revisit trigger

Reopen if the checker produces frequent false results, if the spec overhead causes
trivial changes to be skipped, or if a stakeholder who is not a developer needs to author
criteria, which would favour a Gherkin-based tool.
