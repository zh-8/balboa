#!/usr/bin/env bash
#
# Preview infrastructure changes for an environment. READ-ONLY: what-if never
# writes anything.
#
#   ./infra/scripts/what-if.sh dev
#   make what-if ENV=dev
#
# Reads the same environment variables the deploy workflow uses, so what you see
# here is what CI would apply. Run it before every infra change.

set -euo pipefail

readonly ENV="${1:-dev}"
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly BICEP_DIR="${SCRIPT_DIR}/../bicep"
readonly TEMPLATE="${BICEP_DIR}/main.bicep"
readonly PARAM_FILE="${BICEP_DIR}/env/${ENV}.bicepparam"

die() { printf '\033[31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

case "$ENV" in
  dev|prod) ;;
  *) die "environment must be 'dev' or 'prod', got '$ENV'" ;;
esac

[[ -f "$PARAM_FILE" ]] || die "no parameter file at $PARAM_FILE"

command -v az >/dev/null || die "az CLI not found"
az account show >/dev/null 2>&1 || die "not logged in. Run: az login"

# The .bicepparam file reads these; fail loudly rather than deploying something
# half-configured.
: "${AZURE_RESOURCE_GROUP:?set AZURE_RESOURCE_GROUP (e.g. rg-balboa-dev-itn)}"
: "${BALBOA_LOG_ANALYTICS_ID:?set BALBOA_LOG_ANALYTICS_ID (from the bootstrap outputs)}"
: "${BALBOA_API_IMAGE:?set BALBOA_API_IMAGE (e.g. ghcr.io/zh-8/balboa-api@sha256:...)}"
: "${BALBOA_ADMIN_OBJECT_ID:?set BALBOA_ADMIN_OBJECT_ID}"
: "${BALBOA_ADMIN_PRINCIPAL_NAME:?set BALBOA_ADMIN_PRINCIPAL_NAME}"

if [[ "$BALBOA_API_IMAGE" != *"@sha256:"* ]]; then
  printf '\033[33mWARNING: BALBOA_API_IMAGE is not pinned by digest.\033[0m\n'
  printf '\033[33m         Tags are mutable, so a rollback would not be exact.\033[0m\n'
fi

printf '\033[1mwhat-if: %s -> %s\033[0m\n\n' "$ENV" "$AZURE_RESOURCE_GROUP"

az deployment group what-if \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file "$TEMPLATE" \
  --parameters "$PARAM_FILE" \
  --result-format FullResourcePayloads
