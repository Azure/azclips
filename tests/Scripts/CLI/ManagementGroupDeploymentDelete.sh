#!/usr/bin/env bash
# Creates completed management-group deployments, deletes them through the built
# CLI with both waiting modes, verifies their records are gone, and cleans up.
#
# Prereqs: build the solution, sign in, and set AZCLIPS_MANAGEMENT_GROUP_ID.
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
STAMP="$(date +%s)"
WAIT_NAME="azclips-mg-delete-$STAMP"
NO_WAIT_NAME="azclips-mg-delete-nowait-$STAMP"
TEMPLATE="$(mktemp)"

cleanup() {
    for name in "$WAIT_NAME" "$NO_WAIT_NAME"; do
        "$AZ" rest --method DELETE \
            --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$AZCLIPS_MANAGEMENT_GROUP_ID/providers/Microsoft.Resources/deployments/$name?api-version=2025-04-01" \
            >/dev/null 2>&1 || true
    done
    rm -f "$TEMPLATE"
}
trap cleanup EXIT

printf '%s\n' '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' >"$TEMPLATE"

create_deployment() {
    "$AZ" deployment mg create \
        --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
        --name "$1" \
        --location "${AZCLIPS_LOCATION:-eastus}" \
        --template-file "$TEMPLATE" \
        >/dev/null
}

assert_deleted() {
    local deployments
    deployments="$("$AZ" deployment mg list \
        --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID")"
    if grep -Fq "\"name\": \"$1\"" <<<"$deployments"; then
        echo "Deployment '$1' still exists." >&2
        exit 1
    fi
}

create_deployment "$WAIT_NAME"
OUTPUT="$("$AZ" deployment mg delete \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "$WAIT_NAME")"
[[ -z "$(printf '%s' "$OUTPUT" | tr -d '[:space:]')" ]]
assert_deleted "$WAIT_NAME"

create_deployment "$NO_WAIT_NAME"
OUTPUT="$("$AZ" deployment mg delete \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "$NO_WAIT_NAME" \
    --no-wait)"
[[ -z "$(printf '%s' "$OUTPUT" | tr -d '[:space:]')" ]]

for _ in {1..24}; do
    deployments="$("$AZ" deployment mg list \
        --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID")"
    if ! grep -Fq "\"name\": \"$NO_WAIT_NAME\"" <<<"$deployments"; then
        exit 0
    fi
    sleep 5
done

echo "Deployment '$NO_WAIT_NAME' was not deleted within 120 seconds." >&2
exit 1
