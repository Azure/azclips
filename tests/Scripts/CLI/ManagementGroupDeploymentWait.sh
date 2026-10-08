# Management group scope deployment wait - CLI (az deployment mg wait)
# Equivalent of: az deployment mg wait
#
# AzClips has no --no-wait flag on `deployment mg create`, so a deployment always finishes
# synchronously by the time `wait` runs against it; these cases exercise `wait` against an
# already-resolved deployment rather than a cancel-then-wait flow (see
# ManagementGroupDeploymentCancel.sh for the cancel path).
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
NAME="azclips-mg-wait-$(date +%y%m%d%H%M%S)"

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
    --location eastus \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug

echo "== 1. --exists (returns immediately since the deployment already exists) =="
az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --exists --debug
assert_eq "--exists returned 0" "0" "$?"

echo "== 2. --created (returns immediately since the deployment already succeeded) =="
az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --created --debug
assert_eq "--created returned 0" "0" "$?"

echo "== 3. --custom (returns immediately once the JMESPath expression evaluates to true) =="
az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --custom "properties.provisioningState=='Succeeded'" --debug
assert_eq "--custom returned 0" "0" "$?"

echo "== 4. negative: --custom with a condition that never becomes true (must time out) =="
if output=$(az deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --custom "properties.provisioningState=='Canceled'" \
    --interval 2 \
    --timeout 5 \
    --debug 2>&1)
then
    echo "$output"
    didTimeOut=false
else
    grep -qF "Wait operation timed-out" <<< "$output" && didTimeOut=true || didTimeOut=false
    [[ "$didTimeOut" == "false" ]] && echo "$output"
fi
assert_eq "wait with an unsatisfiable --custom condition timed out as expected" "true" "$didTimeOut"

if (( FAILURES > 0 )); then
    echo "== deployment mg wait smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg wait smoke test finished =="
