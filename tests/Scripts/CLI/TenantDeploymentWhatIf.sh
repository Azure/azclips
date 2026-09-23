#!/usr/bin/env bash
# Tenant-scope deployment what-if smoke tests.
#
# Covers ARM JSON, Bicep, parameter overlays, result formats, excluded change
# types, malformed Bicep, invalid result format, and live ARM template
# diagnostics.
#
# Prerequisites:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login
#   3. Use an identity permitted to run tenant-scope deployment what-if.
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentWhatIf.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="f758ac53-3e63-4317-a956-0997793808d7"
LOCATION="eastus"
TEST_RESOURCE_GROUP="azclips-tenant-test-rg"
STAMP="$(date +%y%m%d%H%M%S)-$$"
PREFIX="${AZCLIPS_TENANT_DEPLOYMENT_NAME:-azclips-tenant-whatif-$STAMP}"

expect_failure() {
    local label="$1"
    local pattern="$2"
    shift 2

    set +e
    local output
    output=$("$@" 2>&1)
    local status=$?
    set -e

    if [[ $status -eq 0 ]]; then
        echo "UNEXPECTED: $label succeeded" >&2
        return 1
    fi
    if ! grep -Eiq "$pattern" <<<"$output"; then
        echo "UNEXPECTED: $label failed for the wrong reason: $output" >&2
        return 1
    fi
    printf '%s\n' "$output"
}

"$AZ" config set --key agent.enabled --value false

# This dedicated test group is reused and intentionally not deleted by individual scripts.
if ! "$AZ" group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$TEST_RESOURCE_GROUP" >/dev/null 2>&1; then
    "$AZ" group create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --name "$TEST_RESOURCE_GROUP" \
        --location "$LOCATION"
fi

# Run what-if for JSON using the default result format.
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-json" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=json

# Run what-if for Bicep with a bicepparam file and inline override.
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-bicep" \
    --template-file "$BICEP_DIR/tenant-empty.bicep" \
    --parameters "@$BICEP_DIR/tenant-empty.bicepparam" subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=bicep-inline-wins

# Run what-if with ResourceIdOnly and excluded change types.
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-id-only" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP" marker=id-only \
    --result-format ResourceIdOnly \
    --exclude-change-types NoChange Ignore

# Verify malformed Bicep is rejected.
expect_failure \
    "malformed Bicep what-if" \
    "Bicep compilation failed" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-malformed" \
        --template-file "$BICEP_DIR/malformed.bicep"

# Verify an invalid result format is rejected.
expect_failure \
    "invalid result format" \
    "result-format|ResultFormat|allowed" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-format" \
        --template-file "$BICEP_DIR/tenant-empty.json" \
        --result-format Invalid

# Verify live ARM what-if diagnostics are surfaced.
expect_failure \
    "invalid tenant template what-if" \
    "InvalidTemplate|undefinedTenantParameter|what-if" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-template" \
        --template-file "$BICEP_DIR/tenant-invalid-template.json" \
        --parameters subscriptionId="$SUBSCRIPTION_ID" resourceGroupName="$TEST_RESOURCE_GROUP"

echo "All tenant deployment what-if smoke checks completed."
