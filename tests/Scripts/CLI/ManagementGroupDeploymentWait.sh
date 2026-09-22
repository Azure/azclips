#!/usr/bin/env bash
# Management group scope deployment wait - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentWait.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7
RESOURCE_GROUP=azclips-mg-test-rg
MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
DEPLOYMENT_NAME=azclips-test-deployment-mg-wait

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --no-wait \
    >/dev/null

"$AZ" deployment mg wait \
    -m "$MANAGEMENT_GROUP_ID" \
    -n "$DEPLOYMENT_NAME" \
    --created \
    --interval 2 \
    --timeout 120
echo "wait created -> succeeded"

"$AZ" deployment mg delete \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --no-wait

"$AZ" deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --deleted \
    --interval 2 \
    --timeout 120
echo "wait deleted -> succeeded"

# Negative case: one wait condition is required.
if "$AZ" deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME"; then
    echo "UNEXPECTED: wait accepted a missing condition"
    exit 1
else
    echo "OK: wait rejected a missing condition"
fi
