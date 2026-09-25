#!/usr/bin/env bash
# Live smoke test for az group exists.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in to the target subscription.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource group
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/GroupExists.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%Y%m%d%H%M%S)"
RESOURCE_GROUP="azclips-group-exists-$STAMP-$$-$RANDOM"
CREATED=0

cleanup() {
  local exit_code=$?
  if [[ "$CREATED" == "1" ]]; then
    "$AZ" rest \
      --method DELETE \
      --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" >/dev/null 2>&1 || true
  fi
  exit "$exit_code"
}
trap cleanup EXIT

echo "=== [group exists] missing resource group ==="
[[ "$("$AZ" group exists --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP")" == "false" ]]

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" >/dev/null
CREATED=1

echo "=== [group exists] existing resource group ==="
[[ "$("$AZ" group exists --subscription-id "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP")" == "true" ]]

echo "=== [group exists] legacy -n alias ==="
[[ "$("$AZ" group exists --subscription-id "$SUBSCRIPTION_ID" -n "$RESOURCE_GROUP")" == "true" ]]

echo "=== [group exists] legacy -g alias ==="
[[ "$("$AZ" group exists --subscription-id "$SUBSCRIPTION_ID" -g "$RESOURCE_GROUP")" == "true" ]]

echo "az group exists smoke checks completed."
