#!/usr/bin/env bash
# Resource group deployment export smoke test for the azclips CLI surface.
# Exercises `az deployment group export`. Export is READ-ONLY; the one write below uses a
# timestamped deployment name, so each run exports the record it just created rather than one left
# by an earlier run; that record is deleted on exit. The storage account it provisions keeps a stable
# name, so repeated runs reuse the same account. Storage account names are globally unique across
# Azure, so that name carries the first 8 characters of the subscription id to avoid clashing with
# other subscriptions.
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
#   AZCLIPS_RESOURCE_GROUP - existing resource group to deploy into; defaults to azclips-test-group-1
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentExport.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
# az.exe can't open Git Bash paths (/c/...); `pwd -W` gives the Windows form.
BICEP_DIR="$(cd "$REPO_ROOT/tests/Scripts/Bicep" && { pwd -W 2>/dev/null || pwd; })"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
DEPLOYMENT_NAME="azclips-test-deployment-export-$(date +%Y%m%d%H%M%S)"
STORAGE_ACCOUNT_NAME="azclipsexport$(printf '%s' "${SUBSCRIPTION_ID:0:8}" | tr '[:upper:]' '[:lower:]')"

WORK_DIR="$(mktemp -d)"
EXPORTED_FILE="$(cd "$WORK_DIR" && { pwd -W 2>/dev/null || pwd; })/exported.json"

# Always remove the temp dir and delete this run's deployment record, even if a check fails.
cleanup() {
  local exit_code=$?
  rm -rf "$WORK_DIR"
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
  --parameter "storageAccountName=$STORAGE_ACCOUNT_NAME" \
  --mode Incremental \
  > /dev/null

echo -e "\n=== [deployment group export] by name ==="
"$AZ" deployment group export \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  -o json > "$WORK_DIR/exported.json"
# The payload must be the template itself, not the ARM { "template": ... } envelope.
if ! grep -q '"contentVersion"' "$WORK_DIR/exported.json"; then
  echo "export did not return a template: $(cat "$WORK_DIR/exported.json")" >&2
  exit 1
fi
if grep -q '"template"' "$WORK_DIR/exported.json"; then
  echo "export did not unwrap the ARM envelope: $(cat "$WORK_DIR/exported.json")" >&2
  exit 1
fi
echo "export returned the unwrapped template"

echo -e "\n=== [deployment group validate] replay exported template ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME-replay" \
  --template-file "$EXPORTED_FILE" \
  --parameter "storageAccountName=$STORAGE_ACCOUNT_NAME" \
  --query properties.provisioningState -o tsv)"
if [[ "$STATE" != "Succeeded" ]]; then
  echo "validate of the exported template returned '$STATE', expected 'Succeeded'" >&2
  exit 1
fi
echo "provisioningState: $STATE"

echo -e "\n=== [deployment group export] default subscription ==="
"$AZ" deployment group export \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  > /dev/null
echo "export exited 0"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment group export] missing deployment must 404 ==="
if OUTPUT="$("$AZ" deployment group export \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-deployment-does-not-exist 2>&1)"; then
  echo "export of a missing deployment succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *DeploymentNotFound* ]]; then
  echo "missing deployment failed without DeploymentNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: DeploymentNotFound"

echo -e "\n=== [deployment group export] missing resource group must 404 ==="
if OUTPUT="$("$AZ" deployment group export \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group azclips-test-group-does-not-exist \
      --name "$DEPLOYMENT_NAME" 2>&1)"; then
  echo "export from a missing resource group succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *ResourceGroupNotFound* ]]; then
  echo "missing resource group failed without ResourceGroupNotFound: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: ResourceGroupNotFound"

echo -e "\n=== [deployment group export] --name is required ==="
if "$AZ" deployment group export \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
  echo "export without --name succeeded" >&2
  exit 1
fi
echo "Expected failure: export requires --name"

echo -e "\n=== [deployment group export] --resource-group is required ==="
if "$AZ" deployment group export \
      --subscription-id "$SUBSCRIPTION_ID" \
      --name "$DEPLOYMENT_NAME" > /dev/null 2>&1; then
  echo "export without --resource-group succeeded" >&2
  exit 1
fi
echo "Expected failure: export requires --resource-group"

echo -e "\nAll deployment export smoke checks completed."
