#!/usr/bin/env bash
# Management group scope deployment what-if - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentWhatIf.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7
RESOURCE_GROUP=azclips-mg-test-rg
MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
DEPLOYMENT_NAME=azclips-test-deployment-mg-what-if

STATUS=$("$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --query status \
    --output tsv)
echo "what-if status -> $STATUS"
[[ "$STATUS" == "Succeeded" ]]

# Negative case: an invalid result format must be rejected.
if "$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --result-format Foo; then
    echo "UNEXPECTED: what-if accepted invalid --result-format Foo"
    exit 1
else
    echo "OK: what-if rejected invalid --result-format Foo"
fi
