#!/usr/bin/env bash
# Subscription scope deployment wait smoke test for the azclips CLI surface (az deployment sub wait).
# Arranges a deployment of an empty template (no resources), then deletes it to exercise --deleted.
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
#   AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/SubDeploymentWait.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
NAME="azclips-test-sub-wait-$(date +%y%m%d%H%M%S)"
TARGET=(--subscription-id "$SUBSCRIPTION_ID" --name "$NAME")
POLL=(--interval 5 --timeout 60)

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

# Each condition is already met, so each wait returns on the first poll.
for CONDITION in --created --updated --exists; do
  echo -e "\n=== [deployment sub wait] $CONDITION ==="
  "$AZ" deployment sub wait "${TARGET[@]}" "$CONDITION" "${POLL[@]}"
done

echo -e "\n=== [deployment sub wait] --deleted ==="
"$AZ" deployment sub delete "${TARGET[@]}"
"$AZ" deployment sub wait "${TARGET[@]}" --deleted "${POLL[@]}"

# Negative case runs inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment sub wait] a condition is required ==="
if "$AZ" deployment sub wait "${TARGET[@]}" "${POLL[@]}" > /dev/null 2>&1; then
  fail "wait without a condition succeeded"
fi

echo -e "\nAll deployment sub wait smoke checks completed."
