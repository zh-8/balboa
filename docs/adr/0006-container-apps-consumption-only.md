# ADR-0006: Azure Container Apps, Consumption-only environment, scale-to-zero

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We need to host a .NET container for roughly zero cost while the product is undefined. Azure
Container Apps offers a monthly free grant of 180,000 vCPU-seconds, 360,000 GiB-seconds and
2 million requests on the Consumption plan. A Container Apps environment can be created as
Consumption-only or with workload profiles. Workload-profile environments add a management
fee of about **$0.13 per hour, roughly $95 per month**, regardless of use. That fee is the
trap this ADR avoids, because it would dwarf every other cost in the project.

## Decision drivers

- Dev cost must be about $0 per month.
- Traffic is near zero, so idle compute must not be billed.
- A single owner must run the platform without Kubernetes expertise.
- The cost trap must be impossible to hit by accident.

## Considered options

1. Azure App Service (free or basic tier).
2. Azure Kubernetes Service.
3. Container Apps, workload-profile environment.
4. Container Apps, Consumption-only environment, scale to zero.
5. Azure Functions.

## Decision

Use **Azure Container Apps in a Consumption-only environment** with **scale-to-zero**
(`minReplicas: 0`). The environment must not define workload profiles. The Bicep module
encodes this and a comment links to this ADR; changing it requires superseding this ADR.

The migrator runs as a Container Apps job in the same environment (ADR-0005).

## Consequences

### Good

- Expected compute cost is within the free grant, so about $0 per month.
- Scale-to-zero means an idle environment bills almost nothing.
- Revisions and traffic splitting give simple rollback (see the deploy-rollback runbook).
- Standard container image keeps us portable.

### Bad

- Cold starts: the first request after idle waits for a replica to start (seconds for .NET).
- Scale-to-zero makes background pollers unreliable, which shapes the outbox design
  (ADR-0004).
- Consumption-only environments cannot use dedicated or GPU workload profiles.
- Free-grant figures are Azure's and may change; they are not guaranteed.
- A future requirement for workload profiles (for example reserved capacity) would add the
  management fee, and recreating the environment is disruptive.

## Revisit trigger

Reopen if cold-start latency is unacceptable for real users, if sustained traffic exceeds the
free grant such that dedicated capacity is cheaper, or if a feature genuinely needs a
workload profile. Re-check Azure's pricing page before any production decision.
