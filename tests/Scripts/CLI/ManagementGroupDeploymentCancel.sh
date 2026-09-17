#!/usr/bin/env bash
# Cancels an existing running management-group deployment, verifies the command
# is silent, and waits for the deployment to reach Canceled.
#
# Prereqs: build the solution, sign in, and set:
#   AZCLIPS_MANAGEMENT_GROUP_ID
#   AZCLIPS_DEPLOYMENT_NAME
set -euo pipefail

: "${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID}"
: "${AZCLIPS_DEPLOYMENT_NAME:?Set AZCLIPS_DEPLOYMENT_NAME to a running deployment}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

CANCEL_OUTPUT="$("$AZ" deployment mg cancel \
    --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
    --name "$AZCLIPS_DEPLOYMENT_NAME")"

if [[ -n "$(printf '%s' "$CANCEL_OUTPUT" | tr -d '[:space:]')" ]]; then
    echo "Cancel unexpectedly produced output: $CANCEL_OUTPUT" >&2
    exit 1
fi

for _ in {1..24}; do
    OUTPUT="$("$AZ" deployment mg show \
        --management-group-id "$AZCLIPS_MANAGEMENT_GROUP_ID" \
        --name "$AZCLIPS_DEPLOYMENT_NAME")"

    if grep -Eqi '"provisioningState"[[:space:]]*:[[:space:]]*"Canceled"' <<<"$OUTPUT"; then
        exit 0
    fi
    if grep -Eqi '"provisioningState"[[:space:]]*:[[:space:]]*"(Failed|Succeeded)"' <<<"$OUTPUT"; then
        echo "Deployment reached a terminal state other than Canceled: $OUTPUT" >&2
        exit 1
    fi
    sleep 5
done

echo "Deployment did not reach Canceled within 120 seconds." >&2
exit 1
