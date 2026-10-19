# Runbook: rotate OIDC federated credentials and managed identities

> **Not yet exercised.** This describes the PLANNED Azure setup. No identities exist yet
> (subscription in `Warned` state). Commands are unverified drafts.

This is a **high-risk** procedure (identity and RBAC). Get explicit approval before each
step.

There is no secret to rotate: OIDC federated credentials carry no key material
([ADR-0009](../adr/0009-github-oidc-user-assigned-managed-identities.md)). Rotation here
means changing the trust relationship, for example after a repository rename, a suspected
compromise of a GitHub environment, a changed subject claim, or replacing an identity.

## When to run

- Repository or environment renamed or transferred (subject no longer matches).
- Suspected compromise of the GitHub repo, environment or an approver account.
- Replacing an identity with a narrower or differently named one.
- Periodic review of who and what can authenticate.

## Steps: replace a federated credential

1. List the current credentials on the identity.

   ```bash
   az identity federated-credential list \
     --identity-name <identity-name> --resource-group rg-balboa-shared-itn -o table
   ```

2. Create the new credential first, so deploys are never without access.

   ```bash
   az identity federated-credential create \
     --name gh-balboa-dev-v2 \
     --identity-name <identity-name> --resource-group rg-balboa-shared-itn \
     --issuer https://token.actions.githubusercontent.com \
     --subject repo:zh-8/balboa:environment:dev \
     --audiences api://AzureADTokenExchange
   ```

3. Run the deploy workflow's login step and confirm `azure/login` succeeds.

4. Delete the old credential.

   ```bash
   az identity federated-credential delete \
     --name <old-credential-name> \
     --identity-name <identity-name> --resource-group rg-balboa-shared-itn --yes
   ```

5. Update the change in Bicep (`infra/bootstrap/`) in the same PR, so the code, not the
   portal, remains the source of truth.

## Steps: replace a managed identity

1. Create the new identity and its federated credential via Bicep (preferred), with the same
   least-privilege role assignments as the old one.
2. Update `AZURE_CLIENT_ID` in the GitHub environment variables.

   ```bash
   gh variable set AZURE_CLIENT_ID --env dev --repo zh-8/balboa --body <new-client-id>
   ```

3. For the **runtime** identities (app and migrator), recreate the Postgres role mapping for
   the new principal ([ADR-0010](../adr/0010-entra-only-postgres-authentication.md)):
   create the Entra-mapped role, grant the same schema privileges, then assign the new
   identity to the Container App or job. Open question: the exact role-mapping script is
   not yet written.
4. Run a deploy; confirm login, migration job and API health.
5. Remove the old identity's role assignments, then delete the identity.

   ```bash
   az role assignment delete --assignee <old-principal-id> --scope <scope>
   az identity delete --name <old-identity-name> --resource-group rg-balboa-shared-itn
   ```

## Verify

```bash
az identity federated-credential list \
  --identity-name <identity-name> --resource-group rg-balboa-shared-itn -o table
az role assignment list --assignee <principal-id> --all -o table
curl -fsS https://api.dev.balboa.cairobits.com/health
```

- Only the intended credentials and role assignments exist; scopes are `rg-balboa-*` only.
- A deploy workflow run succeeds end to end, including the migration job.
- The old principal ID no longer appears in any role assignment or Postgres role.

## Rollback

Because the new credential is created before the old one is deleted, rollback before step 4
is to delete the new credential. After deletion of the old one, re-create it using the same
`create` command with the old name and subject (the values are in Bicep history). If the
identity itself was deleted, redeploy it from Bicep and reassign roles; its principal ID will
differ, so Postgres role mapping must be redone.
