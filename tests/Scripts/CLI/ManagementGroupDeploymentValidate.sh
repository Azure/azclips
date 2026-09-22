#!/usr/bin/env bash
# Management group scope deployment validate - CLI
#
# Prereqs: build the solution first with dotnet build, then sign in.
# Usage: ./tests/Scripts/CLI/ManagementGroupDeploymentValidate.sh
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

"$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP"

# Negative case: malformed Bicep must fail compilation before ARM validation runs.
set +e
BICEP_ERROR=$("$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$BICEP_DIR/malformed.bicep" 2>&1)
STATUS=$?
set -e
if [[ $STATUS -eq 0 ]]; then
    echo "UNEXPECTED: validate accepted malformed Bicep"
    exit 1
fi
if ! grep -q 'Bicep compilation failed' <<<"$BICEP_ERROR"; then
    echo "UNEXPECTED: validate failed for the wrong reason: $BICEP_ERROR" >&2
    exit 1
fi
echo "OK: validate rejected malformed Bicep"

# Negative case: an invalid validation level must be rejected.
if "$AZ" deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$BICEP_DIR/management-group-nested-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$RESOURCE_GROUP" \
    --validation-level Foo; then
    echo "UNEXPECTED: validate accepted invalid --validation-level Foo"
    exit 1
else
    echo "OK: validate rejected invalid --validation-level Foo"
fi
