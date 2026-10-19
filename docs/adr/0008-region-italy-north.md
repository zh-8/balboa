# ADR-0008: Region Italy North, with documented fallbacks

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

The owner and likely first users are in Cairo. Latency to the API matters for interactive
use. Not every Azure region offers both Container Apps and PostgreSQL Flexible Server, and
some regions restrict new subscriptions.

## Decision drivers

- Lowest practical latency from Cairo.
- Availability of both Container Apps and Postgres Flexible Server.
- Ability to create resources on a new or free-tier subscription.
- One region for the whole stack, to avoid cross-region data transfer.

## Considered options

1. West Europe (Netherlands): mature, widely available.
2. Germany West Central (Frankfurt).
3. Italy North (Milan).
4. A Middle East region such as UAE North or Qatar Central.

## Decision

Use **Italy North** (short code `itn`) as the primary region: the closest region to Cairo
that offers both Container Apps and PostgreSQL Flexible Server. Fallback order if Italy North
is unavailable for a resource or a quota: **Germany West Central**, then **West Europe**.
Static Web Apps is hosted in West Europe (ADR-0007).

The region is a Bicep parameter and a component of resource names (ADR-0017), so a fallback
changes the suffix, for example `-gwc`, and is not a silent edit.

## Consequences

### Good

- Lower latency to Cairo than Frankfurt or Amsterdam.
- Single-region deployment keeps data transfer and complexity down.
- The fallback order is decided in advance, so a quota failure is not a design discussion.

### Bad

- Italy North is a newer region with fewer features and a thinner capacity margin than West
  Europe; quota or SKU availability can block a deployment.
- Region-specific naming (`itn`) means a region move is a rename and a data migration, not a
  config change.
- Some preview features arrive in Italy North later than in older regions.

## Revisit trigger

Reopen if a required service or SKU is unavailable in Italy North, if a Middle East region
offering both services becomes available and closer, or if measured latency from real users
is poor.
