#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
TEMPLATE="$(mktemp)"

cleanup() {
    rm -f "$TEMPLATE"
}
trap cleanup EXIT

printf '%s\n' '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' >"$TEMPLATE"

OUTPUT="$("$AZ" deployment mg what-if \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "azclips-mg-what-if-$(date +%s)" \
    --location "${AZCLIPS_LOCATION:-eastus}" \
    --template-file "$TEMPLATE")"

grep -Eqi '"status"[[:space:]]*:[[:space:]]*"Succeeded"' <<<"$OUTPUT"
