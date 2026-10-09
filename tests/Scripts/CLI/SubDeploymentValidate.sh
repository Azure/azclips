#!/usr/bin/env bash
# Subscription scope deployment validate smoke test for the azclips CLI surface (az deployment sub validate).
# Validation is read-only: it creates no deployment record and no resources, so there is no cleanup.
#
# Prereqs: build the solution (dotnet build azclips.slnx) and sign in, or set AZCLIPS_TENANT_ID.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription to validate against
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands
#   AZCLIPS_LOCATION  - deployment region; defaults to australiaeast
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentValidate.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-validate-$(date +%y%m%d%H%M%S)"

# Public quickstart: creates resource group rgName in rgLocation (defaults to the deployment location).
TEMPLATE_URI=https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/subscription-deployments/create-rg/azuredeploy.json
DEPLOY=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME" --location "$LOCATION" --template-uri "$TEMPLATE_URI")

fail() { echo "FAIL: $*" >&2; exit 1; }

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo -e "\n=== [deployment sub validate] ==="
STATE="$("$AZ" deployment sub validate "${DEPLOY[@]}" --parameter "rgName=$NAME" \
  --query properties.provisioningState -o tsv)"
[[ "$STATE" == Succeeded ]] || fail "validate returned '$STATE'"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub validate] missing required template parameter ==="
if OUTPUT="$("$AZ" deployment sub validate "${DEPLOY[@]}" 2>&1)"; then
  fail "validate without rgName succeeded"
fi
[[ "$OUTPUT" == *rgName* ]] || fail "error does not mention rgName: $OUTPUT"

echo -e "\n=== [deployment sub validate] --location is required ==="
if "$AZ" deployment sub validate --subscription-id "$SUBSCRIPTION_ID" --name "$NAME" \
      --template-uri "$TEMPLATE_URI" --parameter "rgName=$NAME" > /dev/null 2>&1; then
  fail "validate without --location succeeded"
fi

echo -e "\nAll deployment sub validate smoke checks completed."
