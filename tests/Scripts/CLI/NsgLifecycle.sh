#!/usr/bin/env bash
# Build and sign in first, then set SUBSCRIPTION_ID and RESOURCE_GROUP.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?Set SUBSCRIPTION_ID}"
: "${RESOURCE_GROUP:?Set RESOURCE_GROUP}"
NSG_NAME="${NSG_NAME:-azclips-nsg-smoke}"

cleanup() {
    "$AZ" network nsg delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$NSG_NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

"$AZ" network nsg create --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$NSG_NAME" --tags owner=azclips scenario=nsg-smoke
"$AZ" network nsg show --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$NSG_NAME"
"$AZ" network nsg list --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP"
