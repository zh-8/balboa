// Balboa environment stack — resource-group scope, deployed by CI.
//
// Everything that differs between environments is a parameter; see env/*.bicepparam.
// Bootstrap (infra/bootstrap/) must have run first: it owns the resource groups,
// the shared Log Analytics workspace and the GitHub deploy identity.
//
// Cost traps, deliberately encoded here — do not "simplify" them away:
//   * The Container Apps environment is Consumption-only. Adding workload profiles
//     turns on an Environment Management fee of ~$0.13/hr (~$95/month).
//   * Only one Postgres B1ms fits the 750 h/month free allowance. A second server
//     running 24/7 is billable.
//   * There is no container registry. Images come from ghcr.io (ADR-0016).

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for the environment resources.')
param location string = resourceGroup().location

@description('Environment name, used in resource names and tags.')
@allowed(['dev', 'prod'])
param env string

@description('Short region code used in resource names.')
param regionCode string = 'itn'

@description('Resource ID of the shared Log Analytics workspace, from the bootstrap outputs.')
param logAnalyticsWorkspaceId string

@description('Fully qualified container image for the API, pinned by digest. Example: ghcr.io/zh-8/balboa-api@sha256:... Digests, not tags, so a revision is immutable and a rollback is exact.')
param apiImage string

@description('Object ID of the break-glass Postgres administrator (a human).')
param adminPrincipalObjectId string

@description('UPN or display name of the break-glass Postgres administrator.')
param adminPrincipalName string

@description('Region for the Static Web App. Static Web Apps is not offered in Italy North, so it lives in West Europe and serves from the global edge (ADR-0007).')
param staticWebAppLocation string = 'westeurope'

@description('Custom hostname for the frontend, or empty to use only the default Azure hostname. CNAME must exist at the DNS provider first.')
param frontendHostname string = ''

@description('Custom hostname for the API, or empty to use only the default Azure hostname.')
param apiHostname string = ''

@description('Postgres compute SKU. B1ms is the one covered by the 12-month free allowance.')
param postgresSkuName string = 'Standard_B1ms'

@description('Postgres storage in GB. 32 GB is the free allowance.')
param postgresStorageGb int = 32

@description('Point-in-time-restore retention in days.')
@minValue(7)
@maxValue(35)
param postgresBackupRetentionDays int = 7

@description('Minimum API replicas. 0 means scale-to-zero: no compute cost when idle, at the price of a cold start.')
@minValue(0)
param apiMinReplicas int = 0

@description('Maximum API replicas, capping the worst-case bill.')
@minValue(1)
param apiMaxReplicas int = 2

@description('Soft-delete retention for Key Vault, in days.')
@minValue(7)
@maxValue(90)
param keyVaultSoftDeleteDays int = 7

@description('Whether Key Vault purge protection is on. Off in dev so the vault can be recreated; on in prod.')
param keyVaultPurgeProtection bool = false

// ---------------------------------------------------------------------------
// Naming and tags
// ---------------------------------------------------------------------------

var suffix = take(uniqueString(resourceGroup().id), 4)
var names = {
  containerAppsEnv: 'cae-balboa-${env}-${regionCode}'
  api: 'ca-balboa-api-${env}-${regionCode}'
  migrateJob: 'caj-balboa-migrate-${env}-${regionCode}'
  postgres: 'psql-balboa-${env}-${regionCode}'
  keyVault: 'kv-balboa-${env}-${suffix}'
  appInsights: 'appi-balboa-${env}-${regionCode}'
  staticWebApp: 'stapp-balboa-${env}'
  apiIdentity: 'id-balboa-api-${env}'
  migratorIdentity: 'id-balboa-migrator-${env}'
}

var tags = {
  app: 'balboa'
  env: env
  owner: 'zh-8'
  managedBy: 'bicep'
}

var databaseName = 'balboa'

// ---------------------------------------------------------------------------
// Identities
//
// Two, with different jobs: the API reads data, the migrator changes schema.
// Splitting them means a compromised API container cannot run DDL.
// ---------------------------------------------------------------------------

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.apiIdentity
  location: location
  tags: tags
}

