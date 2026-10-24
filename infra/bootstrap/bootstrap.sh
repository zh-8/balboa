#!/usr/bin/env bash
#
# Balboa bootstrap — run ONCE per environment, by a human with Owner on the
# subscription. Everything after this is done by CI.
#
#   ./infra/bootstrap/bootstrap.sh dev
#
# Safe to re-run: the Bicep is declarative and the GitHub calls are PUT-by-name.
#
# SAFETY
#   * Asserts the expected subscription and tenant before touching anything.
#   * Only ever names resources matching rg-balboa-*; never deletes a group.
#   * rg-cairobits (an unrelated project in this subscription) cannot be in
#     scope, because no operation here targets anything outside those names.
#   * Shows `what-if` and waits for confirmation before writing.
#
# See docs/runbooks/bootstrap.md.

set -euo pipefail

readonly ENV="${1:-dev}"

# Assigned before being made readonly: `readonly X="$(cmd)"` masks the command's exit
# status, so a failure of the cd would pass silently under `set -e` (SC2155).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly TEMPLATE="${SCRIPT_DIR}/bootstrap.bicep"

# ---- expected context -------------------------------------------------------
# The subscription id, tenant id and alert email are NOT hardcoded, because this
# repository is public. None of the three is a credential, but a tenant id is a
# durable identifier of an Entra directory that can be used to enumerate its login
# and federation configuration, and a public repo is effectively permanent: forks
# and search indexes outlive a deletion. So they are supplied from outside the repo.
#
# The assertion further down is a genuine safety control -- it is what keeps this
# script off the wrong subscription, and so away from rg-cairobits. It is unchanged;
# it just reads what to expect from the environment rather than from a tracked file.
#
# Set them per shell, or put them in infra/bootstrap/.env (gitignored). See
# infra/bootstrap/.env.example and docs/runbooks/bootstrap.md.
if [[ -f "${SCRIPT_DIR}/.env" ]]; then
  # shellcheck disable=SC1091  # operator-supplied, intentionally untracked
  source "${SCRIPT_DIR}/.env"
fi

readonly EXPECTED_SUBSCRIPTION="${BALBOA_SUBSCRIPTION_ID:?not set — see infra/bootstrap/.env.example}"
readonly EXPECTED_TENANT="${BALBOA_TENANT_ID:?not set — see infra/bootstrap/.env.example}"
readonly ALERT_EMAIL="${BALBOA_ALERT_EMAIL:?not set — see infra/bootstrap/.env.example}"

# Public by nature: the repo these federated credentials are scoped to, and the region.
readonly GITHUB_OWNER="zh-8"
readonly GITHUB_REPO="balboa"
readonly LOCATION="italynorth"
readonly BUDGET_AMOUNT=10

readonly REQUIRED_PROVIDERS=(
  Microsoft.App
  Microsoft.DBforPostgreSQL
  Microsoft.KeyVault
  Microsoft.ManagedIdentity
  Microsoft.Network
  Microsoft.OperationalInsights
  Microsoft.Insights
  Microsoft.Storage
  Microsoft.Web
)

red()  { printf '\033[31m%s\033[0m\n' "$*"; }
grn()  { printf '\033[32m%s\033[0m\n' "$*"; }
ylw()  { printf '\033[33m%s\033[0m\n' "$*"; }
step() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
die()  { red "ERROR: $*"; exit 1; }

case "$ENV" in
  dev|prod) ;;
  *) die "environment must be 'dev' or 'prod', got '$ENV'" ;;
esac

# ---------------------------------------------------------------------------
step "1/5 Verifying context"
# ---------------------------------------------------------------------------

command -v az >/dev/null || die "az CLI not found"
command -v gh >/dev/null || die "gh CLI not found"

account_json="$(az account show -o json 2>/dev/null)" \
  || die "not logged in. Run: az login"

actual_sub="$(jq -r '.id' <<<"$account_json")"
actual_tenant="$(jq -r '.tenantId' <<<"$account_json")"
sub_state="$(jq -r '.state' <<<"$account_json")"

[[ "$actual_sub" == "$EXPECTED_SUBSCRIPTION" ]] \
  || die "wrong subscription: expected $EXPECTED_SUBSCRIPTION, got $actual_sub"
[[ "$actual_tenant" == "$EXPECTED_TENANT" ]] \
  || die "wrong tenant: expected $EXPECTED_TENANT, got $actual_tenant"

# `az account show` serves a cached login profile, so ask ARM for the real state.
live_state="$(az rest --method get \
  --url "https://management.azure.com/subscriptions/${actual_sub}?api-version=2022-12-01" \
  --query state -o tsv)"

if [[ "$live_state" != "Enabled" ]]; then
  red "Subscription state is '$live_state', not 'Enabled'."
  red "A Warned or Disabled subscription can read and delete resources but CANNOT create them."
  red "Upgrade it to pay-as-you-go in the portal, then re-run this script."
  exit 1
fi

grn "Subscription $actual_sub ($sub_state / live: $live_state) in tenant $actual_tenant"

admin_object_id="$(az ad signed-in-user show --query id -o tsv)"
admin_principal_name="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
grn "Break-glass admin: $admin_principal_name ($admin_object_id)"

