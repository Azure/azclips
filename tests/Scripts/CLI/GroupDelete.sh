#!/usr/bin/env bash
# Live smoke test for az group delete.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in to the target subscription.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource groups
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/GroupDelete.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Prevent Git Bash/MSYS from rewriting slash-prefixed ARM resource IDs.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%Y%m%d%H%M%S)-$$-$RANDOM"
RESOURCE_GROUP="azclips-group-delete-$STAMP"
CREATED=0

cleanup_group() {
  local resource_group="$1"
  "$AZ" rest \
    --method DELETE \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$resource_group?api-version=2025-04-01" \
    >/dev/null 2>&1 || true
}

cleanup() {
  local exit_code=$?
  [[ "$CREATED" == "0" ]] || cleanup_group "$RESOURCE_GROUP"
  exit "$exit_code"
}
trap cleanup EXIT

echo "=== [group create] delete target ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" >/dev/null
CREATED=1

echo "=== [group delete] legacy aliases ==="
"$AZ" group delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP"
CREATED=0

[[ "$("$AZ" group exists --subscription-id "$SUBSCRIPTION_ID" -n "$RESOURCE_GROUP")" == "false" ]]
echo "az group delete smoke checks completed."
