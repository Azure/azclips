#!/usr/bin/env bash
# Tenant-scope deployment validation smoke tests.
#
# Covers ARM JSON, Bicep, parameter-file overlays, all validation levels,
# derived names, malformed Bicep, invalid templates, and conflicting sources.
#
# Prerequisites:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login
#   3. Use an identity permitted to validate tenant-scope deployments.
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentValidate.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

LOCATION="${AZCLIPS_TENANT_DEPLOYMENT_LOCATION:-eastus}"
STAMP="$(date +%y%m%d%H%M%S)-$$"
PREFIX="${AZCLIPS_TENANT_DEPLOYMENT_NAME:-azclips-tenant-validate-$STAMP}"

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

# Validate JSON using Provider validation.
"$AZ" deployment tenant validate \
    --location "$LOCATION" \
    --name "$PREFIX-provider" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters "@$BICEP_DIR/tenant-empty.parameters.json" marker=inline-wins \
    --validation-level Provider

# Validate Bicep using Template validation.
"$AZ" deployment tenant validate \
    --location "$LOCATION" \
    --name "$PREFIX-template" \
    --template-file "$BICEP_DIR/tenant-empty.bicep" \
    --parameters "@$BICEP_DIR/tenant-empty.bicepparam" marker=bicep-inline-wins \
    --validation-level Template

# Validate with ProviderNoRbac and a derived deployment name.
"$AZ" deployment tenant validate \
    --location "$LOCATION" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters marker=provider-no-rbac \
    --validation-level ProviderNoRbac

# Verify malformed Bicep is rejected.
expect_failure \
    "malformed Bicep validation" \
    "Bicep compilation failed" \
    "$AZ" deployment tenant validate \
        --location "$LOCATION" \
        --name "$PREFIX-malformed" \
        --template-file "$BICEP_DIR/malformed.bicep"

# Verify ARM validation diagnostics are surfaced.
expect_failure \
    "invalid tenant template" \
    "InvalidTemplate|undefinedTenantParameter|validation" \
    "$AZ" deployment tenant validate \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-template" \
        --template-file "$BICEP_DIR/tenant-invalid-template.json"

# Verify a parameters URI cannot be combined with an inline overlay.
expect_failure \
    "parameters URI plus inline overlay" \
    "Inline parameters cannot be combined with a parameters URI" \
    "$AZ" deployment tenant validate \
        --location "$LOCATION" \
        --name "$PREFIX-parameter-conflict" \
        --template-file "$BICEP_DIR/tenant-empty.json" \
        --parameters https://example.com/parameters.json marker=conflict

echo "All tenant deployment validation smoke checks completed."
