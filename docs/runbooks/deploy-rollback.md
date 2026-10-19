# Runbook: deploy and rollback

> **Not yet exercised.** This describes the PLANNED Azure setup. The dev environment does
> not exist yet (subscription in `Warned` state). Commands are unverified drafts; correct
> them on first real use. Production is deferred
> ([ADR-0018](../adr/0018-defer-production-environment.md)); this runbook covers dev.

Production deploys and rollbacks are high-risk and need explicit approval every time.

Related: [ADR-0006](../adr/0006-container-apps-consumption-only.md),
[ADR-0012](../adr/0012-trunk-based-development.md),
[ADR-0016](../adr/0016-ghcr-instead-of-acr.md).

## How a deploy works

1. A PR squash-merges to `main` after `ci-ok` is green.
2. The `main` workflow builds the image, pushes it to
   `ghcr.io/zh-8/balboa-api:<tag>` (CalVer tag and commit SHA) and logs in to Azure by OIDC.
3. Bicep for `rg-balboa-dev-itn` is applied, pointing the Container App at the new image tag.
4. The `migrate` Container Apps job runs the new image under the migrator identity and must
   succeed before traffic moves.
5. A new Container Apps revision is created and receives traffic (single-revision mode).
6. The frontend is deployed to Static Web Apps.
7. Smoke tests run against `https://api.dev.balboa.cairobits.com/health`.

Migrations must be backwards compatible with the previous revision (expand, then contract),
so a rollback of the app does not require a database rollback.

## Roll back the API (revision)

1. List revisions and find the last good one.

   ```bash
   az containerapp revision list \
     --name ca-balboa-api-dev-itn --resource-group rg-balboa-dev-itn \
     --query "[].{name:name,active:properties.active,created:properties.createdTime,traffic:properties.trafficWeight}" \
     -o table
   ```

2. Activate the previous revision (if it is inactive).

   ```bash
   az containerapp revision activate \
     --name ca-balboa-api-dev-itn --resource-group rg-balboa-dev-itn \
     --revision <previous-revision-name>
   ```

3. Send all traffic to it. In single-revision mode activating the older revision and
   deactivating the faulty one is sufficient; in multiple-revision mode set the weights.

   ```bash
   az containerapp revision deactivate \
     --name ca-balboa-api-dev-itn --resource-group rg-balboa-dev-itn \
     --revision <bad-revision-name>
   ```

4. Open an issue and revert the bad commit on `main` through a normal PR, so the next deploy
   does not reintroduce it. Do not leave Azure state diverging from `main`.

Rollback depends on the old image still being pullable from ghcr.io. If ghcr.io is
unavailable and the image is not cached, rollback may fail (see ADR-0016).

## Roll back the frontend

Re-run the SWA deployment from the last good commit (re-run the earlier workflow run, or
revert on `main`). SWA keeps no promotable history on the Free tier.

## Verify

```bash
curl -fsS https://api.dev.balboa.cairobits.com/health
az containerapp revision list --name ca-balboa-api-dev-itn \
  --resource-group rg-balboa-dev-itn --query "[?properties.active]" -o table
az containerapp logs show --name ca-balboa-api-dev-itn \
  --resource-group rg-balboa-dev-itn --tail 50
```

- `/health` returns healthy; the expected revision is active.
- The first request after idle may be slow (cold start); retry once before alarming.

## Rollback of this procedure

If revision activation does not fix the issue, redeploy a known-good image tag by re-running
the deploy workflow on the last good commit, or restore data via
[db-restore.md](db-restore.md) if a bad migration damaged data.
