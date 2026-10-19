# Runbook: Postgres point-in-time restore

> **Not yet exercised.** This describes the PLANNED Azure setup. No Postgres server exists
> yet (subscription in `Warned` state). Commands are unverified drafts; confirm flags with
> `az postgres flexible-server restore --help` on first use.

A restore creates a **new server**; it never overwrites the existing one. Database work is
high-risk and needs explicit approval.

Related: [ADR-0005](../adr/0005-postgresql-schema-per-module.md),
[ADR-0010](../adr/0010-entra-only-postgres-authentication.md),
[ADR-0018](../adr/0018-defer-production-environment.md).

## Important constraints

- Flexible Server keeps automated backups for a retention period (default 7 days; confirm the
  value set in Bicep). You can restore only to a moment within that window.
- Only one B1ms fits the free 750 hours per month. A second server running alongside the
  original is billable. Delete the restored server as soon as you are done (step 7).
- The restored server inherits Entra-only authentication, but its Entra administrators and
  database roles may need to be reconfigured.

## Steps

1. Decide the restore time (UTC) just before the damage occurred, and the new server name.

   ```bash
   RG=rg-balboa-dev-itn
   SRC=psql-balboa-dev-itn          # actual name carries a uniqueString suffix
   NEW=psql-balboa-dev-restore-itn
   WHEN=2026-11-01T09:30:00Z        # example, replace
   ```

2. Confirm the source server and that the time is inside the retention window.

   ```bash
   az postgres flexible-server show --resource-group "$RG" --name "$SRC" \
     --query "{state:state,retention:backup.backupRetentionDays,earliest:backup.earliestRestoreDate}" -o json
   ```

3. Restore to a new server.

   ```bash
   az postgres flexible-server restore \
     --resource-group "$RG" --name "$NEW" \
     --source-server "$SRC" --restore-time "$WHEN"
   ```

4. Make sure your Entra user is an administrator of the new server, then connect with an
   Entra token (no password exists).

   ```bash
   export PGPASSWORD="$(az account get-access-token \
     --resource-type oss-rdbms --query accessToken -o tsv)"
   psql "host=$NEW.postgres.database.azure.com dbname=balboa user=<entra-admin-upn> sslmode=require"
   ```

5. Inspect the data. Per-schema counts are a quick check.

   ```sql
   SELECT schemaname, count(*) FROM pg_tables
   WHERE schemaname NOT IN ('pg_catalog','information_schema') GROUP BY 1;
   ```

6. Recover the data, choosing one:
   - **Targeted repair:** dump only the affected schema or tables from the new server and
     load them into the original (`pg_dump --schema=<module>` then `psql`), reviewed first.
   - **Cutover:** point the application at the restored server. This changes the host name in
     configuration, requires re-creating the app and migrator database roles, and is
     disruptive. Open question: preferred cutover procedure is not decided.

7. Delete the restored server when finished, to stay inside the free allowance.

   ```bash
   az postgres flexible-server delete --resource-group "$RG" --name "$NEW" --yes
   ```

## Verify

- Application `/health` returns healthy and a read through the API returns the repaired data.
- `az postgres flexible-server list --resource-group "$RG" -o table` shows only the intended
  server(s).
- Migration history for each module schema matches the deployed version.

## Rollback

The original server is untouched by a restore. If the targeted repair is wrong, restore again
to an earlier time, or repair from the dump you reviewed. If a cutover failed, point the
configuration back to the original server and redeploy.
