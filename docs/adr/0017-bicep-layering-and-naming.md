# ADR-0017: Bicep layering and resource naming convention

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

All infrastructure is code (non-negotiable in `CLAUDE.md`). The subscription also hosts an
unrelated project in `rg-cairobits`, which must never be touched. We need a structure that
separates one-time privileged setup from routine CI-applied infrastructure, keeps
environments isolated, and makes resource names predictable and traceable to Balboa.

## Decision drivers

- Privileged one-time setup must be separate from routine, CI-applied infrastructure.
- A destroy or deploy must be scoped so it cannot reach `rg-cairobits`.
- Names must reveal type, project, environment and region.
- Resources must be attributable and filterable by tag.

## Considered options

1. One flat Bicep template for everything.
2. Terraform with remote state.
3. Layered Bicep: bootstrap, shared, per-environment, with reusable modules.

## Decision

Use **Bicep**, in layers:

| Layer | Location | Applied by | Scope |
|---|---|---|---|
| Bootstrap | `infra/bootstrap/` | A human, once | Subscription: resource groups, identities, role assignments |
| Shared | `infra/bicep/` shared stack | CI | `rg-balboa-shared-itn` |
| Environment | `infra/bicep/` per-env stack | CI | `rg-balboa-<env>-itn` |
| Modules | `infra/bicep/modules/` | Called by the above | n/a |

Resource groups are `rg-balboa-shared-itn` and `rg-balboa-dev-itn`. CI identities have
rights only on `rg-balboa-*` groups, never at subscription scope. Compiled `infra/bicep/*.json`
is generated and gitignored.

**Naming:** `{type}-balboa-{env}-itn`, for example `ca-balboa-api-dev-itn`, where `type` is
the Azure abbreviation (`rg`, `ca`, `cae`, `psql`, `id`, `log`). Globally unique names get a
4-character `uniqueString` suffix. A workload qualifier may follow the type, as in
`ca-balboa-api-dev-itn`.

**Required tags:** `app=balboa`, `env=<env>`, `owner=zh-8`, `managedBy=bicep`.

## Consequences

### Good

- The riskiest operation (subscription-scope setup) is isolated, rare and human-run.
- CI cannot affect anything outside `rg-balboa-*`, which protects `rg-cairobits`.
- Names and tags make cost and ownership queries trivial.
- `make infra-lint` builds and lints Bicep without Azure login.

### Bad

- Bootstrap is a manual step that is not covered by CI and can drift unnoticed.
- The shared layer couples environments through its outputs.
- Some resources (Static Web App in West Europe, ADR-0007) break the `-itn` suffix.
- Name length limits (for example storage accounts, Postgres servers) may force
  abbreviations that diverge from the pattern.
- Bicep is Azure-only; moving clouds means a rewrite.

## Revisit trigger

Reopen if a second cloud or non-Azure resource is needed, if bootstrap drift causes an
incident (consider a what-if check in CI), or if the number of environments makes the layering
cumbersome.
