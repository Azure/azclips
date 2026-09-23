#!/usr/bin/env bash
# Tenant-scope deployment what-if smoke tests.
#
# Covers ARM JSON, Bicep, parameter overlays, result formats, excluded change
# types, structured/colorless output, malformed Bicep, invalid result format,
# and live ARM template diagnostics.
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

LOCATION="${AZCLIPS_TENANT_DEPLOYMENT_LOCATION:-eastus}"
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
    echo "OK: $label rejected as expected"
}

"$AZ" config set --key agent.enabled --value false

echo "=== [what-if] JSON default result format ==="
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-json" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters marker=json

echo "=== [what-if] Bicep + bicepparam + inline override ==="
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-bicep" \
    --template-file "$BICEP_DIR/tenant-empty.bicep" \
    --parameters "@$BICEP_DIR/tenant-empty.bicepparam" marker=bicep-inline-wins

echo "=== [what-if] ResourceIdOnly + excluded types + structured/colorless output ==="
"$AZ" deployment tenant what-if \
    --location "$LOCATION" \
    --name "$PREFIX-id-only" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters marker=id-only \
    --result-format ResourceIdOnly \
    --exclude-change-types NoChange Ignore \
    --no-pretty-print \
    --no-color

echo "=== [negative] malformed Bicep ==="
expect_failure \
    "malformed Bicep what-if" \
    "Bicep compilation failed" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-malformed" \
        --template-file "$BICEP_DIR/malformed.bicep"

echo "=== [negative] invalid result format ==="
expect_failure \
    "invalid result format" \
    "result-format|ResultFormat|allowed" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-format" \
        --template-file "$BICEP_DIR/tenant-empty.json" \
        --result-format Invalid

echo "=== [negative] live ARM what-if diagnostic ==="
expect_failure \
    "invalid tenant template what-if" \
    "InvalidTemplate|undefinedTenantParameter|what-if" \
    "$AZ" deployment tenant what-if \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-template" \
        --template-file "$BICEP_DIR/tenant-invalid-template.json"

echo "All tenant deployment what-if smoke checks completed."
