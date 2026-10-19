# ADR-0009: GitHub Actions authenticates via OIDC on user-assigned managed identities

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

CI/CD must deploy to Azure without long-lived secrets (a non-negotiable in `CLAUDE.md`).
GitHub Actions can exchange its OIDC token for an Azure token via a federated credential.
Federated credentials can be attached to either an Entra **app registration** or a
**user-assigned managed identity**. App registrations live in Entra ID and need directory
permissions to create; managed identities are ordinary Azure resources and can be created
with subscription rights alone, and defined in Bicep.

## Decision drivers

- No stored credentials or client secrets.
- Creatable with subscription-level rights, without Entra directory roles.
- Defined as code in Bicep and visible with the rest of the infrastructure.
- Least privilege and blast-radius separation between environments and purposes.

## Considered options

1. Service principal with a client secret stored in GitHub secrets.
2. Entra app registration with a federated credential.
3. User-assigned managed identities with federated credentials.

## Decision

Use **user-assigned managed identities with GitHub OIDC federated credentials**, not an Entra
app registration. Identities are separated by purpose and scoped narrowly (for example one
for the shared layer, one per environment for deploy). Each federated credential is bound to
a specific subject, such as a GitHub **environment** (`repo:zh-8/balboa:environment:dev`),
so a branch or fork cannot mint a token. Workflows request `id-token: write` and use
`azure/login` with client ID, tenant ID and subscription ID, none of which are secret.

RBAC assignments are minimal and made in Bicep or by the bootstrap step. See the bootstrap
and rotate-identity runbooks.

## Consequences

### Good

- No secret exists to leak or rotate on a schedule.
- No Entra directory permission is needed; subscription Owner or equivalent suffices.
- Identities are Bicep resources and reviewable in PRs.
- Subject binding limits which workflow context can authenticate.

### Bad

- Bootstrap must still be run once by a human with enough rights to create role
  assignments (high-risk, needs explicit approval; see the bootstrap runbook).
- A managed identity has a limited number of federated credentials (20 at time of writing),
  and subject matching is exact, so many environments or branches need planning.
- Misconfigured subjects cause opaque `AADSTS` errors that are hard to debug.
- Compromise of a GitHub environment with approval gates removed is equivalent to
  compromise of that identity.

## Revisit trigger

Reopen if we exceed the federated-credential limit, if we need Entra directory operations
from CI (which managed identities cannot do), or if GitHub or Azure change the OIDC claim
format or recommend a different mechanism.
