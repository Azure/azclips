#!/usr/bin/env bash
# Smoke test for `az deployment tenant export` through the built CLI.
#
# Steps:
#   1. Happy path: export returns the deployment's template (contentVersion is present).
#   2. Export of a nonexistent deployment name is rejected (NotFound).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentExport.sh
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

NAME="tenant-dep-export-$RANDOM"
az deployment tenant create --name "$NAME" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  fail "setup: could not create deployment '$NAME'"
fi

echo "== 1. happy path: export returns the deployment's template =="
OUTPUT="$(az deployment tenant export --name "$NAME" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "export failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "contentVersion" "exported template contains contentVersion"
fi

echo "== 2. export of a nonexistent deployment name is rejected =="
OUTPUT="$(az deployment tenant export --name "tenant-dep-export-missing-$RANDOM" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected export to fail for a nonexistent deployment name"
else
  assert_contains "$OUTPUT" "NotFound" "nonexistent deployment error mentions NotFound"
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment export checks passed"
