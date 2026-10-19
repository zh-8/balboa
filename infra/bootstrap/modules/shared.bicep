// Shared, cross-environment resources: observability and the GitHub deploy identity.
// Deployed into rg-balboa-shared-itn by bootstrap.bicep.

@description('Azure region.')
param location string

@description('Tags applied to every resource here.')
param tags object

@description('Short region code used in resource names.')
param regionCode string

@description('Environment this deploy identity is for.')
param env string

@description('GitHub account that owns the repository.')
param githubOwner string

@description('GitHub repository name.')
param githubRepo string

@description('Daily ingestion cap in GB. Keeps Log Analytics inside the 5 GB/month free grant.')
param dailyCapGb int

@description('Log retention in days.')
param retentionDays int

// ---------------------------------------------------------------------------
// Log Analytics
//
// One workspace for every environment. App Insights instances are
// workspace-based and point here, so the daily cap governs all ingestion and
// there is a single place to query across environments.
// ---------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: 'log-balboa-shared-${regionCode}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionDays
    workspaceCapping: {
      // Ingestion stops for the rest of the day once this is hit. Losing dev
      // telemetry is strictly better than an unbounded bill.
      dailyQuotaGb: dailyCapGb
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    features: {
      // Local (shared-key) auth stays ENABLED on purpose. A Container Apps
      // environment with a `log-analytics` log destination authenticates with the
      // workspace shared key; disabling local auth breaks console and system log
      // ingestion. The alternative — the `azure-monitor` destination plus
      // diagnostic settings — avoids the key but is a less-travelled path, and
      // this stack carries no user data. Revisit when real data lands.
      disableLocalAuth: false
    }
  }
}

// ---------------------------------------------------------------------------
// GitHub Actions deploy identity
//
// A user-assigned managed identity with a federated credential, NOT an Entra app
// registration (ADR-0009): nothing to rotate, no client secret, and it needs no
// Entra app-registration permission.
//
// The subject pins the credential to one repository AND one GitHub environment,
// so a workflow running outside the protected `dev` environment cannot use it
// even if it runs in this repo.
// ---------------------------------------------------------------------------

resource githubIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-balboa-gh-${env}'
  location: location
  tags: tags
}

resource githubFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: githubIdentity
  name: 'github-${githubOwner}-${githubRepo}-${env}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubOwner}/${githubRepo}:environment:${env}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output logAnalyticsWorkspaceId string = logAnalytics.id
output logAnalyticsWorkspaceName string = logAnalytics.name
output githubIdentityPrincipalId string = githubIdentity.properties.principalId
output githubIdentityClientId string = githubIdentity.properties.clientId
output githubIdentityResourceId string = githubIdentity.id
