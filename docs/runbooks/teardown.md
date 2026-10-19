# Runbook: teardown

> **Not yet exercised.** This describes the PLANNED Azure setup. No Balboa resources exist
> yet (subscription in `Warned` state). Commands are unverified drafts.

> **WARNING: never name, target or scope anything above `rg-cairobits`.** `rg-cairobits` and
> the `cairobits` Static Web App are an unrelated project in the same subscription. Never run
> a subscription-wide delete, never delete by tag or wildcard across the subscription, and
> never use `az group delete` with a name you have not typed out in full from the list below.

Teardown is **destructive and irreversible**. It needs explicit approval, every time.

Related: [ADR-0017](../adr/0017-bicep-layering-and-naming.md).

## What may be deleted

Only these resource groups, by exact name:

| Group | Contents |
|---|---|
| `rg-balboa-dev-itn` | dev Container App, job, environment, Postgres, Log Analytics, identities |
| `rg-balboa-shared-itn` | shared resources |

The Static Web App lives in West Europe but is also inside a `rg-balboa-*` group; confirm
with the listing in step 2.

## Steps

1. Confirm the subscription and that you are not in the wrong one.

   ```bash
   az account show --query "{name:name,id:id}" -o table
   ```

2. List groups and read the output. Only groups beginning `rg-balboa-` are in scope.

   ```bash
   az group list --query "[?starts_with(name,'rg-balboa-')].name" -o tsv
   az resource list --resource-group rg-balboa-dev-itn \
     --query "[].{n:name,t:type}" -o table
   ```

3. Take a final backup if any data matters (see [db-restore.md](db-restore.md); a deleted
   server cannot be restored from within Azure after deletion).

4. Remove custom domain CNAMEs at GoDaddy for `dev.balboa.cairobits.com` and
   `api.dev.balboa.cairobits.com` to prevent subdomain takeover of dangling records. This is
   a manual step outside Azure.

5. Delete the environment group, then shared, by exact name.

   ```bash
   az group delete --name rg-balboa-dev-itn --yes --no-wait
   az group wait --name rg-balboa-dev-itn --deleted
   az group delete --name rg-balboa-shared-itn --yes --no-wait
   az group wait --name rg-balboa-shared-itn --deleted
   ```

6. Remove subscription-scope leftovers that belong to Balboa: role assignments for the
   Balboa identities (they are normally deleted with the groups, but orphans show as
   unknown principals).

   ```bash
   az role assignment list --all \
     --query "[?principalName==null && contains(scope,'rg-balboa')]" -o table
   ```

7. Remove the GitHub environment variables and, if abandoning the project, archive the
   repository. Do not delete `main` history.

## Verify

```bash
az group list --query "[?starts_with(name,'rg-balboa-')].name" -o tsv   # expect empty
az group show --name rg-cairobits --query properties.provisioningState -o tsv
```

- No `rg-balboa-*` groups remain.
- `rg-cairobits` still exists and is in state `Succeeded`. If that is not so, stop and
  report immediately.

## Rollback

There is no undo for deleted resources. Soft-deleted items (for example a Key Vault, if one
exists) can be recovered within their retention window only. To rebuild, re-run
[bootstrap.md](bootstrap.md) and redeploy from `main`; data is restored only from a backup
taken before deletion.
