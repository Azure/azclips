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
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentOperationGroup.sh
set -euo pipefail

# Keep Git Bash from rewriting the ARM resource path passed to `az rest`.
export MSYS_NO_PATHCONV=1

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%Y%m%d%H%M%S)"
RESOURCE_GROUP="azclips-deployment-op-$STAMP-$$-$RANDOM"
DEPLOYMENT_NAME="azclips-deployment-op-$STAMP"
TEMPLATE_URI="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json"
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

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" >/dev/null
CREATED=1

# The deployment must reach a terminal state before it reports any operations; the create
# command already blocks until then.
echo "=== [deployment group create] $DEPLOYMENT_NAME ==="
"$AZ" deployment group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-uri "$TEMPLATE_URI" \
  --mode Incremental >/dev/null

echo "=== [deployment operation group list] ==="
OPERATION_IDS="$("$AZ" deployment operation group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --query "[].operationId" -o tsv)"
[[ -n "$OPERATION_IDS" ]]

mapfile -t IDS <<< "$OPERATION_IDS"
echo "operations returned: ${#IDS[@]}"

echo "=== [deployment operation group list] legacy -g / -n aliases ==="
"$AZ" deployment operation group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$DEPLOYMENT_NAME" >/dev/null

echo "=== [deployment operation group show] single id ==="
SHOWN="$("$AZ" deployment operation group show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --operation-ids "${IDS[0]}" \
  --query "[].operationId" -o tsv)"
[[ "$SHOWN" == "${IDS[0]}" ]]

if (( ${#IDS[@]} > 1 )); then
  echo "=== [deployment operation group show] multiple ids fan out to one array ==="
  COUNT="$("$AZ" deployment operation group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --operation-ids "${IDS[0]}" "${IDS[1]}" \
    --query "length(@)" -o tsv)"
  [[ "$COUNT" == "2" ]]
fi

echo "az deployment operation group smoke checks completed."
