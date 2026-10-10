#!/usr/bin/env bash
# Smoke test for `az deployment tenant create` through the built CLI.
#
# Steps:
#   1. Create with an inline empty template - happy path.
#   2. Missing required --location is rejected.
#   3. Invalid --validation-level is rejected.
#   4. --what-if prints the legacy what-if text instead of creating (name is not persisted).
#   5. -c --proceed-if-no-change skips the prompt when what-if reports no change.
#   6. Inline --parameters key=value reaches the template.
#
# Created deployments are deleted on exit.
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

PARAM_TEMPLATE_FILE="$(mktemp --suffix=.json)"
cat > "$PARAM_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": { "greeting": { "type": "string" } },
  "resources": [],
  "outputs": { "echo": { "type": "string", "value": "[parameters('greeting')]" } }
}
JSON

NAME1="tenant-dep-create-$RANDOM"
CREATED+=("$NAME1")

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
else
  assert_contains "$OUTPUT" "Resource changes: no change." "--what-if prints the legacy what-if summary"
fi
SHOWN="$(az deployment tenant show --name "$NAME4" --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  CREATED+=("$NAME4")
  fail "expected --what-if to not persist a real deployment named '$NAME4'"
fi

echo "== 5. -c --proceed-if-no-change deploys without prompting when nothing changes =="
NAME5="tenant-dep-create-confirm-$RANDOM"
CREATED+=("$NAME5")
OUTPUT="$(az deployment tenant create --name "$NAME5" --location "$LOCATION" --template-file "$TEMPLATE_FILE" -c --proceed-if-no-change --debug 2>&1 </dev/null)"
if [[ $? -ne 0 ]]; then
  fail "-c --proceed-if-no-change create failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "Resource changes: no change." "-c prints the what-if preview first"
  assert_contains "$OUTPUT" "\"provisioningState\": \"Succeeded\"" "-c --proceed-if-no-change deployment state"
fi

echo "== 6. inline --parameters key=value is passed to the template =="
NAME6="tenant-dep-create-params-$RANDOM"
CREATED+=("$NAME6")
OUTPUT="$(az deployment tenant create --name "$NAME6" --location "$LOCATION" --template-file "$PARAM_TEMPLATE_FILE" --parameters greeting=hello-tenant --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "--parameters create failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "hello-tenant" "template output echoes the inline parameter"
fi

rm -f "$TEMPLATE_FILE" "$PARAM_TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment create checks passed"
