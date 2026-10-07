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
if az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --exists --debug; then
    echo "OK: --exists returned 0"
else
    fail "--exists was expected to return 0"
fi

echo "== 2. --created (returns immediately since the deployment already succeeded) =="
if az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --created --debug; then
    echo "OK: --created returned 0"
else
    fail "--created was expected to return 0"
fi

echo "== 3. --custom (returns immediately once the JMESPath expression evaluates to true) =="
if az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --custom "properties.provisioningState=='Succeeded'" --debug; then
    echo "OK: --custom returned 0"
else
    fail "--custom was expected to return 0"
fi

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
    fail "wait with an unsatisfiable --custom condition was expected to time out"
elif grep -qF "Wait operation timed-out" <<< "$output"; then
    echo "OK: wait with an unsatisfiable --custom condition timed out as expected"
else
    echo "$output"
    fail "wait with an unsatisfiable --custom condition failed, but not with the expected message"
fi

if (( FAILURES > 0 )); then
    echo "== deployment mg wait smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg wait smoke test finished =="
