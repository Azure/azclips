#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false >/dev/null
MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID to the signed-in subscription.}"
DEPLOYMENT_LOCATION="eastus"
TEST_RESOURCE_GROUP="azclips-mg-test-rg"

PARENT_ID="$("$AZ" rest \
    --method GET \
    --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$MANAGEMENT_GROUP_ID?api-version=2021-04-01" \
    --query "json.properties.details.parent.id" \
    --output tsv)"
if [[ -z "$PARENT_ID" ]]; then
    echo "$MANAGEMENT_GROUP_ID must identify a child management group, not the tenant root management group." >&2
    exit 1
fi

# This dedicated test group is reused and intentionally not deleted by individual scripts.
RESOURCE_GROUP_ACTION="reused"
if ! "$AZ" group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$TEST_RESOURCE_GROUP" >/dev/null 2>&1; then
    "$AZ" group create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --name "$TEST_RESOURCE_GROUP" \
        --location "$DEPLOYMENT_LOCATION" >/dev/null
    RESOURCE_GROUP_ACTION="created"
fi

BICEP_DIR="$SCRIPT_DIR/../Bicep"
printf 'Target child management group: %s\n' "$MANAGEMENT_GROUP_ID"
printf 'Test resource group: %s (%s)\n' "$TEST_RESOURCE_GROUP" "$RESOURCE_GROUP_ACTION"

# JSON and full-resource output through the normal AzClips formatter.
"$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-whatif-json \
    --template-file "$BICEP_DIR/management-group-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=whatif-json \
    --result-format FullResourcePayloads

# Bicep/bicepparam plus inline overlay and ResourceIdOnly.
"$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-whatif-bicep \
    --template-file "$BICEP_DIR/management-group-empty.bicep" \
    --parameters "@$BICEP_DIR/management-group-empty.bicepparam" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=whatif-inline \
    --result-format ResourceIdOnly

# Invalid result format must fail locally.
if "$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-whatif-invalid-format \
    --template-file "$BICEP_DIR/management-group-empty.json" \
    --result-format Invalid; then
    echo "Invalid result format unexpectedly succeeded." >&2
    exit 1
fi

printf 'Management-group deployment what-if checks completed.\n'
