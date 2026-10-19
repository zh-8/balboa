// Dev environment parameters.
//
// Values that change per deploy (the image digest) or that bootstrap produced
// (the workspace ID, the break-glass admin) come from environment variables, so
// this file stays free of anything deploy-specific and nothing secret is ever
// committed. The deploy workflow and infra/scripts/what-if.sh set them.

using './main.bicep'

param env = 'dev'

// From the bootstrap outputs, surfaced as a GitHub environment variable.
param logAnalyticsWorkspaceId = readEnvironmentVariable('BALBOA_LOG_ANALYTICS_ID')

// Pinned by digest, not tag: a revision must be immutable so a rollback is exact.
// e.g. ghcr.io/zh-8/balboa-api@sha256:abc...
param apiImage = readEnvironmentVariable('BALBOA_API_IMAGE')

// Break-glass Postgres administrator, so there is a way in that does not depend
// on CI. Not a secret: an object ID is an identifier, not a credential.
param adminPrincipalObjectId = readEnvironmentVariable('BALBOA_ADMIN_OBJECT_ID')
param adminPrincipalName = readEnvironmentVariable('BALBOA_ADMIN_PRINCIPAL_NAME')

// --- Dev sizing: cheapest thing that is still representative ---

// Scale to zero. A cold start of a few seconds is an acceptable trade for $0 idle.
param apiMinReplicas = 0
param apiMaxReplicas = 2

// The 12-month free allowance: B1ms, 32 GB. 750 free hours covers 24/7 on one
// server. Do NOT raise these without re-checking the bill.
param postgresSkuName = 'Standard_B1ms'
param postgresStorageGb = 32
param postgresBackupRetentionDays = 7

// Off in dev so the vault can be recreated if the stack is torn down. On in prod,
// where it also reserves the name for 90 days.
param keyVaultPurgeProtection = false

// Static Web Apps is not offered in Italy North (ADR-0007).
param staticWebAppLocation = 'westeurope'

// Custom domains are a SECOND pass: deploy once with these empty, add the CNAME
// and asuid TXT records at GoDaddy, then set them and redeploy so the managed
// certificate can be issued. See docs/runbooks/deploy-rollback.md.
param frontendHostname = ''
param apiHostname = ''
