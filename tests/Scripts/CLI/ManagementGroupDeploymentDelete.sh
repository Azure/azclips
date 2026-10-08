# Management group scope deployment delete - CLI (az deployment mg delete)
# Equivalent of: az deployment mg delete
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to

az config set --key agent.enabled --value false

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

echo "== 1. delete by --name, then show (expected to fail) =="
NAME="azclips-mg-delete-$(date +%y%m%d%H%M%S)"
az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug
az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --debug
deleteExit=$?
assert_eq "delete exit code" "0" "$deleteExit"
az deployment mg show --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" >/dev/null 2>&1 && stillExists=true || stillExists=false
assert_eq "'$NAME' no longer exists after delete" "false" "$stillExists"

echo "== 2. deleting a deployment name that does not exist succeeds (ARM DELETE is idempotent) =="
az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name azclips-mg-delete-does-not-exist --debug
nonexistentDeleteExit=$?
assert_eq "delete of a nonexistent deployment completed without error" "0" "$nonexistentDeleteExit"

if (( FAILURES > 0 )); then
    echo "== deployment mg delete smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg delete smoke test finished =="
