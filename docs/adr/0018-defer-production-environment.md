# ADR-0018: Production environment deliberately deferred; dev-only $10/month budget

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Balboa has no defined product domain, no users and no real data. The Azure subscription is
in `Warned` state: the free trial credit expired around 2026-10-15, the balance is $0 and the
spending limit is on, so resources cannot be created until the subscription is upgraded to
pay-as-you-go. The 12-month free-services window runs to 2027-10-15. Free allowances (for
example one Postgres B1ms for 750 hours per month) cover a single database server, so a
second environment with its own server would be billable.

## Decision drivers

- Do not pay for capacity that serves no one.
- The free Postgres allowance covers one B1ms server only.
- Avoid operating, securing and patching a production environment with nothing in it.
- Stay inside a hard budget while the domain is discovered.

## Considered options

1. Create dev and prod from the start.
2. Create dev only; build prod when the first real feature needs it.
3. Defer all Azure work until a product exists.

## Decision

Create **dev only**. **Production is deliberately deferred** until the first real feature.
The dev environment has a **$10 per month budget** with alerts; the expected cost after the
pay-as-you-go upgrade is about $0 per month, so the budget is a safety margin, not a plan.

Code and Bicep are written to be environment-parameterised, but no prod stack, prod
identities or prod DNS names are created. The planned prod hostnames
(`balboa.cairobits.com`, `api.balboa.cairobits.com`) are recorded but not provisioned.

Creating prod is a high-risk step that needs explicit approval and its own ADR or an update
that supersedes the relevant decisions (ADR-0011, ADR-0016).

## Consequences

### Good

- Costs stay at about $0 and the free Postgres allowance is not exceeded.
- Less infrastructure to secure and maintain while there is nothing to protect.
- Forces prod readiness decisions to be made with a real feature and real requirements.

### Bad

- Prod-specific concerns (private networking, backup retention, alerting, access review,
  scale settings) are untested until late, and prod may reveal issues dev does not.
- Dev is the only deployed environment, so it is also the only place to rehearse deploys and
  restores.
- Parameterised-but-never-instantiated prod Bicep can rot.
- The upgrade to pay-as-you-go removes the spending limit's protection, so the budget alert
  is the only cost guard.

## Revisit trigger

Reopen when the first real bounded context has a feature to ship to users, or when real user
data is about to be stored. Before creating prod, supersede or revisit ADR-0011 (networking)
and ADR-0016 (registry), and confirm the Postgres cost with the then-current pricing.
