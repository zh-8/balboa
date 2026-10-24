# Runbook: bootstrap (one-time subscription-scope setup)

> **Not yet exercised.** This describes the PLANNED Azure setup. No Azure resources for
> Balboa exist, and the subscription is in `Warned` state, so nothing here can be created
> until the subscription is upgraded to pay-as-you-go. Commands are unverified drafts;
> correct them the first time this is run, and record the corrections here.

This is a **high-risk** procedure (identity, RBAC, resource creation). Run it by hand, once,
and obtain explicit approval before each step. See `CLAUDE.md` section 8.

Related: [ADR-0009](../adr/0009-github-oidc-user-assigned-managed-identities.md),
[ADR-0017](../adr/0017-bicep-layering-and-naming.md).

## Prerequisites

- Subscription upgraded from `Warned` to pay-as-you-go (owner action in the portal).
- Azure CLI signed in as a user with Owner (or Contributor plus User Access Administrator)
  on the subscription.
- Public GitHub repository `zh-8/balboa` exists, with an environment named `dev`.
- `infra/bootstrap/` exists in the repository.

## Steps

1. Sign in and select the subscription. Confirm the state is `Enabled`, not `Warned`.

   The ids live in `infra/bootstrap/.env` (gitignored), not in this public repo. Copy
   `infra/bootstrap/.env.example` and fill it in once; `bootstrap.sh` sources it.

   ```bash
   set -a && source infra/bootstrap/.env && set +a
   az login --tenant "$BALBOA_TENANT_ID"
   az account set --subscription "$BALBOA_SUBSCRIPTION_ID"
   az account show --query "{name:name, state:state, id:id}" -o table
   ```

2. Register required resource providers (idempotent).

   ```bash
   for ns in Microsoft.App Microsoft.DBforPostgreSQL Microsoft.OperationalInsights \
             Microsoft.ManagedIdentity Microsoft.Web; do
     az provider register --namespace "$ns"
   done
   az provider list --query "[?registrationState=='Registered'].namespace" -o tsv | sort
   ```

3. Preview the bootstrap deployment. Read the what-if output fully; confirm that it touches
   only `rg-balboa-*` resource groups and nothing named `cairobits`.

   ```bash
   az deployment sub what-if \
     --location italynorth \
     --name balboa-bootstrap \
     --template-file infra/bootstrap/main.bicep \
     --parameters infra/bootstrap/main.bicepparam
   ```

4. Apply the bootstrap deployment after approval. It creates `rg-balboa-shared-itn`,
   `rg-balboa-dev-itn`, the user-assigned managed identities, their federated credentials
   (subject `repo:zh-8/balboa:environment:dev`, issuer
   `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`) and
   the minimal role assignments scoped to the `rg-balboa-*` groups.

   ```bash
   az deployment sub create \
     --location italynorth \
     --name balboa-bootstrap \
     --template-file infra/bootstrap/main.bicep \
     --parameters infra/bootstrap/main.bicepparam
   ```

5. Read the identity client IDs from the deployment outputs.

   ```bash
   az deployment sub show --name balboa-bootstrap --query properties.outputs -o json
   ```

6. Store the non-secret identifiers as GitHub environment variables (client ID, tenant ID,
   subscription ID are not secrets). Do not create any client secret.

   ```bash
   gh variable set AZURE_TENANT_ID       --env dev --repo zh-8/balboa \
     --body "$BALBOA_TENANT_ID"
   gh variable set AZURE_SUBSCRIPTION_ID --env dev --repo zh-8/balboa \
     --body "$BALBOA_SUBSCRIPTION_ID"
   gh variable set AZURE_CLIENT_ID       --env dev --repo zh-8/balboa --body <client-id>
   ```

   These are GitHub *variables*, not secrets, and that is deliberate: none of the three
   authenticates anything on its own. They are still not committed, because a public
   repository is permanent in a way an environment variable is not.

7. Set the Postgres Entra administrator and create the database roles for the app and
   migrator identities once the server exists (first CI infrastructure run). Open question:
   the exact mechanism (a script run by the migrator identity versus a one-off step run
   here) is not yet decided.

8. Create a $10 budget with alert emails on `rg-balboa-dev-itn` (portal-free, via Bicep in
   the bootstrap template, or `az consumption budget create`).

## Verify

```bash
az group list --query "[?starts_with(name,'rg-balboa-')].{n:name,l:location}" -o table
az identity list --resource-group rg-balboa-shared-itn -o table
az identity federated-credential list \
  --identity-name <identity-name> --resource-group rg-balboa-shared-itn -o table
az role assignment list --all \
  --query "[?contains(roleDefinitionName,'Contributor')].{p:principalName,s:scope}" -o table
```

- Only `rg-balboa-*` groups appear as scopes; nothing refers to `rg-cairobits`.
- Federated credential subject matches `repo:zh-8/balboa:environment:dev`.
- Trigger the deploy workflow's login step and confirm `azure/login` succeeds.

## Rollback

Bootstrap creates only Balboa-owned resources, so rollback is deleting those groups. Follow
[teardown.md](teardown.md); do not delete anything by broad query.

```bash
az group delete --name rg-balboa-dev-itn --yes --no-wait
az group delete --name rg-balboa-shared-itn --yes --no-wait
```

Role assignments scoped to the deleted groups disappear with them; list and remove any
subscription-scope assignment for the Balboa identities by name.
