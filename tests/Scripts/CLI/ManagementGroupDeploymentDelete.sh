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
RUN_ID="$(date +%s)-$$-$RANDOM"
WAIT_NAME="azclips-test-deployment-mg-delete-$RUN_ID"
NO_WAIT_NAME="azclips-test-deployment-mg-delete-no-wait-$RUN_ID"
WAIT_NESTED_NAME="azclips-delete-wait-$RUN_ID"
NO_WAIT_NESTED_NAME="azclips-delete-no-wait-$RUN_ID"
NEGATIVE_OUTPUT="$(mktemp)"

cleanup() {
    for name in "$WAIT_NAME" "$NO_WAIT_NAME"; do
        "$AZ" rest --method DELETE \
            --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$MANAGEMENT_GROUP_ID/providers/Microsoft.Resources/deployments/$name?api-version=2025-04-01" \
            >/dev/null 2>&1 || true
    done
    for name in "$WAIT_NESTED_NAME" "$NO_WAIT_NESTED_NAME"; do
        "$AZ" rest --method DELETE \
            --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$name?api-version=2025-04-01" \
            >/dev/null 2>&1 || true
    done
    rm -f "$NEGATIVE_OUTPUT"
}
trap cleanup EXIT

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$WAIT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" nestedDeploymentName="$WAIT_NESTED_NAME" \
    >/dev/null

OUTPUT="$("$AZ" deployment mg delete \
    -m "$MANAGEMENT_GROUP_ID" \
    -n "$WAIT_NAME")"
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
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" nestedDeploymentName="$NO_WAIT_NESTED_NAME" \
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

# Negative case: omitting the required deployment name must be rejected as a usage error.
if "$AZ" deployment mg delete \
    --management-group-id "$MANAGEMENT_GROUP_ID" >"$NEGATIVE_OUTPUT" 2>&1; then
    echo "UNEXPECTED: delete accepted a missing --name"
    cat "$NEGATIVE_OUTPUT"
    exit 1
fi

if ! grep -Fq "Required parameter '--name' is missing." "$NEGATIVE_OUTPUT"; then
    echo "UNEXPECTED: delete failed for a reason other than the missing --name"
    cat "$NEGATIVE_OUTPUT"
    exit 1
fi

echo "OK: delete rejected a missing --name"