gh auth status >/dev/null 2>&1 || die "gh is not authenticated. Run: gh auth login"
gh repo view "${GITHUB_OWNER}/${GITHUB_REPO}" >/dev/null 2>&1 \
  || die "repo ${GITHUB_OWNER}/${GITHUB_REPO} not reachable. Create it first."

# ---------------------------------------------------------------------------
step "2/5 Registering resource providers"
# ---------------------------------------------------------------------------

for provider in "${REQUIRED_PROVIDERS[@]}"; do
  state="$(az provider show -n "$provider" --query registrationState -o tsv 2>/dev/null || echo NotRegistered)"
  if [[ "$state" == "Registered" ]]; then
    printf '  %-32s already registered\n' "$provider"
  else
    printf '  %-32s registering...' "$provider"
    az provider register -n "$provider" --wait
    printf ' done\n'
  fi
done

# ---------------------------------------------------------------------------
step "3/5 Previewing changes (what-if)"
# ---------------------------------------------------------------------------

deployment_name="balboa-bootstrap-${ENV}-$(date -u +%Y%m%d%H%M%S)"

common_params=(
  --location "$LOCATION"
  --template-file "$TEMPLATE"
  --parameters
  "location=$LOCATION"
  "env=$ENV"
  "githubOwner=$GITHUB_OWNER"
  "githubRepo=$GITHUB_REPO"
  "budgetAmount=$BUDGET_AMOUNT"
  "alertEmail=$ALERT_EMAIL"
  "adminPrincipalObjectId=$admin_object_id"
  "adminPrincipalName=$admin_principal_name"
)

az deployment sub what-if --name "$deployment_name" "${common_params[@]}"

ylw ""
ylw "The plan above creates resource groups, Log Analytics, the GitHub OIDC identity,"
ylw "its RBAC and a \$${BUDGET_AMOUNT}/month budget. Expected running cost: ~\$0."
read -r -p "Proceed? Type 'yes' to continue: " confirm
[[ "$confirm" == "yes" ]] || die "aborted by operator"

# ---------------------------------------------------------------------------
step "4/5 Deploying"
# ---------------------------------------------------------------------------

outputs="$(az deployment sub create --name "$deployment_name" "${common_params[@]}" \
  --query properties.outputs -o json)"

get_output() { jq -r ".[\"$1\"].value" <<<"$outputs"; }

shared_rg="$(get_output sharedResourceGroup)"
env_rg="$(get_output envResourceGroup)"
log_id="$(get_output logAnalyticsWorkspaceId)"
client_id="$(get_output githubIdentityClientId)"

grn "Shared RG:        $shared_rg"
grn "Environment RG:   $env_rg"
grn "Deploy client ID: $client_id"

# ---------------------------------------------------------------------------
step "5/5 Configuring GitHub"
# ---------------------------------------------------------------------------

# Environment first; the OIDC federated credential is scoped to it, so a workflow
# outside this environment cannot assume the identity.
gh api -X PUT "repos/${GITHUB_OWNER}/${GITHUB_REPO}/environments/${ENV}" \
  --silent >/dev/null
grn "GitHub environment '${ENV}' ready"

# Variables, not secrets: with OIDC none of these is a credential.
set_var() {
  gh variable set "$1" --env "$ENV" --repo "${GITHUB_OWNER}/${GITHUB_REPO}" --body "$2"
  printf '  %-28s = %s\n' "$1" "$2"
}

set_var AZURE_CLIENT_ID "$client_id"
set_var AZURE_TENANT_ID "$actual_tenant"
set_var AZURE_SUBSCRIPTION_ID "$actual_sub"
set_var AZURE_RESOURCE_GROUP "$env_rg"
set_var BALBOA_LOG_ANALYTICS_ID "$log_id"
set_var BALBOA_ADMIN_OBJECT_ID "$admin_object_id"
set_var BALBOA_ADMIN_PRINCIPAL_NAME "$admin_principal_name"

# Branch protection, kept as JSON so it is reviewable and re-appliable.
ruleset_file="${SCRIPT_DIR}/github/rulesets/main.json"
if [[ -f "$ruleset_file" ]]; then
  existing_id="$(gh api "repos/${GITHUB_OWNER}/${GITHUB_REPO}/rulesets" \
    --jq '.[] | select(.name=="main-protection") | .id' 2>/dev/null || true)"
  if [[ -n "$existing_id" ]]; then
    gh api -X PUT "repos/${GITHUB_OWNER}/${GITHUB_REPO}/rulesets/${existing_id}" \
      --input "$ruleset_file" --silent >/dev/null
    grn "Ruleset 'main-protection' updated"
  else
    gh api -X POST "repos/${GITHUB_OWNER}/${GITHUB_REPO}/rulesets" \
      --input "$ruleset_file" --silent >/dev/null
    grn "Ruleset 'main-protection' created"
  fi
fi

step "Done"
grn "Bootstrap complete for '$ENV'."
cat <<EOF

Next:
  1. Check the budget exists:
       az consumption budget list -o table
  2. Deploy the stack (the 'Deploy dev' workflow, or):
       make what-if ENV=$ENV
  3. Grant the Postgres roles once the server exists — see
     docs/runbooks/bootstrap.md, "Postgres role mapping". This is the one step
     that is not automated, because it needs a psql session as the Entra admin.

Teardown, if you need it:
  az group delete -n $env_rg --yes
  az group delete -n $shared_rg --yes
  az consumption budget delete --budget-name budget-balboa-$ENV
EOF
