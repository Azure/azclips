#!/usr/bin/env bash
# Management group scope deployment show/list - CLI
#
# Prereqs: run ManagementGroupDeploymentCreate.sh first.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentGet.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
DEPLOYMENT_NAME=azclips-test-deployment-mg-create

SHOW_NAME=$("$AZ" deployment mg show \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --query name \
    --output tsv)
echo "show deployment -> $SHOW_NAME"
[[ "$SHOW_NAME" == "$DEPLOYMENT_NAME" ]]

LIST_NAME=$("$AZ" deployment mg list \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --query "[?name=='$DEPLOYMENT_NAME'].name | [0]" \
    --output tsv)
echo "list deployment -> $LIST_NAME"
[[ "$LIST_NAME" == "$DEPLOYMENT_NAME" ]]

# Negative case: showing a nonexistent deployment must return DeploymentNotFound.
set +e
SHOW_ERROR=$("$AZ" deployment mg show \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-does-not-exist 2>&1)
STATUS=$?
set -e
if [[ $STATUS -eq 0 ]]; then
    echo "UNEXPECTED: show found a nonexistent deployment"
    exit 1
fi
if ! grep -q 'DeploymentNotFound' <<<"$SHOW_ERROR"; then
    echo "UNEXPECTED: show failed for the wrong reason: $SHOW_ERROR" >&2
    exit 1
fi
echo "OK: show rejected a nonexistent deployment"