resource migratorIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.migratorIdentity
  location: location
  tags: tags
}

// ---------------------------------------------------------------------------
// Observability
// ---------------------------------------------------------------------------

module observability 'modules/observability.bicep' = {
  params: {
    name: names.appInsights
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId
    samplingPercentage: env == 'prod' ? 50 : 100
  }
}

// ---------------------------------------------------------------------------
// Key Vault
//
// Empty at first: with Entra-only Postgres auth there is no connection-string
// password to store. It exists so that the first third-party API key has an
// obvious home rather than becoming an app setting.
// ---------------------------------------------------------------------------

module keyVault 'modules/key-vault.bicep' = {
  params: {
    name: names.keyVault
    location: location
    tags: tags
    softDeleteRetentionDays: keyVaultSoftDeleteDays
    purgeProtection: keyVaultPurgeProtection
    secretsUserPrincipalIds: [
      apiIdentity.properties.principalId
    ]
  }
}

// ---------------------------------------------------------------------------
// Postgres
// ---------------------------------------------------------------------------

module postgres 'modules/postgres.bicep' = {
  params: {
    name: names.postgres
    location: location
    tags: tags
    skuName: postgresSkuName
    storageGb: postgresStorageGb
    backupRetentionDays: postgresBackupRetentionDays
    databaseName: databaseName
    administrators: [
      {
        objectId: migratorIdentity.properties.principalId
        principalName: names.migratorIdentity
        principalType: 'ServicePrincipal'
      }
      {
        objectId: adminPrincipalObjectId
        principalName: adminPrincipalName
        principalType: 'User'
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// Container Apps
// ---------------------------------------------------------------------------

module containerApps 'modules/container-apps.bicep' = {
  params: {
    environmentName: names.containerAppsEnv
    apiName: names.api
    migrateJobName: names.migrateJob
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId
    image: apiImage
    apiIdentityResourceId: apiIdentity.id
    apiIdentityClientId: apiIdentity.properties.clientId
    migratorIdentityResourceId: migratorIdentity.id
    migratorIdentityClientId: migratorIdentity.properties.clientId
    minReplicas: apiMinReplicas
    maxReplicas: apiMaxReplicas
    appInsightsConnectionString: observability.outputs.connectionString
    postgresFqdn: postgres.outputs.fqdn
    databaseName: databaseName
    allowedCorsOrigins: frontendHostname == ''
      ? ['https://${staticWebApp.properties.defaultHostname}']
      : ['https://${frontendHostname}']
    customHostname: apiHostname
    env: env
  }
}

// ---------------------------------------------------------------------------
// Static Web App (frontend)
//
// Free tier: no cost, managed TLS, custom domains, global edge. Declared here
// rather than in a module because it is a single resource and `main` needs its
// default hostname to build the API's CORS allow-list.
// ---------------------------------------------------------------------------

resource staticWebApp 'Microsoft.Web/staticSites@2024-11-01' = {
  name: names.staticWebApp
  location: staticWebAppLocation
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // The frontend is built and uploaded by CI, not by SWA's own GitHub
    // integration: one pipeline, one place to look when a deploy fails.
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

// ---------------------------------------------------------------------------
// Outputs — consumed by the deploy workflow and the runbooks.
// ---------------------------------------------------------------------------

output apiFqdn string = containerApps.outputs.apiFqdn
output apiName string = names.api
output migrateJobName string = names.migrateJob
output containerAppsEnvironmentName string = names.containerAppsEnv
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
output postgresFqdn string = postgres.outputs.fqdn
output postgresServerName string = names.postgres
output databaseName string = databaseName
output keyVaultName string = keyVault.outputs.name
output apiIdentityClientId string = apiIdentity.properties.clientId
output migratorIdentityClientId string = migratorIdentity.properties.clientId
output appInsightsName string = names.appInsights

// The hostname a CNAME should point at, so the runbook can print the exact DNS
// record to create at the registrar.
output frontendCnameTarget string = staticWebApp.properties.defaultHostname
output apiCnameTarget string = containerApps.outputs.apiFqdn
output apiCustomDomainVerificationId string = containerApps.outputs.customDomainVerificationId
