# ADR-0010: Entra-only authentication for Postgres, no passwords, separate migrator identity

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

PostgreSQL Flexible Server supports password authentication, Microsoft Entra authentication,
or both. Passwords would need to be stored somewhere (Key Vault, environment variables, CI)
and rotated. `CLAUDE.md` forbids secrets in git. Container Apps can present a managed
identity to Entra, which issues short-lived access tokens usable as the Postgres password.

## Decision drivers

- No connection-string password anywhere.
- Least privilege: the running app must not be able to alter schema.
- Short-lived credentials rather than rotated long-lived ones.
- Auditable identity per workload.

## Considered options

1. Password authentication with a secret in Key Vault or Container Apps secrets.
2. Both password and Entra authentication enabled.
3. Entra-only authentication, one identity for everything.
4. Entra-only authentication, separate identities for the app and the migrator.

## Decision

Configure the server for **Entra-only authentication** (password authentication disabled).
Use two user-assigned managed identities:

| Identity | Used by | Database rights |
|---|---|---|
| App identity | API container | DML on module schemas only (select, insert, update, delete); no DDL |
| Migrator identity | `migrate` Container Apps job | DDL on module schemas, applying migrations |

The application obtains an Entra token at connection time and uses it as the password; the
connection string contains the host, database and identity name, and no secret. A human
Entra admin is configured on the server for break-glass use and for creating the database
roles mapped to the identities.

Local development uses a Postgres container with a throwaway local password that never
leaves the developer machine and is not used anywhere else.

## Consequences

### Good

- Nothing to leak, rotate or store: no password exists for the deployed database.
- A compromised API container cannot change the schema.
- Tokens expire quickly and every connection is attributable to an identity.

### Bad

- Setup is more involved: mapping identities to Postgres roles is a manual-looking step that
  must be scripted and is easy to get wrong.
- Token acquisition adds connection-time dependency on Entra; an Entra outage blocks new
  connections, and connection pools must refresh tokens before they expire.
- Local and cloud authentication paths differ, so the cloud path is only exercised after
  deployment.
- Tools that only support passwords (some GUI clients, backup tooling) need an Entra token
  workaround.

## Revisit trigger

Reopen if token refresh causes production connection errors we cannot fix in the connection
layer, if a required tool cannot authenticate with Entra, or if the migrator and app
identities prove too coarse for a module-level permission model.
