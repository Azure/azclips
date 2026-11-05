#!/usr/bin/env bash
# Smoke test for `az deployment tenant what-if` through the built CLI.
#
# Steps:
#   1. Happy path (empty template): the preview reports no changes.
#   2. Nested create (a nested deployment that creates a resource group under a target
#      subscription): what-if reports a Create change.
#   3. --validation-level ProviderNoRbac previews without an RBAC permission check.
#   4. Invalid --result-format is rejected.
#
# What-if is READ-ONLY: it predicts the changes a template would make and never creates
# or modifies resources.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Optional:
#   AZCLIPS_LOCATION        - defaults to westus
#   AZCLIPS_SUBSCRIPTION_ID - nested subscription targeted by the create-preview case;
#                             defaults to the signed-in account's subscription
#
# Usage: ./tests/Scripts/CLI/TenantDeploymentWhatIf.sh
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
EMPTY_TEMPLATE_FILE="$(mktemp --suffix=.json)"
cat > "$EMPTY_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

# Nested deployment that creates a new resource group under a target subscription,
# so the what-if preview has a genuine "Create" change to report.
CREATE_TEMPLATE_FILE="$(mktemp --suffix=.json)"
SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}"
RG_NAME="tenant-whatif-rg-$RANDOM"
cat > "$CREATE_TEMPLATE_FILE" <<JSON
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.Resources/deployments",
      "apiVersion": "2021-04-01",
      "name": "nestedRGDeployment",
      "location": "$LOCATION",
      "subscriptionId": "$SUBSCRIPTION_ID",
      "properties": {
        "mode": "Incremental",
        "template": {
          "\$schema": "https://schema.management.azure.com/schemas/2019-08-01/subscriptionDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "resources": [
            {
              "type": "Microsoft.Resources/resourceGroups",
              "apiVersion": "2021-04-01",
              "name": "$RG_NAME",
              "location": "$LOCATION"
            }
          ]
        }
      }
    }
  ]
}
JSON

echo "== 1. happy path (empty template): preview with no predicted changes =="
OUTPUT="$(az deployment tenant what-if --name "tenant-dep-whatif-$RANDOM" --location "$LOCATION" --template-file "$EMPTY_TEMPLATE_FILE" --debug 2>/dev/null)"
if [[ $? -ne 0 ]]; then
  fail "happy path what-if failed"
else
  assert_contains "$OUTPUT" "Resource changes: no change." "empty template change report"
fi

echo "== 2. nested create: what-if reports a Create change =="
OUTPUT="$(az deployment tenant what-if --name "tenant-dep-whatif-create-$RANDOM" --location "$LOCATION" --template-file "$CREATE_TEMPLATE_FILE" --debug 2>&1)"
if [[ $? -ne 0 ]]; then
  fail "nested create what-if failed: $OUTPUT"
else
  assert_contains "$OUTPUT" "to create" "what-if report shows the Create change"
fi

echo "== 3. --validation-level ProviderNoRbac previews without an RBAC permission check =="
OUTPUT="$(az deployment tenant what-if --name "tenant-dep-whatif-norbac-$RANDOM" --location "$LOCATION" --template-file "$CREATE_TEMPLATE_FILE" --validation-level ProviderNoRbac --debug 2>/dev/null)"
if [[ $? -ne 0 ]]; then
  fail "validation-level ProviderNoRbac what-if failed"
else
  assert_contains "$OUTPUT" "Resource changes:" "validation-level ProviderNoRbac change report"
fi

echo "== 4. invalid --result-format is rejected =="
OUTPUT="$(az deployment tenant what-if --name "tenant-dep-whatif-badformat-$RANDOM" --location "$LOCATION" --template-file "$EMPTY_TEMPLATE_FILE" --result-format Foo --debug 2>&1)"
if [[ $? -eq 0 ]]; then
  fail "expected failure for an invalid --result-format value"
else
  assert_contains "$OUTPUT" "ResultFormat" "invalid result-format error mentions the parameter"
  assert_contains "$OUTPUT" "FullResourcePayloads, ResourceIdOnly" "invalid result-format error lists allowed values"
fi

rm -f "$EMPTY_TEMPLATE_FILE" "$CREATE_TEMPLATE_FILE"

if (( FAILURES > 0 )); then
  echo "$FAILURES assertion(s) failed"
  exit 1
fi
echo "All tenant deployment what-if checks passed"
