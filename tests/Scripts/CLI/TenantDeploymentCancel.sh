#!/usr/bin/env bash
# Smoke test for `az deployment tenant cancel` through the built CLI.
#
# Steps:
#   1. Cancel an already-succeeded deployment - rejected.
#   2. Cancel a nonexistent deployment - rejected.
#
# `deployment tenant cancel` forwards directly to ARM's cancel operation with no local
# precheck of provisioning state, so the exact error text is ARM's own and is not
# asserted; only that the call fails is checked. There is no reachable
# "cancel mid-flight" case here: azclips has no -NoWait/async create for tenant scope,
# so a deployment always finishes synchronously before cancel could ever run against it.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentCancel.sh
set -u

az config set --key agent.enabled --value false >/dev/null 2>&1 || true

FAILURES=0
fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

CREATED=()
cleanup() {
  for n in "${CREATED[@]}"; do
    az deployment tenant delete --name "$n" >/dev/null 2>&1 || echo "WARN: could not delete deployment '$n'"
  done
}
trap cleanup EXIT

LOCATION="${AZCLIPS_LOCATION:-westus}"
TEMPLATE_FILE="$(mktemp --suffix=.json)"
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

NAME="tenant-dep-cancel-$RANDOM"
CREATED+=("$NAME")
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

# `deployment tenant cancel` forwards directly to ARM's cancel operation with no
# local precheck of provisioning state, so the exact error text here is ARM's own
# and is not asserted; only that the call fails is checked.
echo "== 1. cancel an already-succeeded deployment is rejected =="
az deployment tenant cancel --name "$NAME" --debug >/dev/null 2>&1
if [[ $? -eq 0 ]]; then
  fail "expected cancel to fail for an already-succeeded deployment"
fi

echo "== 2. cancel a nonexistent deployment is rejected =="
az deployment tenant cancel --name "tenant-dep-cancel-missing-$RANDOM" --debug >/dev/null 2>&1
if [[ $? -eq 0 ]]; then
  fail "expected cancel to fail for a nonexistent deployment"
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment cancel checks passed"
