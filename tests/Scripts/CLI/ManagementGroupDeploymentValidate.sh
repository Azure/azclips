#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
TEMPLATE="$(mktemp)"
trap 'rm -f "$TEMPLATE"' EXIT

printf '%s\n' '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' >"$TEMPLATE"

OUTPUT="$("$AZ" deployment mg validate \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --location "${AZCLIPS_LOCATION:-eastus}" \
    --template-file "$TEMPLATE")"

grep -Eqi '"provisioningState"[[:space:]]*:[[:space:]]*"Succeeded"' <<<"$OUTPUT"
