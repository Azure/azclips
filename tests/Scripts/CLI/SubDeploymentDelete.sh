#!/usr/bin/env bash
# Subscription scope deployment delete smoke test for the azclips CLI surface (az deployment sub delete).
# Deleting removes the history record only, never the resources it created. Deleting a name that does
# not exist succeeds silently because ARM answers 204 either way. Arranges a deployment of an empty
# template, so nothing else needs cleaning up.
#
# Prereqs: build the solution (dotnet build azclips.slnx) and sign in, or set AZCLIPS_TENANT_ID.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription to deploy into
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands
#   AZCLIPS_LOCATION  - deployment region; defaults to australiaeast
#
# Usage:
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentDelete.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-delete-$(date +%y%m%d%H%M%S)"
TARGET=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME")

# az.exe cannot open Git Bash /c/... paths; pwd -W yields the Windows form.
BICEP_DIR="$(cd "$REPO_ROOT/tests/Scripts/Bicep" && { pwd -W 2>/dev/null || pwd; })"

cleanup() {
  local exit_code=$?
  "$AZ" deployment sub delete "${TARGET[@]}" > /dev/null 2>&1 || true
  exit "$exit_code"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo -e "\n=== [deployment sub create] arrange $NAME ==="
STATE="$("$AZ" deployment sub create "${TARGET[@]}" --location "$LOCATION" \
  --template-file "$BICEP_DIR/subscription-empty-template.json" \
  --query properties.provisioningState -o tsv)"
[[ "$STATE" == Succeeded ]] || fail "arrange ended in '$STATE'"

echo -e "\n=== [deployment sub delete] ==="
# Blocks until the delete LRO finishes and has no response body.
OUTPUT="$("$AZ" deployment sub delete "${TARGET[@]}")"
[[ -z "${OUTPUT//[[:space:]]/}" ]] || fail "delete printed '$OUTPUT', expected no output"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub show] record is gone ==="
if OUTPUT="$("$AZ" deployment sub show "${TARGET[@]}" 2>&1)"; then
  fail "deployment $NAME still exists after delete"
fi
[[ "$OUTPUT" == *DeploymentNotFound* ]] || fail "show failed without DeploymentNotFound: $OUTPUT"

echo -e "\n=== [deployment sub delete] already deleted: silent success ==="
"$AZ" deployment sub delete "${TARGET[@]}"

echo -e "\n=== [deployment sub delete] --name is required ==="
if "$AZ" deployment sub delete --subscription-id "$SUBSCRIPTION_ID" > /dev/null 2>&1; then
  fail "delete without --name succeeded"
fi

echo -e "\nAll deployment sub delete smoke checks completed."
