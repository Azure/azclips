#!/usr/bin/env bash
# Live smoke test for az deployment operation group list / show.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in to the target subscription.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource group
#
# Optional:
#   AZCLIPS_LOCATION - region for the test resource group (default: westus2)
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentOperation.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Prevent Git Bash/MSYS from rewriting slash-prefixed ARM resource IDs.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%Y%m%d%H%M%S)"
RESOURCE_GROUP="azclips-deployment-operation-$STAMP-$$-$RANDOM"
DEPLOYMENT_NAME="azclips-deployment-operation-$STAMP"
TEMPLATE_URI="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json"
CREATED=0

# Deleting the resource group removes the deployment, its operations, and every resource the
# template provisioned, so a single delete is the complete cleanup for this test.
cleanup() {
  local exit_code=$?
  if [[ "$CREATED" == "1" ]]; then
    "$AZ" rest \
      --method DELETE \
      --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" \
      >/dev/null 2>&1 || true
  fi
  exit "$exit_code"
}
trap cleanup EXIT

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" >/dev/null
CREATED=1

echo "=== [deployment group create] $DEPLOYMENT_NAME ==="
"$AZ" deployment group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-uri "$TEMPLATE_URI" \
  --mode Incremental >/dev/null

echo "=== [deployment operation group list] ==="
LIST_OUTPUT="$("$AZ" deployment operation group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME")"
echo "$LIST_OUTPUT"

mapfile -t OPERATION_IDS < <(
  printf '%s' "$LIST_OUTPUT" \
    | grep -oE '"operationId"[[:space:]]*:[[:space:]]*"[^"]+"' \
    | sed -E 's/.*"([^"]+)"$/\1/'
)
[[ "${#OPERATION_IDS[@]}" -gt 0 ]] || { echo "list returned no operations"; exit 1; }
echo "discovered ${#OPERATION_IDS[@]} operation id(s)"

echo "=== [deployment operation group list] legacy -g / -n aliases ==="
"$AZ" deployment operation group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$DEPLOYMENT_NAME" >/dev/null

echo "=== [deployment operation group show] single operation id ==="
"$AZ" deployment operation group show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --operation-ids "${OPERATION_IDS[0]}"

if [[ "${#OPERATION_IDS[@]}" -gt 1 ]]; then
  echo "=== [deployment operation group show] multiple operation ids ==="
  "$AZ" deployment operation group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --operation-ids "${OPERATION_IDS[0]}" "${OPERATION_IDS[1]}"
else
  echo "=== [deployment operation group show] multiple operation ids: skipped (only one operation) ==="
fi

echo "=== [deployment operation group show] unknown operation id must fail ==="
if "$AZ" deployment operation group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --operation-ids "00000000000000000000" >/dev/null 2>&1; then
  echo "UNEXPECTED: show succeeded for an unknown operation id"
  exit 1
fi
echo "EXPECTED FAILURE (unknown operation id)"

echo "az deployment operation group smoke checks completed."
