#!/usr/bin/env bash
# Management group scope deployment delete - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentDelete.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7
RESOURCE_GROUP=azclips-mg-test-rg
MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
WAIT_NAME=azclips-test-deployment-mg-delete
NO_WAIT_NAME=azclips-test-deployment-mg-delete-no-wait

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$WAIT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    >/dev/null

OUTPUT="$("$AZ" deployment mg delete \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$WAIT_NAME")"
[[ -z "$(printf '%s' "$OUTPUT" | tr -d '[:space:]')" ]]

WAIT_RESULT=$("$AZ" deployment mg list \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --query "[?name=='$WAIT_NAME'].name | [0]" \
    --output tsv)
[[ -z "$WAIT_RESULT" ]]
echo "delete wait -> removed"

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NO_WAIT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    >/dev/null

OUTPUT="$("$AZ" deployment mg delete \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NO_WAIT_NAME" \
    --no-wait)"
[[ -z "$(printf '%s' "$OUTPUT" | tr -d '[:space:]')" ]]

for _ in {1..24}; do
    NO_WAIT_RESULT=$("$AZ" deployment mg list \
        --management-group-id "$MANAGEMENT_GROUP_ID" \
        --query "[?name=='$NO_WAIT_NAME'].name | [0]" \
        --output tsv)
    if [[ -z "$NO_WAIT_RESULT" ]]; then
        echo "delete no-wait -> removed"
        break
    fi
    sleep 5
done

[[ -z "$NO_WAIT_RESULT" ]]

# Negative case: deleting a nonexistent deployment must return an ARM error.
if "$AZ" deployment mg delete \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-does-not-exist; then
    echo "UNEXPECTED: delete accepted a nonexistent deployment"
    exit 1
else
    echo "OK: delete rejected a nonexistent deployment"
fi
