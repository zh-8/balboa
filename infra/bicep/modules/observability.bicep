// Workspace-based Application Insights.
//
// Telemetry lands in the shared Log Analytics workspace, so the daily ingestion
// cap set in bootstrap governs this too and there is one place to query.

@description('Resource name.')
param name string

@description('Azure region.')
param location string

@description('Tags.')
param tags object

@description('Resource ID of the shared Log Analytics workspace.')
param logAnalyticsWorkspaceId string

@description('Ingestion sampling percentage. 100 in dev (volume is tiny and full traces are more useful); reduced in prod to protect the free grant.')
@minValue(1)
@maxValue(100)
param samplingPercentage int = 100

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    SamplingPercentage: samplingPercentage
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    // Telemetry is sent with a managed identity, so no ingestion key is needed.
    DisableLocalAuth: true
  }
}

output id string = appInsights.id
output name string = appInsights.name

// The connection string contains an instrumentation key. It is an ingestion
// endpoint identifier, not a credential that grants read access, and local auth
// is disabled above — so passing it to the container as an env var is safe.
#disable-next-line outputs-should-not-contain-secrets
output connectionString string = appInsights.properties.ConnectionString
