#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"
: "${AZCLIPS_DEPLOYMENT_NAME:?Set AZCLIPS_DEPLOYMENT_NAME}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

"$AZ" deployment mg wait \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "$AZCLIPS_DEPLOYMENT_NAME" \
    --created
