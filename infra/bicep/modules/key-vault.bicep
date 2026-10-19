// Key Vault, RBAC-mode.
//
// Empty on day one: Entra-only Postgres auth means there is no connection-string
// password, and the App Insights connection string is not a credential. It exists
// so the first third-party API key has an obvious home instead of quietly becoming
// a plaintext app setting.

@description('Vault name. Must be globally unique.')
@minLength(3)
@maxLength(24)
param name string

@description('Azure region.')
param location string

@description('Tags.')
param tags object

@description('Soft-delete retention in days.')
@minValue(7)
@maxValue(90)
param softDeleteRetentionDays int

@description('Purge protection. Once enabled it cannot be disabled, and the vault name is reserved for the full retention period — so it stays off in dev.')
param purgeProtection bool

@description('Principal IDs granted Key Vault Secrets User (read secret values, nothing else).')
param secretsUserPrincipalIds array

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    // RBAC, not access policies: one authorization model for the whole
    // subscription, and assignments are visible in the same place as everything
    // else.
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: softDeleteRetentionDays
    enablePurgeProtection: purgeProtection ? true : null
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in secretsUserPrincipalIds: {
    scope: vault
    name: guid(vault.id, principalId, keyVaultSecretsUserRoleId)
    properties: {
      principalId: principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        keyVaultSecretsUserRoleId
      )
      description: 'Read secret values at runtime.'
    }
  }
]

output id string = vault.id
output name string = vault.name
output uri string = vault.properties.vaultUri
