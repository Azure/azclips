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

# JSON template plus parameters-file and inline precedence.
"$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-validate-json \
    --template-file "$BICEP_DIR/management-group-empty.json" \
    --parameters-file "$BICEP_DIR/management-group-empty.parameters.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=from-inline \
    --validation-level Provider

# Bicep and bicepparam compilation.
"$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-validate-bicep \
    --template-file "$BICEP_DIR/management-group-empty.bicep" \
    --parameters "@$BICEP_DIR/management-group-empty.bicepparam" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" \
    --validation-level Template

# Invalid ARM template must fail with the expected template error.
set +e
INVALID_OUTPUT="$("$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location "$DEPLOYMENT_LOCATION" \
    --name azclips-mg-validate-invalid \
    --template-file "$BICEP_DIR/management-group-invalid-template.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" 2>&1)"
INVALID_STATUS=$?
set -e
if [[ $INVALID_STATUS -eq 0 ]] || ! grep -Eiq "InvalidTemplate|missingParameter|validation" <<<"$INVALID_OUTPUT"; then
    printf '%s\n' "$INVALID_OUTPUT" >&2
    exit 1
fi
printf '%s\n' "$INVALID_OUTPUT"

printf 'Management-group deployment validation checks completed.\n'
