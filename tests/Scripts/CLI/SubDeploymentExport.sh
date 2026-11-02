#!/usr/bin/env bash
# Subscription scope deployment export smoke test for the azclips CLI surface (az deployment sub export).
# Arranges a deployment of an empty template (no resources) and deletes its record on exit.
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
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentExport.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-export-$(date +%y%m%d%H%M%S)"
TARGET=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME")

# az.exe cannot open Git Bash /c/... paths; pwd -W yields the Windows form.
BICEP_DIR="$(cd "$REPO_ROOT/tests/Scripts/Bicep" && { pwd -W 2>/dev/null || pwd; })"

cleanup() {
  local exit_code=$?
  echo -e "\n=== cleanup $NAME ==="
  "$AZ" deployment sub delete "${TARGET[@]}" > /dev/null 2>&1 || true
  exit "$exit_code"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo PASS; }

# Keep AI error recovery manual so errors surface as non-zero exits.
"$AZ" config set --key agent.recovery.trigger --value manual

if [[ -n "$TENANT_ID" ]]; then
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo -e "\n=== [deployment sub create] arrange $NAME ==="
STATE="$("$AZ" deployment sub create "${TARGET[@]}" --location "$LOCATION" \
  --template-file "$BICEP_DIR/subscription-empty-template.json" \
  --query properties.provisioningState -o tsv)"
[[ "$STATE" == Succeeded ]] || fail "arrange ended in '$STATE'"
pass

echo -e "\n=== [deployment sub export] ==="
# The exported template is the one deployed, so it carries the subscription template schema.
OUTPUT="$("$AZ" deployment sub export "${TARGET[@]}")"
[[ "$OUTPUT" == *subscriptionDeploymentTemplate.json* ]] || fail "export is missing the template: $OUTPUT"
pass

# Negative case runs inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub export] missing deployment must 404 ==="
if OUTPUT="$("$AZ" deployment sub export --subscription-id "$SUBSCRIPTION_ID" \
      --name azclips-test-sub-does-not-exist 2>&1)"; then
  fail "export of a missing deployment succeeded"
fi
[[ "$OUTPUT" == *DeploymentNotFound* ]] || fail "missing deployment failed without DeploymentNotFound: $OUTPUT"
pass

echo -e "\nAll deployment sub export smoke checks completed."
