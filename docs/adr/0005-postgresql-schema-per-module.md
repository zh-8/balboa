# ADR-0005: PostgreSQL with one schema per module, EF Core, migrations by a dedicated job

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Modules must own their data (ADR-0002) yet share one operational database to stay within the
free allowance of a single Postgres Flexible Server (ADR-0006). Schema changes must be
applied safely, and a scale-to-zero app that may run several replicas at once must not race
to migrate on startup.

## Decision drivers

- Data ownership per module without paying for several servers.
- Cross-module coupling through the database must be impossible by design.
- Migrations must run exactly once per deploy, with a different identity from the app.
- Tests must exercise the real migrations on a real Postgres.

## Considered options

1. One shared schema and `DbContext` for all modules.
2. One database per module on the same server.
3. One schema per module in one database, one `DbContext` per module.
4. One server per module.

## Decision

Use **PostgreSQL 17**, one database, **one schema per module**, one EF Core `DbContext` per
module with its own migrations history table inside that schema. There are **no cross-schema
foreign keys and no cross-schema joins**; modules reference each other by ID only, and read
each other through Contracts query interfaces (ADR-0004).

**Migrations are applied by a dedicated job**, not on app startup: the Host exposes a
`migrate` command, run as a Container Apps job using a separate migrator identity
(ADR-0010) before the new revision receives traffic. Apps never hold DDL rights.

## Consequences

### Good

- One server fits the free allowance; ownership is still visible in the schema name.
- Banning cross-schema joins keeps a later per-module extraction feasible.
- No replica race on migrations, and the app's database role can be least-privilege.
- Integration tests apply the same migrations that production will.

### Bad

- A shared database is a shared failure domain and a shared noisy-neighbour risk.
- No foreign keys across modules means referential integrity between modules is eventual,
  and orphaned references must be tolerated or cleaned up by events.
- Reporting that needs data from several modules cannot use a simple join.
- The migration job is an extra deploy step and an extra thing that can fail.
- Schema separation is a convention backed by an architecture test, not a database-enforced
  wall; a role with broad grants could still cross it.

## Revisit trigger

Reopen if a module's load or availability needs to be isolated from the others, if a
reporting requirement needs cross-module joins (introduce a read model rather than relaxing
the rule), or if Postgres free-tier terms change.
