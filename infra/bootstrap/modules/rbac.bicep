// RBAC for the GitHub Actions deploy identity on an environment resource group.
//
// Two assignments, both scoped to this resource group only:
//
//   Contributor            — deploy and manage the stack.
//   RBAC Administrator     — create the role assignments that main.bicep needs
//                            (the app identity granted Key Vault Secrets User and
//                            Monitoring Metrics Publisher).
//
// RBAC Administrator is the dangerous one: unconstrained, it would let CI grant
// itself Owner. The ABAC condition below pins it to the exact role definitions in
// `assignableRoleIds`, so any attempt to assign a different role is refused by
// Azure itself rather than by convention.

targetScope = 'resourceGroup'

@description('Principal ID (object ID) of the GitHub deploy managed identity.')
param principalId string

@description('Contributor role definition GUID.')
param contributorRoleId string

@description('Role Based Access Control Administrator role definition GUID.')
param rbacAdministratorRoleId string

@description('The only role definition GUIDs this principal may assign to others.')
param assignableRoleIds array

// Renders to: {guid1,guid2}
var assignableRoleSet = join(assignableRoleIds, ',')

// Allow the request through when it is NOT a role-assignment write, or when it is
// a role-assignment write whose RoleDefinitionId is in the allow-list. Deleting a
// role assignment is constrained the same way, so CI cannot remove an assignment
// it was not allowed to create.
var constrainRoleAssignments = join(
  [
    '('
    '  ('
    '    !(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})'
    '  )'
    '  OR'
    '  ('
    '    @Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId]'
    '      ForAnyOfAnyValues:GuidEquals{${assignableRoleSet}}'
    '  )'
    ')'
    'AND'
    '('
    '  ('
    '    !(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})'
    '  )'
    '  OR'
    '  ('
    '    @Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId]'
    '      ForAnyOfAnyValues:GuidEquals{${assignableRoleSet}}'
    '  )'
    ')'
  ],
  '\n'
)

resource contributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, contributorRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    description: 'GitHub Actions deploys the Balboa stack into this resource group.'
  }
}

resource rbacAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, rbacAdministratorRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rbacAdministratorRoleId)
    description: 'Lets CI grant the app identity Key Vault and App Insights access, and nothing else.'
    condition: constrainRoleAssignments
    conditionVersion: '2.0'
  }
}

output contributorAssignmentId string = contributor.id
output rbacAdminAssignmentId string = rbacAdmin.id
