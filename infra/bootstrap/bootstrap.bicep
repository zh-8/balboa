// Balboa bootstrap — subscription-scope, one-time setup.
//
// Creates the things that must exist BEFORE CI can deploy anything:
//   * the resource groups
//   * the shared Log Analytics workspace (with a daily cap, so ingestion cannot run away)
//   * the GitHub OIDC identity, its federated credential and its least-privilege RBAC
//   * the cost budget
//
// Run it through infra/bootstrap/bootstrap.sh, which asserts the subscription and tenant
// first. See docs/runbooks/bootstrap.md.
//
// Deliberately NOT here: a container registry. Images live on ghcr.io, which is free for
// public repos; ACR Basic would cost ~$5/month. See ADR-0016.

targetScope = 'subscription'

@description('Azure region for all resources. Italy North is the closest region to Cairo offering both Container Apps and Postgres Flexible Server (ADR-0008).')
param location string = 'italynorth'

@description('Environment being bootstrapped. Only dev exists for now; prod is deferred (ADR-0018).')
@allowed(['dev', 'prod'])
param env string = 'dev'

@description('GitHub account that owns the repository.')
param githubOwner string = 'zh-8'

@description('GitHub repository name.')
param githubRepo string = 'balboa'

@description('Monthly budget in USD across the shared and environment resource groups. Dev-only for now; raise it when prod lands (ADR-0018).')
@minValue(1)
param budgetAmount int = 10

@description('Address that receives budget alerts.')
param alertEmail string

@description('Object ID of the human break-glass administrator. Becomes a Postgres Entra admin so there is a way in that does not depend on CI.')
param adminPrincipalObjectId string

@description('UPN or display name of the break-glass administrator, for the Postgres Entra admin record.')
param adminPrincipalName string

@description('Log Analytics daily ingestion cap in GB. 0.15 GB/day is ~4.6 GB/month, inside the 5 GB free grant.')
param logAnalyticsDailyCapGb string = '0.15'

@description('Days to retain logs. 30 is the free retention period.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

// ---------------------------------------------------------------------------
// Naming and tags
// ---------------------------------------------------------------------------

var regionCode = 'itn'
var sharedRgName = 'rg-balboa-shared-${regionCode}'
var envRgName = 'rg-balboa-${env}-${regionCode}'

var commonTags = {
  app: 'balboa'
  owner: githubOwner
  managedBy: 'bicep'
}
var sharedTags = union(commonTags, { env: 'shared' })
var envTags = union(commonTags, { env: env })

// Built-in role definition IDs.
var roles = {
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  rbacAdministrator: 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  monitoringMetricsPublisher: '3913510d-42f4-4e42-8a64-420c390055eb'
}

// ---------------------------------------------------------------------------
// Resource groups
// ---------------------------------------------------------------------------

resource sharedRg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: sharedRgName
  location: location
  tags: sharedTags
}

resource envRg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: envRgName
  location: location
  tags: envTags
}

// ---------------------------------------------------------------------------
// Shared observability + the GitHub deploy identity
// ---------------------------------------------------------------------------

module shared 'modules/shared.bicep' = {
  scope: sharedRg
  params: {
    location: location
    tags: sharedTags
    regionCode: regionCode
    env: env
    githubOwner: githubOwner
    githubRepo: githubRepo
    dailyCapGb: json(logAnalyticsDailyCapGb)
    retentionDays: logRetentionDays
  }
}

// ---------------------------------------------------------------------------
// RBAC for the GitHub deploy identity
//
// Contributor on the environment resource group lets CI deploy the stack.
// RBAC Administrator lets it create the role assignments that main.bicep needs
// (app identity -> Key Vault, -> App Insights), but an ABAC condition pins it to
// exactly those two roles so it cannot grant anything else — including to itself.
//
// `what-if-short-circuiting` is suppressed on the two principalId lines below, and
// nowhere else. A role assignment's name must be a deterministic guid() derived from
// the principal, and the principal only exists once the managed identity in
// modules/shared.bicep has been created — so the value cannot be anything but a
// runtime reference. The consequence is that `what-if` reports these two role
// assignments as unknown rather than predicting them; bootstrap.sh prints the full
// what-if and requires the operator to type `yes`, so the loss of precision is
// visible and bounded to RBAC the operator is already being asked to approve.
// ---------------------------------------------------------------------------

module envRbac 'modules/rbac.bicep' = {
  scope: envRg
  params: {
    #disable-next-line what-if-short-circuiting
    principalId: shared.outputs.githubIdentityPrincipalId
    contributorRoleId: roles.contributor
    rbacAdministratorRoleId: roles.rbacAdministrator
    assignableRoleIds: [
      roles.keyVaultSecretsUser
      roles.monitoringMetricsPublisher
    ]
  }
}

// Reader on the shared group: CI needs to resolve the Log Analytics workspace ID
// when it deploys App Insights, but must never modify shared resources.
module sharedRbac 'modules/rbac-reader.bicep' = {
  scope: sharedRg
  params: {
    #disable-next-line what-if-short-circuiting
    principalId: shared.outputs.githubIdentityPrincipalId
  }
}

// ---------------------------------------------------------------------------
// Budget
//
// The spending limit disappears when the subscription is upgraded to
// pay-as-you-go, so this is the only thing standing between a misconfiguration
// and a real bill. Expected spend is ~$0; $10 is a tripwire, not a ceiling.
// ---------------------------------------------------------------------------

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'budget-balboa-${env}'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      // Must be the first of a month and not in the past.
      startDate: '2026-11-01T00:00:00Z'
    }
    filter: {
      dimensions: {
        name: 'ResourceGroupName'
        operator: 'In'
        values: [
          sharedRgName
          envRgName
        ]
      }
    }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
      }
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [alertEmail]
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs — these become GitHub environment variables. None of them is a secret:
// OIDC means there is no client secret to store.
// ---------------------------------------------------------------------------

output sharedResourceGroup string = sharedRg.name
output envResourceGroup string = envRg.name
output logAnalyticsWorkspaceId string = shared.outputs.logAnalyticsWorkspaceId
output githubIdentityClientId string = shared.outputs.githubIdentityClientId
output tenantId string = subscription().tenantId
output subscriptionId string = subscription().subscriptionId
output budgetName string = budget.name

// Echoed back so bootstrap.sh can show the operator what will become the Postgres
// break-glass admin, and so a drifted value is visible in what-if output.
output breakGlassAdminObjectId string = adminPrincipalObjectId
output breakGlassAdminName string = adminPrincipalName
