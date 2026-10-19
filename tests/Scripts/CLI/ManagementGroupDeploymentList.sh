# Management group scope deployment list - CLI (az deployment mg list)
# Equivalent of: az deployment mg list
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)

export MSYS_NO_PATHCONV=1
win_path() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else echo "$1"; fi; }

az config set --key agent.enabled --value false || exit 1
set -o pipefail

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:-azclips-test-mg}"
NAME="azclips-mg-list-$(date +%y%m%d%H%M%S)"

TEMPLATE_FILE="$(win_path "$(mktemp -t azclips-mg-template-XXXXXX.json)")"
cleanup() {
    local status=$?
    az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" || status=1
    rm -f "$TEMPLATE_FILE" || status=1
    exit "$status"
}
trap cleanup EXIT
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
    --location WestEurope \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug || { fail "setup deployment creation failed"; exit 1; }

echo "== 1. list (unfiltered; expect '$NAME' among the results) =="
az deployment mg list --management-group-id "$MANAGEMENT_GROUP_ID" --query "[?name=='$NAME'].name" --output tsv --debug | grep -qF "$NAME" && listContainsName=true || listContainsName=false
assert_eq "list contains '$NAME'" "true" "$listContainsName"

echo "== 2. list --filter \"provisioningState eq 'Succeeded'\" (expect '$NAME' among the results) =="
az deployment mg list \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --filter "provisioningState eq 'Succeeded'" \
    --query "[?name=='$NAME'].name" --output tsv --debug | grep -qF "$NAME" && filteredListContainsName=true || filteredListContainsName=false
assert_eq "filtered list contains '$NAME'" "true" "$filteredListContainsName"

echo "== 3. negative: list without --management-group-id (required parameter must be rejected) =="
if output=$(az deployment mg list --debug 2>&1); then
    echo "$output"
    didFail=false
else
    grep -qF "Required parameter '--management-group-id' is missing." <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "list without --management-group-id failed with the expected message" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg list smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg list smoke test finished =="
