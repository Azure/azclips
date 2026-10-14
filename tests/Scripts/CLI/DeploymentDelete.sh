#!/usr/bin/env bash
# Resource group deployment delete smoke test for the azclips CLI surface.
# Exercises `az deployment group delete`. Deleting a deployment removes its history record and
# operations only; it never deletes the resources the deployment created or the resource group.
# Two behaviours are pinned here because they surprise people:
#   1. Deleting a name that does not exist succeeds silently: ARM answers 204 either way.
#   2. A deployment that is still running cannot be deleted (DeploymentActive); cancel it first.
# The terminal record comes from `az deployment group create` with an empty template, so it creates
# nothing. The running record comes from an `az rest` PUT, which does no LRO polling and returns while
# the deployment is still running. Deployment names are timestamped, so each run acts on the records
# it just created; both records and the storage account the running one may create are deleted on exit.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. A resource group must already exist. This script creates and deletes only the deployments and
#      a storage account; it never creates or deletes the resource group. If you don't have one, run:
#        ./tests/Scripts/CLI/CreateResourceGroup.sh <resource-group> -l <location> -s <subscription-id>
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription holding the test resource group
#
# Optional:
#   AZCLIPS_TENANT_ID      - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP - existing resource group to deploy into; defaults to azclips-test-group-1
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentDelete.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
TIMESTAMP="$(date +%Y%m%d%H%M%S)"
DEPLOYMENT_NAME="azclips-test-deployment-delete-$TIMESTAMP"
RUNNING_DEPLOYMENT_NAME="azclips-test-deployment-delete-running-$TIMESTAMP"
STORAGE_ACCOUNT=azclipsdeploydelete

RUNNING_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$RUNNING_DEPLOYMENT_NAME?api-version=2025-04-01"
STORAGE_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01"

WORK_DIR="$(mktemp -d)"
# az.exe can't open Git Bash paths (/tmp/...); `pwd -W` gives the Windows form.
TEMPLATE_FILE="$(cd "$WORK_DIR" && { pwd -W 2>/dev/null || pwd; })/empty-template.json"

# Always remove the temp dir, the storage account and this run's deployment records, even if a
# check fails. An account whose create was in flight answers 409 until it settles, so the delete is
# retried. A leaked account fails the run.
cleanup() {
  local exit_code=$?
  rm -rf "$WORK_DIR"

  echo -e "\n=== [deployment group cancel] cleanup $RUNNING_DEPLOYMENT_NAME ==="
  "$AZ" deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME" > /dev/null 2>&1 || true

  echo -e "\n=== [rest] cleanup storage account $STORAGE_ACCOUNT ==="
  local status=""
  for _ in $(seq 1 12); do
    status="$("$AZ" rest --method DELETE --uri "$STORAGE_URI" --query statusCode -o tsv 2>/dev/null || true)"
    if [[ "$status" == 200 || "$status" == 204 ]]; then
      break
    fi
    sleep 5
  done
  if [[ "$status" == 200 || "$status" == 204 ]]; then
    echo "storage account delete returned $status"
  else
    echo "storage account $STORAGE_ACCOUNT delete never succeeded (last status '$status'); it may still exist" >&2
    exit_code=1
  fi

  echo -e "\n=== [deployment group delete] cleanup $DEPLOYMENT_NAME, $RUNNING_DEPLOYMENT_NAME ==="
  "$AZ" deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" || true
  # A just-cancelled deployment answers DeploymentActive until the cancel settles, so retry.
  for _ in $(seq 1 15); do
    if "$AZ" deployment group delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$RUNNING_DEPLOYMENT_NAME" > /dev/null 2>&1; then
      break
    fi
    sleep 4
  done
  exit "$exit_code"
}
trap cleanup EXIT

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo -e "\n=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

# Single-quoted heredoc: the template's "$schema" key must not be expanded by the shell.
cat > "$WORK_DIR/empty-template.json" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": []
}
JSON

echo -e "\n=== [deployment group create] arrange $DEPLOYMENT_NAME ==="
"$AZ" deployment group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-file "$TEMPLATE_FILE" \
  > /dev/null
