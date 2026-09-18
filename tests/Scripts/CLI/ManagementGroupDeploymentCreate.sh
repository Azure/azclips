#!/usr/bin/env bash
# Management group scope deployment create - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentCreate.sh
set -euo pipefail

# Shared test context:
#   subscription: f758ac53-3e63-4317-a956-0997793808d7
#   resource group: azclips-mg-test-rg (region: eastus)

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7
RESOURCE_GROUP=azclips-mg-test-rg
MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
DEPLOYMENT_NAME=azclips-test-deployment-mg-create

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP"

STATE=$("$AZ" rest \
    --method GET \
    --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$MANAGEMENT_GROUP_ID/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2025-04-01" \
    --query "json.properties.provisioningState" \
    --output tsv)
echo "create provisioningState -> $STATE"

[[ "$STATE" == "Succeeded" ]]

# Negative case: malformed Bicep must fail compilation before a deployment is created.
if "$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-malformed \
    --location eastus \
    --template-file "$BICEP_DIR/malformed.bicep"; then
    echo "UNEXPECTED: create accepted malformed Bicep"
    exit 1
else
    echo "OK: create rejected malformed Bicep"
fi

# Negative case: an invalid mode must be rejected before a deployment is created.
if "$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-invalid-mode \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --mode Foo; then
    echo "UNEXPECTED: create accepted invalid --mode Foo"
    exit 1
else
    echo "OK: create rejected invalid --mode Foo"
fi
