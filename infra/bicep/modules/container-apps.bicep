// Container Apps: the environment, the API app and the migration job.
//
// THE ENVIRONMENT IS CONSUMPTION-ONLY. There is deliberately no `workloadProfiles`
// property. Adding one — even a single Consumption profile — switches the
// environment to the workload-profile model, which bills an Environment
// Management fee of ~$0.13/hr (~$95/month). Consumption-only has no such fee and
// the free grant (180k vCPU-s, 360k GiB-s, 2M requests per month) covers a dev
// workload outright.
//
// The API and the migration job run the SAME image, differing only in arguments.
// One build, one digest, so what migrates the database is exactly what serves it.

@description('Container Apps environment name.')
param environmentName string

@description('API container app name.')
param apiName string

@description('Migration job name.')
param migrateJobName string

@description('Azure region.')
param location string

@description('Tags.')
param tags object

@description('Resource ID of the shared Log Analytics workspace.')
param logAnalyticsWorkspaceId string

@description('Container image, pinned by digest so a revision is immutable.')
param image string

@description('Resource ID of the API user-assigned identity.')
param apiIdentityResourceId string

@description('Client ID of the API identity, used by DefaultAzureCredential to pick it.')
param apiIdentityClientId string

@description('Resource ID of the migrator user-assigned identity.')
param migratorIdentityResourceId string

@description('Client ID of the migrator identity.')
param migratorIdentityClientId string

@description('Minimum replicas. 0 enables scale-to-zero.')
param minReplicas int

@description('Maximum replicas.')
param maxReplicas int

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Postgres server FQDN.')
param postgresFqdn string

@description('Application database name.')
param databaseName string

@description('Origins allowed to call the API. The frontend is on a different host, so CORS is required and is pinned to exactly that origin.')
param allowedCorsOrigins array

@description('Custom hostname for the API, or empty for none. See the two-pass note below.')
param customHostname string = ''

@description('Environment name, passed to the app as ASPNETCORE_ENVIRONMENT.')
param env string

@description('CPU cores per replica.')
param cpu string = '0.25'

@description('Memory per replica. Container Apps requires a 1:2 vCPU-to-GiB ratio.')
param memory string = '0.5Gi'

// Postgres usernames for managed identities are the identity's name.
var apiDbUser = last(split(apiIdentityResourceId, '/'))
var migratorDbUser = last(split(migratorIdentityResourceId, '/'))

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-02-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))
  scope: resourceGroup(split(logAnalyticsWorkspaceId, '/')[2], split(logAnalyticsWorkspaceId, '/')[4])
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

// ---------------------------------------------------------------------------
// API
// ---------------------------------------------------------------------------

var commonEnvVars = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: env == 'prod' ? 'Production' : 'Development'
  }
  {
    name: 'BALBOA_DB_HOST'
    value: postgresFqdn
  }
  {
    name: 'BALBOA_DB_NAME'
    value: databaseName
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
]

resource api 'Microsoft.App/containerApps@2025-01-01' = {
  name: apiName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        corsPolicy: {
          allowedOrigins: allowedCorsOrigins
          allowedMethods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          allowCredentials: false
          maxAge: 3600
        }
        // A custom domain needs the CNAME and asuid TXT records to exist and a
        // managed certificate to be issued, so it is a SECOND deploy: first pass
        // with customHostname empty, add DNS, then redeploy with it set.
        customDomains: customHostname == ''
          ? []
          : [
              {
                name: customHostname
                bindingType: 'SniEnabled'
                certificateId: managedCertificate.id
              }
            ]
      }
      // No `registries` block: the ghcr.io image is public, so there is nothing
      // to authenticate with and no registry credential to store (ADR-0016).
      maxInactiveRevisions: 2
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat(commonEnvVars, [
            {
              name: 'BALBOA_DB_USER'
              value: apiDbUser
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: apiIdentityClientId
            }
          ])
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 3
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              // Scale-to-zero means a cold start on the first request. A startup
              // probe keeps the liveness probe from killing a container that is
              // merely still warming up.
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 2
              periodSeconds: 3
              failureThreshold: 20
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

resource managedCertificate 'Microsoft.App/managedEnvironments/managedCertificates@2025-01-01' = if (customHostname != '') {
  parent: environment
  name: 'cert-${replace(customHostname, '.', '-')}'
  location: location
  tags: tags
  properties: {
    subjectName: customHostname
    domainControlValidation: 'CNAME'
  }
}

// ---------------------------------------------------------------------------
// Migration job
//
// Manual trigger: CI runs it between deploying infrastructure and routing traffic
// to the new revision. Migrations are expand/contract, so the currently running
// revision keeps working while they apply.
// ---------------------------------------------------------------------------

resource migrateJob 'Microsoft.App/jobs@2025-01-01' = {
  name: migrateJobName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migratorIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: image
          args: ['migrate']
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(commonEnvVars, [
            {
              name: 'BALBOA_DB_USER'
              value: migratorDbUser
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: migratorIdentityClientId
            }
          ])
        }
      ]
    }
  }
}

output environmentId string = environment.id
output apiFqdn string = api.properties.configuration.ingress.fqdn
output apiName string = api.name
output migrateJobName string = migrateJob.name

// Goes into the asuid.<subdomain> TXT record that proves domain ownership.
output customDomainVerificationId string = api.properties.customDomainVerificationId
