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
STAMP="$(date +%y%m%d%H%M%S)"
NAME="azclips-mg-$STAMP"
printf 'Target child management group: %s\n' "$MANAGEMENT_GROUP_ID"
printf 'Test resource group: %s (%s)\n' "$TEST_RESOURCE_GROUP" "$RESOURCE_GROUP_ACTION"

# Create from Bicep with a bicepparam base and an inline overlay.
"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name "$NAME" \
    --template-file "$BICEP_DIR/management-group-empty.bicep" \
    --parameters "@$BICEP_DIR/management-group-empty.bicepparam" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=from-inline \
    --validation-level Provider

# Exercise list, show, export, and wait against the created deployment.
"$AZ" deployment mg list --management-group-id "$MANAGEMENT_GROUP_ID"
LIST_MATCH="$("$AZ" deployment mg list \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --query "[?name=='$NAME'] | length(@)" \
    --output tsv)"
[[ "$LIST_MATCH" == "1" ]]
"$AZ" deployment mg show \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME"
"$AZ" deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME"
"$AZ" deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --updated \
    --interval 1 \
    --timeout 120

# Confirm the template workload ran in the dedicated test resource group.
"$AZ" rest \
    --method GET \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$TEST_RESOURCE_GROUP/providers/Microsoft.Resources/deployments/azclips-management-group-nested?api-version=2022-09-01"
NESTED_STATE="$("$AZ" rest \
    --method GET \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$TEST_RESOURCE_GROUP/providers/Microsoft.Resources/deployments/azclips-management-group-nested?api-version=2022-09-01" \
    --query "json.properties.provisioningState" \
    --output tsv)"
[[ "$NESTED_STATE" == "Succeeded" ]]

# A completed empty deployment may only reject cancellation as not running.
set +e
CANCEL_OUTPUT="$("$AZ" deployment mg cancel \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" 2>&1)"
CANCEL_STATUS=$?
set -e
if [[ $CANCEL_STATUS -ne 0 ]] && ! grep -Eiq "not running|cannot be canceled" <<<"$CANCEL_OUTPUT"; then
    printf '%s\n' "$CANCEL_OUTPUT" >&2
    exit "$CANCEL_STATUS"
fi
printf '%s\n' "$CANCEL_OUTPUT"

# Delete deployment history and verify the deleted wait condition.
"$AZ" deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME"
"$AZ" deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --deleted \
    --interval 1 \
    --timeout 120

printf 'Management-group deployment lifecycle completed for %s.\n' "$NAME"