STATE="$("$AZ" deployment group show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --query properties.provisioningState -o tsv)"
if [[ "$STATE" != "Succeeded" ]]; then
  echo "arranged deployment is '$STATE', expected 'Succeeded'" >&2
  exit 1
fi
echo "provisioningState: $STATE"

echo -e "\n=== [deployment group delete] terminal deployment ==="
# Delete waits for the LRO and has no response body, so the command must print nothing.
OUTPUT="$("$AZ" deployment group delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME")"
if [[ -n "${OUTPUT//[[:space:]]/}" ]]; then
  echo "delete printed '$OUTPUT', expected no output" >&2
  exit 1
fi
echo "delete exited 0 with no output"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment group show] deleted deployment must 404 ==="
if OUTPUT="$("$AZ" deployment group show \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$DEPLOYMENT_NAME" 2>&1)"; then
  echo "show of a deleted deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentNotFound* ]]; then
  echo "deleted deployment failed without DeploymentNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentNotFound"

echo -e "\n=== [deployment group delete] already deleted, default subscription ==="
"$AZ" deployment group delete \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME"
echo "delete of an already deleted deployment exited 0"

echo -e "\n=== [deployment group delete] never existed ==="
"$AZ" deployment group delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-deployment-does-not-exist
echo "delete of a missing deployment exited 0"

echo -e "\n=== [deployment group delete] missing resource group must 404 ==="
if OUTPUT="$("$AZ" deployment group delete \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group azclips-test-group-does-not-exist \
      --name "$DEPLOYMENT_NAME" 2>&1)"; then
  echo "delete in a missing resource group succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *ResourceGroupNotFound* ]]; then
  echo "missing resource group failed without ResourceGroupNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: ResourceGroupNotFound"

echo -e "\n=== [deployment group delete] --name is required ==="
if "$AZ" deployment group delete \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
  echo "delete without --name succeeded" >&2
  exit 1
fi
echo "Expected failure: delete requires --name"

RUNNING_TEMPLATE_PAYLOAD="$(cat <<'JSON'
{
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Storage/storageAccounts",
          "apiVersion": "2023-01-01",
          "name": "azclipsdeploydelete",
          "location": "[resourceGroup().location]",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
JSON
)"

echo -e "\n=== [rest] arrange running deployment $RUNNING_DEPLOYMENT_NAME ==="
STATUS="$("$AZ" rest \
  --method PUT \
  --uri "$RUNNING_URI" \
  --body "$RUNNING_TEMPLATE_PAYLOAD" \
  --query statusCode -o tsv)"
if [[ "$STATUS" != 201 ]]; then
  echo "arrange PUT returned '$STATUS', expected 201" >&2
  exit 1
fi
echo "PUT returned $STATUS"

echo -e "\n=== [deployment group delete] running deployment must be rejected ==="
if OUTPUT="$("$AZ" deployment group delete \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$RUNNING_DEPLOYMENT_NAME" 2>&1)"; then
  echo "delete of a running deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentActive* ]]; then
  echo "running deployment failed without DeploymentActive: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentActive"

echo -e "\n=== [deployment group cancel] running deployment ==="
"$AZ" deployment group cancel \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$RUNNING_DEPLOYMENT_NAME"
STATE=""
for _ in $(seq 1 30); do
  STATE="$("$AZ" deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME" \
    --query properties.provisioningState -o tsv)"
  if [[ "$STATE" == Canceled || "$STATE" == Succeeded || "$STATE" == Failed ]]; then
    break
  fi
  sleep 2
done
if [[ "$STATE" != Canceled && "$STATE" != Succeeded && "$STATE" != Failed ]]; then
  echo "running deployment never reached a terminal state (last '$STATE')" >&2
  exit 1
fi
echo "provisioningState: $STATE"

echo -e "\n=== [deployment group delete] cancelled deployment ==="
"$AZ" deployment group delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$RUNNING_DEPLOYMENT_NAME"
echo "delete of the cancelled deployment exited 0"

echo -e "\nAll deployment delete smoke checks completed."
