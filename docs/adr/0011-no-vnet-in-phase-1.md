# ADR-0011: No VNet or private endpoints in phase 1

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

The textbook secure layout puts Postgres behind a private endpoint inside a VNet. A private
endpoint costs about **$7 per month each**, and VNet integration for Container Apps adds
configuration, and for some options a workload-profile environment, which is the $95/month
trap in ADR-0006. Phase 1 holds no real user data and the budget target is about $0. The
database is reachable over the public endpoint, protected by Entra-only authentication
(ADR-0010) and TLS.

## Decision drivers

- Cost: each private endpoint is a recurring charge on a near-zero budget.
- Avoiding any change that pushes the environment into the workload-profile fee.
- Phase 1 stores no real user data and no regulated data.
- Honest risk acceptance rather than pretending the public endpoint is equivalent.

## Considered options

1. VNet integration plus private endpoints for Postgres and other services.
2. Public endpoints with firewall rules, Entra-only authentication and TLS.
3. Public endpoints with no firewall restriction.

## Decision

In phase 1, use **no VNet and no private endpoints**. Postgres is on its public endpoint
with:

- Entra-only authentication, no passwords (ADR-0010) and TLS required.
- A firewall that allows only the Azure services rule needed for Container Apps, and no
  other broad ranges; developer IPs are added temporarily and removed after use.
- Least-privilege database roles (ADR-0010) so a compromised app cannot alter schema.
- No real user data stored while in this configuration.

**Open question:** whether the Azure-services firewall rule can be narrowed to the
Container Apps outbound IPs without a VNet; to be checked when Azure access is restored.

## Consequences

### Good

- Saves about $7 per month per private endpoint and avoids VNet complexity.
- Keeps the Consumption-only environment (ADR-0006) intact.
- Simpler operations and fewer moving parts to debug for a solo owner.

### Bad

- The database is reachable from the public internet; the only barriers are the firewall,
  TLS and Entra authentication. The "allow Azure services" rule admits traffic from any
  Azure tenant's resources, which is a weaker control than network isolation.
- Does not meet compliance regimes that require private connectivity.
- Adding a VNet later is a significant infrastructure change, possibly an environment
  recreation, not a toggle.

## Revisit trigger

Reopen **before** the first real user data is stored, or when a compliance requirement
appears (whichever comes first). The trigger is binary: no real user data lands in this
database until this ADR is superseded by one that adopts private connectivity or consciously
accepts the risk again.
