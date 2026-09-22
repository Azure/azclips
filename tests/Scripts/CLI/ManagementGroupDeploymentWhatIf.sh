#!/usr/bin/env bash
# Management group scope deployment what-if - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Required: AZCLIPS_SUBSCRIPTION_ID
# Optional: AZCLIPS_RESOURCE_GROUP, AZCLIPS_MANAGEMENT_GROUP_ID, AZCLIPS_DEPLOYMENT_NAME
# Usage: AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/ManagementGroupDeploymentWhatIf.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
TEMPLATE_FILE="$BICEP_DIR/management-group-nested-empty.json"

# Prevent Git Bash/MSYS from rewriting slash-prefixed ARM resource IDs.
export MSYS_NO_PATHCONV=1
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) TEMPLATE_FILE="$(cygpath -w "$TEMPLATE_FILE")" ;;
esac

"$AZ" config set --key agent.enabled --value false

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-mg-test-rg}"
MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:-}"
if [[ -z "$MANAGEMENT_GROUP_ID" ]]; then
    MANAGEMENT_GROUP_ID=$("$AZ" rest \
        --method GET \
        --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01" \
        --query "sort_by(json.value, &name)[0].name" \
        --output tsv)
fi
DEPLOYMENT_NAME="${AZCLIPS_DEPLOYMENT_NAME:-azclips-test-deployment-mg-what-if}"

STATUS=$("$AZ" deployment mg what-if \
    -m "$MANAGEMENT_GROUP_ID" \
    -n "$DEPLOYMENT_NAME" \
    -l eastus \
    -f "$TEMPLATE_FILE" \
    -p subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --query status \
    --output tsv)
echo "what-if status -> $STATUS"
[[ "$STATUS" == "Succeeded" ]]

# Negative case: an invalid result format must be rejected.
if "$AZ" deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$DEPLOYMENT_NAME" \
    --location eastus \
    --template-file "$TEMPLATE_FILE" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --result-format Foo; then
    echo "UNEXPECTED: what-if accepted invalid --result-format Foo"
    exit 1
else
    echo "OK: what-if rejected invalid --result-format Foo"
fi
