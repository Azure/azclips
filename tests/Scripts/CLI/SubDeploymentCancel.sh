#!/usr/bin/env bash
# Subscription scope deployment cancel smoke test for the azclips CLI surface (az deployment sub cancel).
# Only an Accepted or Running deployment can be cancelled, and `az deployment sub create` blocks until
# terminal, so the arrange step PUTs the deployment through `az rest`, which returns as soon as ARM
# accepts it. The template creates a resource group named after the deployment plus a storage account
# in it, which keeps the deployment running long enough to cancel. The deployment record and that
# resource group are deleted on exit.
#
# Prereqs: build the solution (dotnet build azclips.slnx) and sign in, or set AZCLIPS_TENANT_ID.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription to deploy into
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands
#   AZCLIPS_LOCATION  - deployment and resource group region; defaults to australiaeast
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentCancel.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-cancel-$(date +%y%m%d%H%M%S)"
TARGET=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME")
DEPLOYMENT_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Resources/deployments/$NAME?api-version=2025-04-01"

# Cancelling does not roll back what was created, so remove the resource group explicitly. It exists
# only if the deployment got that far, so a failed delete is a warning.
cleanup() {
  local exit_code=$?
  echo -e "\n=== cleanup $NAME ==="
  "$AZ" deployment sub cancel "${TARGET[@]}" > /dev/null 2>&1 || true
  "$AZ" deployment sub delete "${TARGET[@]}" > /dev/null 2>&1 || true
  "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$NAME" > /dev/null 2>&1 \
    || echo "WARNING: could not delete resource group $NAME; delete it manually if it exists" >&2
  exit "$exit_code"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }

state() { "$AZ" deployment sub show "${TARGET[@]}" --query properties.provisioningState -o tsv; }

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

# Single-quoted heredoc: the template's "$schema" keys must not be expanded by the shell.
PAYLOAD="$(cat <<'JSON'
{
  "location": "__LOCATION__",
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Resources/resourceGroups",
          "apiVersion": "2022-09-01",
          "name": "[deployment().name]",
          "location": "[deployment().location]"
        },
        {
          "type": "Microsoft.Resources/deployments",
          "apiVersion": "2022-09-01",
          "name": "storage",
          "resourceGroup": "[deployment().name]",
          "dependsOn": [ "[subscriptionResourceId('Microsoft.Resources/resourceGroups', deployment().name)]" ],
          "properties": {
            "mode": "Incremental",
            "template": {
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "resources": [
                {
                  "type": "Microsoft.Storage/storageAccounts",
                  "apiVersion": "2023-01-01",
                  "name": "[concat('azclips', uniqueString(deployment().name))]",
                  "location": "[deployment().location]",
                  "sku": { "name": "Standard_LRS" },
                  "kind": "StorageV2"
                }
              ]
            }
          }
        }
      ]
    }
  }
}
JSON
)"
PAYLOAD="${PAYLOAD//__LOCATION__/$LOCATION}"

echo -e "\n=== [rest] arrange running deployment $NAME ==="
STATUS="$("$AZ" rest --method PUT --uri "$DEPLOYMENT_URI" --body "$PAYLOAD" --query statusCode -o tsv)"
[[ "$STATUS" == 201 ]] || fail "arrange PUT returned '$STATUS', expected 201"

# The deployment is only cancellable while Accepted or Running; confirm that before cancelling.
for _ in $(seq 1 15); do
  STATE="$(state)"
  [[ "$STATE" == Accepted || "$STATE" == Running ]] && break
  sleep 2
done
[[ "$STATE" == Accepted || "$STATE" == Running ]] || fail "deployment settled at '$STATE' before it could be cancelled"

echo -e "\n=== [deployment sub cancel] running deployment ==="
# ARM answers 204 No Content, so the command must print nothing.
OUTPUT="$("$AZ" deployment sub cancel "${TARGET[@]}")"
[[ -z "${OUTPUT//[[:space:]]/}" ]] || fail "cancel printed '$OUTPUT', expected no output"

echo -e "\n=== [deployment sub show] state flips to Canceled ==="
# ARM flips provisioningState only after the in-flight work settles.
for _ in $(seq 1 30); do
  STATE="$(state)"
  [[ "$STATE" == Canceled ]] && break
  sleep 2
done
[[ "$STATE" == Canceled ]] || fail "provisioningState is '$STATE', expected 'Canceled'"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub cancel] terminal deployment ==="
if OUTPUT="$("$AZ" deployment sub cancel "${TARGET[@]}" 2>&1)"; then
  fail "cancel of a terminal deployment succeeded"
fi
[[ "$OUTPUT" == *DeploymentCannotBeCancelled* ]] || fail "terminal deployment failed without DeploymentCannotBeCancelled: $OUTPUT"

echo -e "\n=== [deployment sub cancel] missing deployment must 404 ==="
if OUTPUT="$("$AZ" deployment sub cancel --subscription-id "$SUBSCRIPTION_ID" \
      --name azclips-test-sub-does-not-exist 2>&1)"; then
  fail "cancel of a missing deployment succeeded"
fi
[[ "$OUTPUT" == *DeploymentNotFound* ]] || fail "missing deployment failed without DeploymentNotFound: $OUTPUT"

echo -e "\nAll deployment sub cancel smoke checks completed."
