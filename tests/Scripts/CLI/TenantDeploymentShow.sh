#!/usr/bin/env bash
# Smoke test for `az deployment tenant show` through the built CLI.
#
# Steps:
#   1. Happy path: show returns the created deployment's name and provisioning state.
#   2. Show a nonexistent deployment name is rejected (NotFound).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentShow.sh
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

NAME="tenant-dep-show-$RANDOM"
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

echo "== 1. happy path: show returns the created deployment =="
OUTPUT="$(az deployment tenant show --name "$NAME" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "show failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "\"name\": \"$NAME\"" "show output contains the deployment name"
  assert_contains "$OUTPUT" "\"provisioningState\": \"Succeeded\"" "show output contains the provisioning state"
fi

echo "== 2. show a nonexistent deployment name is rejected =="
OUTPUT="$(az deployment tenant show --name "tenant-dep-show-missing-$RANDOM" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected show to fail for a nonexistent deployment name"
else
  assert_contains "$OUTPUT" "NotFound" "nonexistent deployment error mentions NotFound"
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment show checks passed"
