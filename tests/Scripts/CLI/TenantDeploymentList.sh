#!/usr/bin/env bash
# Smoke test for `az deployment tenant list` through the built CLI.
#
# Steps:
#   1. List deployments at tenant scope and find the one just created.
#   2. --filter narrows the result set to the one matching deployment.
#
# Note: unlike resource-group/management-group scope, `deployment tenant list` has no
# required scope-id parameter, so there is no "missing required parameter" negative
# case to exercise here.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentList.sh
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

NAME="tenant-dep-list-$RANDOM"
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

echo "== 1. list deployments at tenant scope and find the one just created =="
OUTPUT="$(az deployment tenant list --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "list failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "\"name\": \"$NAME\"" "list output contains the created deployment"
fi

echo "== 2. --filter narrows the result set =="
OUTPUT="$(az deployment tenant list --filter "name eq '$NAME'" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "filtered list failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "\"name\": \"$NAME\"" "filtered list output contains the created deployment"
fi

rm -f "$TEMPLATE_FILE"

# Note: unlike resource-group/management-group scope, `deployment tenant list` has no
# required scope-id parameter, so there is no "missing required parameter" negative
# case to exercise here.

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment list checks passed"
