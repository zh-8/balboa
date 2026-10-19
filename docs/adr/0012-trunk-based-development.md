# ADR-0012: Trunk-based development

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Balboa is built by one owner with AI agents that can produce large changes quickly. Large
branches and long-lived divergence make review and integration harder, and agents tend to
produce sprawling diffs unless constrained. We want `main` to be releasable at all times and
a history that reads as a changelog.

## Decision drivers

- Small, reviewable, quickly integrated changes.
- `main` always releasable, so deploys are routine.
- A linear history that can drive versioning and release notes.
- Unfinished work must be mergeable without being exposed.

## Considered options

1. GitFlow with develop, release and hotfix branches.
2. GitHub Flow with merge commits.
3. Trunk-based development with squash merges and feature flags.

## Decision

Use **trunk-based development**:

- Short-lived branches (target under a day or two) with small PRs into `main`.
- **Squash merge only**; **linear history**; no merge commits. Never force-push `main`.
- **Conventional commits** (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`, `ci:`,
  `infra:`). The PR title becomes the squash commit message and is linted.
- Unfinished work lands behind a **feature flag**, never on a long-lived branch.
- Releases are tagged with **CalVer** (`YYYY.MM.DD` with a `.N` suffix for additional
  releases the same day, for example `2026.10.19.1`). A tag marks a deployable commit on
  `main`.
- The required status check `ci-ok` must be green to merge.

## Consequences

### Good

- Integration problems surface within hours, not weeks.
- Squash plus conventional titles give a clean, machine-readable history.
- Feature flags decouple deploy from release.
- Rollback is a revert or a revision switch, not a branch dance.

### Bad

- Feature flags add code paths that must be tested and later removed; stale flags are debt.
- Squashing loses the intermediate commit history within a PR, including the red/green TDD
  trail unless the PR description preserves it.
- Requires a trustworthy CI gate; a weak gate makes an always-releasable `main` untrue.
- CalVer says when a release was cut, not whether it contains breaking changes.

## Revisit trigger

Reopen if multiple maintainers need to release independent versions, if consumers need
semantic versioning guarantees (for example a published library or public API), or if flag
debt becomes unmanageable.
