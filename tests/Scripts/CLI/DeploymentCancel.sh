#!/usr/bin/env bash
# Resource group deployment cancel smoke test for the azclips CLI surface.
# Exercises `az deployment group cancel`. A deployment can only be cancelled while it is Accepted or
# Running, and `az deployment group create` blocks until the deployment is terminal, so the arrange
# step issues the deployment PUT through `az rest`, which does no LRO polling and returns while the
# deployment is still running. The deployment name is timestamped, so each run cancels the record it
# just created; that record and the storage account it may have partially created are deleted on exit.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. A resource group must already exist. This script creates and deletes only the deployment and
#      its storage account; it never creates or deletes the resource group. If you don't have one, run:
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
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentCancel.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
DEPLOYMENT_NAME="azclips-test-deployment-cancel-$(date +%Y%m%d%H%M%S)"
# Storage account names are globally unique across Azure, so the name carries the first 8 characters
# of the subscription id: stable per subscription, but no clash with other subscriptions.
STORAGE_ACCOUNT="azclipscancel$(printf '%s' "${SUBSCRIPTION_ID:0:8}" | tr '[:upper:]' '[:lower:]')"

DEPLOYMENT_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2025-04-01"
STORAGE_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01"

# Always delete the storage account and this run's deployment record, even if a check fails.
# Cancelling does not roll back what the deployment already created, and an account whose create
# was in flight answers 409 until it settles, so the delete is retried. A leaked account fails the run.
cleanup() {
  local exit_code=$?
  echo -e "\n=== [deployment group cancel] cleanup $DEPLOYMENT_NAME ==="
  "$AZ" deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" > /dev/null 2>&1 || true

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

  echo -e "\n=== [deployment group delete] cleanup $DEPLOYMENT_NAME ==="
  "$AZ" deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" || true
  exit "$exit_code"
}
trap cleanup EXIT

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo -e "\n=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

# Single-quoted heredoc: the template's "$schema" key must not be expanded by the shell; the storage
# account name is substituted afterwards.
TEMPLATE_PAYLOAD="$(cat <<'JSON'
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
          "name": "__STORAGE_ACCOUNT__",
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
TEMPLATE_PAYLOAD="${TEMPLATE_PAYLOAD//__STORAGE_ACCOUNT__/$STORAGE_ACCOUNT}"

echo -e "\n=== [rest] arrange running deployment $DEPLOYMENT_NAME ==="
STATUS="$("$AZ" rest \
  --method PUT \
  --uri "$DEPLOYMENT_URI" \
  --body "$TEMPLATE_PAYLOAD" \
  --query statusCode -o tsv)"
if [[ "$STATUS" != 201 ]]; then
  echo "arrange PUT returned '$STATUS', expected 201" >&2
  exit 1
fi
echo "PUT returned $STATUS"

# The deployment is only cancellable while Accepted or Running; confirm that before cancelling.
STATE=""
for _ in $(seq 1 15); do
  STATE="$("$AZ" deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query properties.provisioningState -o tsv)"
  if [[ "$STATE" == Accepted || "$STATE" == Running ]]; then
    break
  fi
  sleep 2
done
if [[ "$STATE" != Accepted && "$STATE" != Running ]]; then
  echo "deployment settled at '$STATE' before it could be cancelled" >&2
  exit 1
fi
echo "provisioningState: $STATE"

echo -e "\n=== [deployment group cancel] running deployment ==="
# ARM answers 204 No Content, so the command must print nothing.
OUTPUT="$("$AZ" deployment group cancel \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME")"
if [[ -n "${OUTPUT//[[:space:]]/}" ]]; then
  echo "cancel printed '$OUTPUT', expected no output" >&2
  exit 1
fi
echo "cancel exited 0 with no output"

echo -e "\n=== [deployment group show] state flips to Canceled ==="
# ARM flips provisioningState only after the in-flight storage create settles.
for _ in $(seq 1 30); do
  STATE="$("$AZ" deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query properties.provisioningState -o tsv)"
  if [[ "$STATE" == Canceled ]]; then
    break
  fi
  sleep 2
done
if [[ "$STATE" != Canceled ]]; then
  echo "provisioningState is '$STATE', expected 'Canceled'" >&2
  exit 1
fi
echo "provisioningState: $STATE"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment group cancel] terminal deployment, default subscription ==="
if OUTPUT="$("$AZ" deployment group cancel \
      --resource-group "$RESOURCE_GROUP" \
      --name "$DEPLOYMENT_NAME" 2>&1)"; then
  echo "cancel of a terminal deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentCannotBeCancelled* ]]; then
  echo "terminal deployment failed without DeploymentCannotBeCancelled: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentCannotBeCancelled"

echo -e "\n=== [deployment group cancel] missing deployment must 404 ==="
if OUTPUT="$("$AZ" deployment group cancel \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-deployment-does-not-exist 2>&1)"; then
  echo "cancel of a missing deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentNotFound* ]]; then
  echo "missing deployment failed without DeploymentNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentNotFound"

echo -e "\n=== [deployment group cancel] missing resource group must 404 ==="
if OUTPUT="$("$AZ" deployment group cancel \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group azclips-test-group-does-not-exist \
      --name "$DEPLOYMENT_NAME" 2>&1)"; then
  echo "cancel in a missing resource group succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *ResourceGroupNotFound* ]]; then
  echo "missing resource group failed without ResourceGroupNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: ResourceGroupNotFound"

echo -e "\n=== [deployment group cancel] --name is required ==="
if "$AZ" deployment group cancel \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
  echo "cancel without --name succeeded" >&2
  exit 1
fi
echo "Expected failure: cancel requires --name"

echo -e "\nAll deployment cancel smoke checks completed."
