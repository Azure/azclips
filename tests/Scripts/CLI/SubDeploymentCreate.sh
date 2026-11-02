#!/usr/bin/env bash
# Subscription scope deployment create smoke test for the azclips CLI surface (az deployment sub create).
# The template creates a resource group named after the deployment; it and the deployment record are
# deleted on exit.
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
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentCreate.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-create-$(date +%y%m%d%H%M%S)"
TARGET=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME")

# Public quickstart: creates resource group rgName in rgLocation (defaults to the deployment location).
TEMPLATE_URI=https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/subscription-deployments/create-rg/azuredeploy.json

# Deleting a deployment does not delete what it created, so remove the resource group explicitly.
cleanup() {
  local exit_code=$?
  echo -e "\n=== cleanup $NAME ==="
  "$AZ" deployment sub delete "${TARGET[@]}" > /dev/null 2>&1 || true
  "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$NAME" > /dev/null 2>&1 \
    || echo "WARNING: could not delete resource group $NAME; delete it manually if it exists" >&2
  exit "$exit_code"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo PASS; }

# Keep AI error recovery manual so errors surface as non-zero exits.
"$AZ" config set --key agent.recovery.trigger --value manual

if [[ -n "$TENANT_ID" ]]; then
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo -e "\n=== [deployment sub create] ==="
STATE="$("$AZ" deployment sub create "${TARGET[@]}" --location "$LOCATION" \
  --template-uri "$TEMPLATE_URI" --parameter "rgName=$NAME" \
  --query properties.provisioningState -o tsv)"
[[ "$STATE" == Succeeded ]] || fail "create ended in '$STATE'"
pass

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub create] a template source is required ==="
if OUTPUT="$("$AZ" deployment sub create --subscription-id "$SUBSCRIPTION_ID" --name "$NAME-none" \
      --location "$LOCATION" 2>&1)"; then
  fail "create without a template succeeded"
fi
[[ "$OUTPUT" == *"Exactly one of"* ]] || fail "unexpected error: $OUTPUT"
pass

echo -e "\n=== [deployment sub create] --location is required ==="
if "$AZ" deployment sub create --subscription-id "$SUBSCRIPTION_ID" --name "$NAME-none" \
      --template-uri "$TEMPLATE_URI" > /dev/null 2>&1; then
  fail "create without --location succeeded"
fi
pass

echo -e "\nAll deployment sub create smoke checks completed."
