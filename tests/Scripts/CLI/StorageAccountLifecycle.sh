#!/usr/bin/env bash
# Storage account lifecycle smoke test for the azclips CLI surface.
# Exercises create, show, list, and delete.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID             - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP        - resource group name; defaults to timestamped test group
#   AZCLIPS_STORAGE_ACCOUNT_NAME  - storage account name; defaults to timestamped lowercase name
#   AZCLIPS_LOCATION              - Azure region; defaults to eastus
#   AZCLIPS_KEEP_STORAGE_ACCOUNT  - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG            - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/StorageAccountLifecycle.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
STAMP="$(date +%y%m%d%H%M%S)"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-storage-it-$STAMP}"
ACCOUNT_NAME="${AZCLIPS_STORAGE_ACCOUNT_NAME:-azclipssa$STAMP}"

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_STORAGE_ACCOUNT:-0}" != "1" ]]; then
    echo "=== [storage account delete] cleanup ==="
    "$AZ" storage account delete \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$ACCOUNT_NAME" || true
  fi

  if [[ "${AZCLIPS_CLEANUP_RG:-0}" == "1" ]]; then
    echo "=== [group delete] cleanup resource group ==="
    "$AZ" rest \
      --method DELETE \
      --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" || true
  fi

  exit "$exit_code"
}
trap cleanup EXIT

"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo "=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== [storage account create] $ACCOUNT_NAME ==="
"$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACCOUNT_NAME" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2

echo "=== [storage account show] ==="
"$AZ" storage account show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACCOUNT_NAME"

echo "=== [storage account list] resource group ==="
"$AZ" storage account list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP"

echo "=== [storage account list] subscription ==="
"$AZ" storage account list --subscription-id "$SUBSCRIPTION_ID"

echo "=== [storage account delete] ==="
"$AZ" storage account delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACCOUNT_NAME"

echo "All storage account lifecycle smoke checks completed."