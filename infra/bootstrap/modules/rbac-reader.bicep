// Reader on the shared resource group for the GitHub Actions deploy identity.
//
// CI needs to resolve the shared Log Analytics workspace resource ID when it
// deploys the workspace-based App Insights instance. Reader is enough for that
// and keeps the shared resources — which outlive any single environment — safe
// from an environment deployment.

targetScope = 'resourceGroup'

@description('Principal ID (object ID) of the GitHub deploy managed identity.')
param principalId string

@description('Reader role definition GUID.')
param readerRoleId string = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'

resource reader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, readerRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', readerRoleId)
    description: 'CI reads the shared Log Analytics workspace ID; it must not modify shared resources.'
  }
}

output readerAssignmentId string = reader.id
