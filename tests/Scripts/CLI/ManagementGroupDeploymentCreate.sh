#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
NAME="azclips-mg-$(date +%s)"
TEMPLATE="$(mktemp)"

cleanup() {
    "$AZ" rest --method DELETE \
        --uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$AZCLIPS_MANAGEMENT_GROUP_ID/providers/Microsoft.Resources/deployments/$NAME?api-version=2025-04-01" >/dev/null 2>&1 || true
    rm -f "$TEMPLATE"
}
trap cleanup EXIT

printf '%s\n' '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' >"$TEMPLATE"

OUTPUT="$("$AZ" deployment mg create \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --location "${AZCLIPS_LOCATION:-eastus}" \
    --template-file "$TEMPLATE")"

grep -Eqi '"provisioningState"[[:space:]]*:[[:space:]]*"Succeeded"' <<<"$OUTPUT"
