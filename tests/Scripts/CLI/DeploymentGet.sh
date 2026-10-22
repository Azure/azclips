#!/usr/bin/env bash
# Resource group deployment read smoke test for the azclips CLI surface.
# Exercises `az deployment group show` and `az deployment group list`. Show and list are READ-ONLY;
# the one write below uses a timestamped deployment name, so each run reads back the record it
# just created rather than one left by an earlier run; that record is deleted on exit. The storage
# account it provisions keeps a fixed name, so repeated runs reuse the same account.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. A resource group must already exist. This script creates and deletes only the deployment;
#      it never creates or deletes the resource group. If you don't have one, run:
#        ./tests/Scripts/CLI/CreateResourceGroup.sh <resource-group> -l <location> -s <subscription-id>
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription holding the test resource group
#
# Optional:
#   AZCLIPS_TENANT_ID      - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP - existing resource group to read; defaults to azclips-test-group-1
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentGet.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$(cd "$REPO_ROOT/tests/Scripts/Bicep" && { pwd -W 2>/dev/null || pwd; })"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
DEPLOYMENT_NAME="azclips-test-deployment-get-$(date +%Y%m%d%H%M%S)"

# Always delete this run's deployment record, even if a check fails.
cleanup() {
  local exit_code=$?
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

echo -e "\n=== [deployment group create] arrange $DEPLOYMENT_NAME ==="
"$AZ" deployment group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameter "storageAccountName=azclipsdeploymentget" \
  --mode Incremental \
  > /dev/null

echo -e "\n=== [deployment group show] by name ==="
STATE="$("$AZ" deployment group show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --query properties.provisioningState -o tsv)"
if [[ "$STATE" != "Succeeded" ]]; then
  echo "show returned provisioningState '$STATE', expected 'Succeeded'" >&2
  exit 1
fi
echo "provisioningState: $STATE"

echo -e "\n=== [deployment group list] by resource group ==="
NAMES="$("$AZ" deployment group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].name" -o tsv)"
if ! grep -qx "$DEPLOYMENT_NAME" <<< "$NAMES"; then
  echo "list did not return $DEPLOYMENT_NAME" >&2
  exit 1
fi
echo "list returned $DEPLOYMENT_NAME"

echo -e "\n=== [deployment group list] --filter provisioningState ==="
NAMES="$("$AZ" deployment group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --filter "provisioningState eq 'Succeeded'" \
  --query "[].name" -o tsv)"
if ! grep -qx "$DEPLOYMENT_NAME" <<< "$NAMES"; then
  echo "filtered list did not return $DEPLOYMENT_NAME" >&2
  exit 1
fi
STATES="$("$AZ" deployment group list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --filter "provisioningState eq 'Succeeded'" \
  --query "[].properties.provisioningState" -o tsv | tr -d '\r' | sort -u)"
if [[ "$STATES" != "Succeeded" ]]; then
  echo "filtered list returned other states: $STATES" >&2
  exit 1
fi
echo "filtered list returned $DEPLOYMENT_NAME and only Succeeded deployments"

echo -e "\n=== [deployment group list] default subscription ==="
NAMES="$("$AZ" deployment group list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].name" -o tsv)"
if ! grep -qx "$DEPLOYMENT_NAME" <<< "$NAMES"; then
  echo "default-subscription list did not return $DEPLOYMENT_NAME" >&2
  exit 1
fi
echo "list returned $DEPLOYMENT_NAME"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment group show] missing deployment must 404 ==="
if OUTPUT="$("$AZ" deployment group show \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-deployment-does-not-exist 2>&1)"; then
  echo "show of a missing deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentNotFound* ]]; then
  echo "missing deployment failed without DeploymentNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentNotFound"

echo -e "\n=== [deployment group list] missing resource group must 404 ==="
if OUTPUT="$("$AZ" deployment group list \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group azclips-test-group-does-not-exist 2>&1)"; then
  echo "list of a missing resource group succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *ResourceGroupNotFound* ]]; then
  echo "missing resource group failed without ResourceGroupNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: ResourceGroupNotFound"

echo -e "\n=== [deployment group show] --name is required ==="
if "$AZ" deployment group show \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
  echo "show without --name succeeded" >&2
  exit 1
fi
echo "Expected failure: show requires --name"

echo -e "\nAll deployment get smoke checks completed."
