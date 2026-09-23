#!/usr/bin/env bash
# Tenant-scope deployment create/lifecycle smoke tests.
#
# Covers local ARM JSON, local Bicep, .bicepparam and inline-parameter precedence,
# derived names, validation levels, waited/no-wait create, wait conditions, show,
# list, export, cancel, waited/no-wait delete, and local input failures.
#
# Prerequisites:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login
#   3. Use an identity permitted to create tenant-scope deployments.
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentCreate.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BICEP_DIR="$REPO_ROOT/tests/Scripts/Bicep"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

LOCATION="${AZCLIPS_TENANT_DEPLOYMENT_LOCATION:-eastus}"
STAMP="$(date +%y%m%d%H%M%S)-$$"
PREFIX="${AZCLIPS_TENANT_DEPLOYMENT_NAME:-azclips-tenant-$STAMP}"
EXPORT_FILE="${TMPDIR:-/tmp}/$PREFIX-export.json"
DERIVED_TEMPLATE="${TMPDIR:-/tmp}/$PREFIX-derived.json"
DEPLOYMENTS=()

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

cleanup() {
    local exit_code=$?
    for name in "${DEPLOYMENTS[@]}"; do
        "$AZ" deployment tenant delete --name "$name" --no-wait >/dev/null 2>&1 || true
    done
    rm -f "$EXPORT_FILE" "$DERIVED_TEMPLATE"
    exit "$exit_code"
}
trap cleanup EXIT

"$AZ" config set --key agent.enabled --value false

JSON_NAME="$PREFIX-json"
DEPLOYMENTS+=("$JSON_NAME")
echo "=== [create] local ARM JSON, waited LRO ==="
"$AZ" deployment tenant create \
    --location "$LOCATION" \
    --name "$JSON_NAME" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters "@$BICEP_DIR/tenant-empty.parameters.json" marker=inline-wins \
    --validation-level Provider

echo "=== [wait] exists, updated, and custom conditions ==="
"$AZ" deployment tenant wait --name "$JSON_NAME" --exists --interval 2 --timeout 120
"$AZ" deployment tenant wait --name "$JSON_NAME" --updated --interval 2 --timeout 120
"$AZ" deployment tenant wait \
    --name "$JSON_NAME" \
    --custom "properties.provisioningState=='Succeeded'" \
    --interval 2 \
    --timeout 120

echo "=== [show/list/export] ==="
"$AZ" deployment tenant show --name "$JSON_NAME"
"$AZ" deployment tenant list
"$AZ" deployment tenant export --name "$JSON_NAME" > "$EXPORT_FILE"
test -s "$EXPORT_FILE"

BICEP_NAME="$PREFIX-bicep"
DEPLOYMENTS+=("$BICEP_NAME")
echo "=== [create] Bicep + bicepparam + inline override ==="
"$AZ" deployment tenant create \
    --location "$LOCATION" \
    --name "$BICEP_NAME" \
    --template-file "$BICEP_DIR/tenant-empty.bicep" \
    --parameters "@$BICEP_DIR/tenant-empty.bicepparam" marker=bicep-inline-wins \
    --validation-level Template

echo "=== [create] derived name when --name is omitted ==="
cp "$BICEP_DIR/tenant-empty.json" "$DERIVED_TEMPLATE"
DERIVED_NAME="$(basename "$DERIVED_TEMPLATE" .json)"
DEPLOYMENTS+=("$DERIVED_NAME")
"$AZ" deployment tenant create \
    --location "$LOCATION" \
    --template-file "$DERIVED_TEMPLATE" \
    --parameters marker=derived-name

NOWAIT_NAME="$PREFIX-nowait"
DEPLOYMENTS+=("$NOWAIT_NAME")
echo "=== [create/cancel] no-wait then cancel race ==="
"$AZ" deployment tenant create \
    --location "$LOCATION" \
    --name "$NOWAIT_NAME" \
    --template-file "$BICEP_DIR/tenant-empty.json" \
    --parameters marker=no-wait \
    --no-wait

# The empty deployment can finish before cancel reaches ARM. Both a successful
# cancel and the expected terminal-state rejection are acceptable here.
"$AZ" deployment tenant cancel --name "$NOWAIT_NAME" || true
"$AZ" deployment tenant wait --name "$NOWAIT_NAME" --exists --interval 2 --timeout 120

echo "=== [negative] malformed Bicep ==="
expect_failure \
    "malformed Bicep create" \
    "Bicep compilation failed" \
    "$AZ" deployment tenant create \
        --location "$LOCATION" \
        --name "$PREFIX-malformed" \
        --template-file "$BICEP_DIR/malformed.bicep"

echo "=== [negative] invalid validation level ==="
expect_failure \
    "invalid validation level" \
    "validation-level|ValidationLevel|allowed" \
    "$AZ" deployment tenant create \
        --location "$LOCATION" \
        --name "$PREFIX-invalid-level" \
        --template-file "$BICEP_DIR/tenant-empty.json" \
        --validation-level Invalid

echo "=== [negative] parameters URI plus inline overlay ==="
expect_failure \
    "parameters URI plus inline overlay" \
    "Inline parameters cannot be combined with a parameters URI" \
    "$AZ" deployment tenant create \
        --location "$LOCATION" \
        --name "$PREFIX-parameter-conflict" \
        --template-file "$BICEP_DIR/tenant-empty.json" \
        --parameters https://example.com/parameters.json marker=conflict

echo "=== [delete] waited and no-wait variants ==="
"$AZ" deployment tenant delete --name "$JSON_NAME"
"$AZ" deployment tenant wait --name "$JSON_NAME" --deleted --interval 2 --timeout 120

"$AZ" deployment tenant delete --name "$BICEP_NAME" --no-wait
"$AZ" deployment tenant wait --name "$BICEP_NAME" --deleted --interval 2 --timeout 120

"$AZ" deployment tenant delete --name "$DERIVED_NAME"
"$AZ" deployment tenant wait --name "$DERIVED_NAME" --deleted --interval 2 --timeout 120

"$AZ" deployment tenant delete --name "$NOWAIT_NAME" --no-wait
"$AZ" deployment tenant wait --name "$NOWAIT_NAME" --deleted --interval 2 --timeout 120

DEPLOYMENTS=()
rm -f "$EXPORT_FILE" "$DERIVED_TEMPLATE"
trap - EXIT
echo "All tenant deployment create/lifecycle smoke checks completed."
