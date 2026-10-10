#!/usr/bin/env bash
# Smoke test for `az deployment tenant validate` through the built CLI.
#
# Steps:
#   1. Happy path: validate succeeds and returns the deployment properties.
#   2. Invalid template (allowedValues constraint violation) is rejected.
#   3. Invalid --validation-level is rejected.
#
# `az deployment tenant validate` throws on an ARM validation failure (unlike
# Test-AzTenantDeployment in PowerShell, which returns an object instead of throwing -
# see TenantDeploymentValidate.ps1 for that divergence).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentValidate.sh
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

# A template whose only parameter violates its own allowedValues constraint. ARM's
# tenant-scope validate reliably reports this as a validation failure, unlike an
# undefined-parameter-in-outputs template which was observed to be silently accepted
# when the template has no resources.
INVALID_TEMPLATE_FILE="$(mktemp --suffix=.json)"
cat > "$INVALID_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "env": {
      "type": "string",
      "defaultValue": "bogus",
      "allowedValues": ["dev", "test", "prod"]
    }
  },
  "resources": []
}
JSON

echo "== 1. happy path: validate succeeds and returns the deployment properties =="
OUTPUT="$(az deployment tenant validate --name "tenant-dep-validate-$RANDOM" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "happy path validate failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "\"provisioningState\"" "validate result contains provisioningState"
fi

echo "== 2. invalid template (allowedValues violation) is rejected =="
OUTPUT="$(az deployment tenant validate --name "tenant-dep-validate-invalid-$RANDOM" --location "$LOCATION" --template-file "$INVALID_TEMPLATE_FILE" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected validate to fail for a constraint-violating template"
else
  assert_contains "$OUTPUT" "'env'" "invalid template error names the offending parameter"
fi

echo "== 3. invalid --validation-level is rejected =="
OUTPUT="$(az deployment tenant validate --name "tenant-dep-validate-badlevel-$RANDOM" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --validation-level Foo --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected failure for an invalid --validation-level value"
else
  assert_contains "$OUTPUT" "ValidationLevel" "invalid validation-level error mentions the parameter"
fi

rm -f "$TEMPLATE_FILE" "$INVALID_TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment validate checks passed"
