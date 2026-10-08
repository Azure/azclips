# Management group scope deployment validate - CLI (az deployment mg validate)
# Equivalent of: az deployment mg validate
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to validate against

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"

TEMPLATE_FILE="$(mktemp -t azclips-mg-template-XXXXXX.json)"
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

echo "== 1. validate (happy path) =="
state=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$TEMPLATE_FILE" \
    --query properties.provisioningState --output tsv --debug)
assert_eq "validate" "Succeeded" "$state"

echo "== 2. --validation-level Provider (full provider validation) =="
state=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$TEMPLATE_FILE" \
    --validation-level Provider \
    --query properties.provisioningState --output tsv --debug)
assert_eq "validation-level Provider" "Succeeded" "$state"

echo "== 3. negative: template references an undefined parameter (must be rejected) =="
INVALID_TEMPLATE_FILE="$(mktemp -t azclips-mg-invalid-template-XXXXXX.json)"
cat > "$INVALID_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": [],
  "outputs": {
    "undefined": {
      "type": "string",
      "value": "[parameters('undefinedNameParam')]"
    }
  }
}
JSON
if output=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$INVALID_TEMPLATE_FILE" \
    --debug 2>&1)
then
    echo "$output"
    didFail=false
else
    grep -qF "Management group deployment validation failed" <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "validate failed with the expected message" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg validate smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg validate smoke test finished =="
