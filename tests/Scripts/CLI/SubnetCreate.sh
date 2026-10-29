#!/usr/bin/env bash
# Smoke test for subnet create/show/list/delete.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${SUBSCRIPTION_ID:?Set SUBSCRIPTION_ID}"
RESOURCE_GROUP="${RESOURCE_GROUP:?Set RESOURCE_GROUP}"

if [[ -r /proc/sys/kernel/random/uuid ]]; then
    RUN_ID="$(tr -d '-' </proc/sys/kernel/random/uuid)"
elif command -v uuidgen >/dev/null 2>&1; then
    RUN_ID="$(uuidgen | tr -d '-')"
else
    RUN_ID="$(powershell.exe -NoProfile -Command "[guid]::NewGuid().ToString('N')")"
fi
VNET_NAME="azclips-subnet-vnet-${RUN_ID:0:16}"
SUBNET_NAME="azclips-subnet-${RUN_ID:0:16}"
vnet_created=false

cleanup() {
    if [[ "$vnet_created" == true ]]; then
        "$AZ" network vnet delete --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" --name "$VNET_NAME" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

vnet_created=true
"$AZ" network vnet create --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" --name "$VNET_NAME" --location eastus \
    --address-prefixes 10.20.0.0/16

"$AZ" network vnet subnet create --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" --name "$SUBNET_NAME" \
    --address-prefixes 10.20.1.0/24 --service-endpoints Microsoft.Storage \
    --default-outbound-access false

"$AZ" network vnet subnet show --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" --name "$SUBNET_NAME"

"$AZ" network vnet subnet list --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" --vnet-name "$VNET_NAME"

"$AZ" network vnet subnet delete --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" --vnet-name "$VNET_NAME" --name "$SUBNET_NAME"
