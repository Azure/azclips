# Management group scope deployment list - CLI (az deployment mg list)
# Equivalent of: az deployment mg list
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
NAME="azclips-mg-list-$(date +%y%m%d%H%M%S)"

TEMPLATE_FILE="$(mktemp -t azclips-mg-template-XXXXXX.json)"
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug

echo "== 1. list (unfiltered; expect '$NAME' among the results) =="
if az deployment mg list --management-group-id "$MANAGEMENT_GROUP_ID" --query "value[?name=='$NAME']" --output tsv --debug | grep -qF "$NAME"; then
    echo "OK: list contains '$NAME'"
else
    fail "list did not contain '$NAME'"
fi

echo "== 2. list --filter \"provisioningState eq 'Succeeded'\" (expect '$NAME' among the results) =="
if az deployment mg list \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --filter "provisioningState eq 'Succeeded'" \
    --query "value[?name=='$NAME']" --output tsv --debug | grep -qF "$NAME"; then
    echo "OK: filtered list contains '$NAME'"
else
    fail "filtered list did not contain '$NAME'"
fi

echo "== 3. negative: list without --management-group-id (required parameter must be rejected) =="
if output=$(az deployment mg list --debug 2>&1); then
    echo "$output"
    fail "list without --management-group-id was expected to fail"
elif grep -qF "Required parameter '--management-group-id' is missing." <<< "$output"; then
    echo "OK: list without --management-group-id failed with the expected message"
else
    echo "$output"
    fail "list without --management-group-id failed, but not with the expected message"
fi

if (( FAILURES > 0 )); then
    echo "== deployment mg list smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg list smoke test finished =="
