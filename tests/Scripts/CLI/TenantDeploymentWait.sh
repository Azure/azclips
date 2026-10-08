#!/usr/bin/env bash
# Smoke test for `az deployment tenant wait` through the built CLI.
#
# Steps:
#   1. --exists returns immediately for an already-succeeded deployment.
#   2. --updated returns once provisioning has succeeded.
#   3. --custom evaluates a JMESPath expression against the deployment.
#   4. Waiting on a nonexistent deployment times out.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentWait.sh
set -u

az config set --key agent.enabled --value false >/dev/null 2>&1 || true

FAILURES=0
fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

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

NAME="tenant-dep-wait-$RANDOM"
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

echo "== 1. --exists returns immediately for an already-succeeded deployment =="
az deployment tenant wait --name "$NAME" --exists --interval 2 --timeout 30 --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "wait --exists failed for an existing deployment"
fi

echo "== 2. --updated returns once provisioning has succeeded =="
az deployment tenant wait --name "$NAME" --updated --interval 2 --timeout 30 --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "wait --updated failed for a succeeded deployment"
fi

echo "== 3. --custom evaluates a JMESPath expression against the deployment =="
az deployment tenant wait --name "$NAME" --custom "properties.provisioningState=='Succeeded'" --interval 2 --timeout 30 --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "wait --custom failed for a matching JMESPath expression"
fi

echo "== 4. waiting on a nonexistent deployment times out =="
OUTPUT="$(az deployment tenant wait --name "tenant-dep-wait-missing-$RANDOM" --exists --interval 2 --timeout 5 --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected wait --exists to time out for a nonexistent deployment"
else
  if [[ "$OUTPUT" != *"Timed out"* ]]; then
    fail "expected timeout error to mention 'Timed out', got '$OUTPUT'"
  fi
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment wait checks passed"
