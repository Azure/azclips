#!/usr/bin/env bash
# Smoke test for `az deployment tenant delete` through the built CLI.
#
# Steps:
#   1. Happy path: delete then show fails (deployment is gone, NotFound).
#   2. Deleting a deployment name that does not exist succeeds (ARM DELETE is idempotent).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentDelete.sh
set -u

az config set --key agent.enabled --value false >/dev/null 2>&1 || true

FAILURES=0
fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

assert_contains() {
  local haystack="$1" needle="$2" what="$3"
  if [[ "$haystack" != *"$needle"* ]]; then
    fail "$what: expected output to contain '$needle', got '$haystack'"
  fi
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

NAME="tenant-dep-delete-$RANDOM"
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

echo "== 1. happy path: delete then show fails (deployment is gone) =="
az deployment tenant delete --name "$NAME" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "delete failed for '$NAME'"
fi
OUTPUT="$(az deployment tenant show --name "$NAME" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected show to fail after delete for '$NAME'"
else
  assert_contains "$OUTPUT" "NotFound" "post-delete show error mentions NotFound"
fi

echo "== 2. deleting a deployment name that does not exist succeeds (ARM DELETE is idempotent) =="
az deployment tenant delete --name "tenant-dep-delete-missing-$RANDOM" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "expected delete of a nonexistent deployment to complete without error"
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment delete checks passed"
