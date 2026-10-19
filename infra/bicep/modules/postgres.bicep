// PostgreSQL Flexible Server.
//
// Entra-only authentication (ADR-0010): password auth is disabled outright, so
// there is no connection-string secret to leak, rotate or accidentally commit.
// The API and the migrator authenticate with their managed identities and a
// short-lived token.
//
// Cost note: B1ms with 32 GB is exactly the 12-month free allowance, and 750 free
// hours per month covers one server running 24/7 (31 x 24 = 744). A SECOND server
// on this subscription is billable.

@description('Server name.')
param name string

@description('Azure region.')
param location string

@description('Tags.')
param tags object

@description('Compute SKU.')
param skuName string

@description('Storage in GB.')
param storageGb int

@description('Point-in-time-restore retention in days.')
param backupRetentionDays int

@description('Application database name.')
param databaseName string

@description('Entra administrators: objects with objectId, principalName and principalType.')
param administrators array

@description('PostgreSQL major version.')
param postgresVersion string = '17'

@description('High availability. Off: it doubles the compute cost and this is a single-region dev stack.')
param highAvailability bool = false

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: 'Burstable'
  }
  properties: {
    version: postgresVersion
    // No administratorLogin/administratorLoginPassword: with passwordAuth
    // disabled they would be dead weight, and Bicep rejects a password-less
    // admin login anyway.
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
    storage: {
      storageSizeGB: storageGb
      autoGrow: 'Disabled'
      tier: 'P4'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: highAvailability ? 'ZoneRedundant' : 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    createMode: 'Create'
  }
}

// At least one Entra administrator is required when password auth is disabled,
// otherwise nothing can ever connect.
resource admins 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = [
  for admin in administrators: {
    parent: server
    name: admin.objectId
    properties: {
      principalName: admin.principalName
      principalType: admin.principalType
      tenantId: tenant().tenantId
    }
  }
]

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    admins
  ]
}

// Container Apps on a Consumption-only environment has no stable outbound IP
// range, so without a VNet this is the available option (ADR-0011). The special
// 0.0.0.0 rule means "Azure services", not "the internet"; combined with
// Entra-only auth, an attacker needs a valid token from this tenant, not just
// network reach.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: server
  name: 'AllowAllAzureServicesAndResources'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
  dependsOn: [
    database
  ]
}

// Require TLS and log slow queries. Enforced here so a portal change cannot
// silently weaken them.
resource requireSsl 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: server
  name: 'require_secure_transport'
  properties: {
    value: 'ON'
    source: 'user-override'
  }
  dependsOn: [
    allowAzureServices
  ]
}

output id string = server.id
output name string = server.name
output fqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
output sslEnforced string = requireSsl.properties.value
