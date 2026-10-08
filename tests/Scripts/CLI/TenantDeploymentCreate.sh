#!/usr/bin/env bash
# Smoke test for `az deployment tenant create` through the built CLI.
#
# Steps:
#   1. Create with an inline empty template - happy path.
#   2. Missing required --location is rejected.
#   3. Invalid --validation-level is rejected.
#   4. --what-if runs a preview instead of creating (the previewed name is not persisted).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION - defaults to westus
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentCreate.sh
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

NAME1="tenant-dep-create-$RANDOM"

echo "== 1. happy path: create with an inline empty template =="
OUTPUT="$(az deployment tenant create --name "$NAME1" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "happy path create failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "\"name\": \"$NAME1\"" "created deployment name"
  assert_contains "$OUTPUT" "\"provisioningState\": \"Succeeded\"" "created deployment state"
fi

echo "== 2. missing required --location is rejected =="
OUTPUT="$(az deployment tenant create --name "tenant-dep-create-missing-loc-$RANDOM" --template-file "$TEMPLATE_FILE" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected failure when --location is omitted"
else
  assert_contains "$OUTPUT" "location" "missing location error mentions the parameter"
fi

echo "== 3. invalid --validation-level is rejected =="
OUTPUT="$(az deployment tenant create --name "tenant-dep-create-badlevel-$RANDOM" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --validation-level Foo --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected failure for an invalid --validation-level value"
else
  assert_contains "$OUTPUT" "ValidationLevel" "invalid validation-level error mentions the parameter"
  assert_contains "$OUTPUT" "Template, Provider, ProviderNoRbac" "invalid validation-level error lists allowed values"
fi

echo "== 4. --what-if runs a preview instead of creating =="
NAME4="tenant-dep-create-whatif-$RANDOM"
OUTPUT="$(az deployment tenant create --name "$NAME4" --location "$LOCATION" --template-file "$TEMPLATE_FILE" --what-if --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "--what-if run failed: $OUTPUT"
fi
SHOWN="$(az deployment tenant show --name "$NAME4" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected --what-if to not persist a real deployment named '$NAME4'"
fi

rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment create checks passed"
