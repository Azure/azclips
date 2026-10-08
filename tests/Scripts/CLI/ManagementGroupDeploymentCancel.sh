# Management group scope deployment cancel - CLI (az deployment mg cancel)
# Equivalent of: az deployment mg cancel
#
# AzClips has no --no-wait flag on `deployment mg create`, so a deployment always finishes
# synchronously and can never be caught mid-flight; the only case reachable here is the
# already-succeeded rejection guard.
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
NAME="azclips-mg-cancel-$(date +%y%m%d%H%M%S)"

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

echo "== 1. negative: cancel an already-succeeded deployment (must be rejected) =="
if output=$(az deployment mg cancel --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --debug 2>&1); then
    echo "$output"
    didFail=false
else
    grep -qF "No running deployments with name '$NAME' were found at management group '$MANAGEMENT_GROUP_ID'." <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "cancel of an already-succeeded deployment failed with the expected message" "true" "$didFail"

echo "== 2. negative: cancel a deployment name that does not exist (must be rejected) =="
if output=$(az deployment mg cancel --management-group-id "$MANAGEMENT_GROUP_ID" --name azclips-mg-cancel-does-not-exist --debug 2>&1); then
    echo "$output"
    didFail=false
else
    grep -qF "No running deployments with name 'azclips-mg-cancel-does-not-exist' were found at management group '$MANAGEMENT_GROUP_ID'." <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "cancel of a nonexistent deployment failed with the expected message" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg cancel smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg cancel smoke test finished =="
