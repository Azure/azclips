#!/usr/bin/env bash
# Management group scope deployment cancel - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentCancel.sh
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
DEPLOYMENT_NAME="azclips-test-deployment-mg-cancel-$RUN_ID"
NESTED_NAME="azclips-cancel-$RUN_ID"

cleanup() {
    "$AZ" rest --method DELETE \
        --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$MANAGEMENT_GROUP_ID/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2025-04-01" \
        >/dev/null 2>&1 || true
    "$AZ" rest --method DELETE \
        --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$NESTED_NAME?api-version=2025-04-01" \
        >/dev/null 2>&1 || true
    "$AZ" rest --method DELETE \
        --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deploymentScripts/$NESTED_NAME?api-version=2023-08-01" \
        >/dev/null 2>&1 || true
}
trap cleanup EXIT

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-cancel.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" runId="\"$RUN_ID\"" \
    --no-wait

ACTIVE=false
for _ in {1..24}; do
    STATE=$("$AZ" deployment mg show \
        --management-group-id "$MANAGEMENT_GROUP_ID" \
        --name "$DEPLOYMENT_NAME" \
        --query properties.provisioningState \
        --output tsv)
    if [[ "$STATE" == "Accepted" || "$STATE" == "Running" ]]; then
        ACTIVE=true
        break
    fi
    if [[ "$STATE" == "Failed" || "$STATE" == "Succeeded" || "$STATE" == "Canceled" ]]; then
        echo "Deployment reached '$STATE' before cancellation could start." >&2
        exit 1
    fi
    sleep 1
done
if [[ "$ACTIVE" != true ]]; then
    echo "Deployment did not enter an active state before cancellation." >&2
    exit 1
fi

CANCEL_OUTPUT="$("$AZ" deployment mg cancel \
    -m "$MANAGEMENT_GROUP_ID" \
    -n "$DEPLOYMENT_NAME")"

if [[ -n "$(printf '%s' "$CANCEL_OUTPUT" | tr -d '[:space:]')" ]]; then
    echo "Cancel unexpectedly produced output: $CANCEL_OUTPUT" >&2
    exit 1
fi

for _ in {1..24}; do
    STATE=$("$AZ" deployment mg show \
        --management-group-id "$MANAGEMENT_GROUP_ID" \
        --name "$DEPLOYMENT_NAME" \
        --query properties.provisioningState \
        --output tsv)

    if [[ "$STATE" == "Canceled" ]]; then
        echo "cancel provisioningState -> $STATE"
        break
    fi
    if [[ "$STATE" == "Failed" || "$STATE" == "Succeeded" ]]; then
        echo "Deployment reached '$STATE' before it could be canceled." >&2
        exit 1
    fi
    sleep 5
done

[[ "$STATE" == "Canceled" ]]

# Negative case: canceling a nonexistent deployment must return an ARM error.
set +e
ERROR_OUTPUT=$("$AZ" deployment mg cancel \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-does-not-exist 2>&1)
STATUS=$?
set -e

if [[ $STATUS -eq 0 ]]; then
    echo "UNEXPECTED: cancel accepted a nonexistent deployment"
    exit 1
fi
if ! grep -q 'DeploymentNotFound' <<<"$ERROR_OUTPUT"; then
    echo "Cancel failed for an unexpected reason: $ERROR_OUTPUT" >&2
    exit 1
fi
echo "OK: cancel rejected a nonexistent deployment"
