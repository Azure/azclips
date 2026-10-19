# Management group scope deployment validate - CLI (az deployment mg validate)
# Equivalent of: az deployment mg validate
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)

export MSYS_NO_PATHCONV=1
win_path() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else echo "$1"; fi; }

az config set --key agent.enabled --value false || exit 1

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:-azclips-test-mg}"

TEMPLATE_FILE="$(win_path "$(mktemp -t azclips-mg-template-XXXXXX.json)")"
INVALID_TEMPLATE_FILE="$(win_path "$(mktemp -t azclips-mg-invalid-template-XXXXXX.json)")"
trap 'rm -f "$TEMPLATE_FILE" "$INVALID_TEMPLATE_FILE"' EXIT
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

echo "== 1. validate accepts --mode Complete but validates using Incremental =="
state=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestEurope \
    --template-file "$TEMPLATE_FILE" \
    --mode Complete \
    --query properties.provisioningState --output tsv --debug)
assert_eq "validate" "Succeeded" "$state"

echo "== 2. --validation-level Provider (full provider validation) =="
state=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestEurope \
    --template-file "$TEMPLATE_FILE" \
    --validation-level Provider \
    --query properties.provisioningState --output tsv --debug)
assert_eq "validation-level Provider" "Succeeded" "$state"

echo "== 3. negative: default value not in allowedValues (must be rejected) =="
cat > "$INVALID_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "mode": {
      "type": "string",
      "allowedValues": ["a", "b"],
      "defaultValue": "c"
    }
  },
  "resources": []
}
JSON
if output=$(az deployment mg validate \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestEurope \
    --template-file "$INVALID_TEMPLATE_FILE" \
    --debug 2>&1)
then
    echo "$output"
    didFail=false
else
    grep -qF "InvalidTemplate" <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "validate failed with the expected message" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg validate smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg validate smoke test finished =="
