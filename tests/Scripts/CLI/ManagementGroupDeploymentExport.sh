#!/usr/bin/env bash
# Management group scope deployment export - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentExport.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7
RESOURCE_GROUP=azclips-mg-test-rg
MANAGEMENT_GROUP_ID=$("$AZ" rest --method GET --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" --query "json.value[0].name" --output tsv)
DEPLOYMENT_NAME=azclips-test-deployment-mg-export

"$AZ" deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    >/dev/null

OUTPUT="$("$AZ" deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME")"

grep -Eqi '"contentVersion"[[:space:]]*:[[:space:]]*"1.0.0.0"' <<<"$OUTPUT"
grep -Eqi '"resources"[[:space:]]*:[[:space:]]*\[' <<<"$OUTPUT"
if grep -Eqi '"template"[[:space:]]*:' <<<"$OUTPUT"; then
    echo "Export returned the response envelope instead of the template." >&2
    exit 1
fi
echo "export template -> succeeded"

# Negative case: exporting a nonexistent deployment must return an ARM error.
if "$AZ" deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-test-deployment-mg-does-not-exist; then
    echo "UNEXPECTED: export accepted a nonexistent deployment"
    exit 1
else
    echo "OK: export rejected a nonexistent deployment"
fi
