#!/usr/bin/env bash
# Live smoke test for az group update.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource group
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
RESOURCE_GROUP="azclips-group-update-$(date +%Y%m%d%H%M%S)-$$-$RANDOM"

cleanup() {
  "$AZ" group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    >/dev/null 2>&1 || true
}
trap cleanup EXIT

"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" >/dev/null

DIRECT_OUTPUT=$("$AZ" group update \
  --subscription-id "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  --tags Owner=team-a Environment=test)
grep -q '"Owner"' <<<"$DIRECT_OUTPUT"
grep -q '"team-a"' <<<"$DIRECT_OUTPUT"

SET_OUTPUT=$("$AZ" group update \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --set tags.Owner=team-b \
  --force-string)
grep -q '"Owner"' <<<"$SET_OUTPUT"
grep -q '"team-b"' <<<"$SET_OUTPUT"

echo "az group update smoke checks completed."
