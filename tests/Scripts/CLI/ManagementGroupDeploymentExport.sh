# Management group scope deployment export - CLI (az deployment mg export)
# Equivalent of: az deployment mg export
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
NAME="azclips-mg-export-$(date +%y%m%d%H%M%S)"

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

echo "== 1. export (expect the template we deployed: contentVersion 1.0.0.0, no resources) =="
contentVersion=$(az deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --query contentVersion --output tsv --debug)
assert_eq "export contentVersion" "1.0.0.0" "$contentVersion"
resourceCount=$(az deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --query "length(resources)" --output tsv --debug)
assert_eq "export resource count" "0" "$resourceCount"

echo "== 2. negative: export a deployment name that does not exist (must be rejected) =="
if output=$(az deployment mg export \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name azclips-mg-export-does-not-exist \
    --debug 2>&1)
then
    echo "$output"
    didFail=false
else
    grep -qiE "not found|NotFound" <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "export of a nonexistent deployment failed as expected" "true" "$didFail"

az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME"
rm -f "$TEMPLATE_FILE"

if (( FAILURES > 0 )); then
    echo "== deployment mg export smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg export smoke test finished =="
