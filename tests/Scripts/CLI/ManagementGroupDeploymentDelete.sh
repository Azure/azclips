# Management group scope deployment delete - CLI (az deployment mg delete)
# Equivalent of: az deployment mg delete
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)

export MSYS_NO_PATHCONV=1
win_path() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else echo "$1"; fi; }

az config set --key agent.enabled --value false || exit 1

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:-azclips-test-mg}"
NAME="azclips-mg-delete-$(date +%y%m%d%H%M%S)"

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

echo "== 1. delete by --name, then show (expected to fail) =="
az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location westeurope \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug || { fail "setup deployment creation failed"; exit 1; }
az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --debug
deleteExit=$?
assert_eq "delete exit code" "0" "$deleteExit"
if output=$(az deployment mg show --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" 2>&1); then
    fail "'$NAME' still exists after delete"
else
    echo "$output"
    grep -qiE "not found|NotFound" <<< "$output" || fail "show after delete did not report NotFound"
fi

echo "== 2. deleting a deployment name that does not exist succeeds (ARM DELETE is idempotent) =="
az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name azclips-mg-delete-does-not-exist --debug
nonexistentDeleteExit=$?
assert_eq "delete of a nonexistent deployment completed without error" "0" "$nonexistentDeleteExit"

if (( FAILURES > 0 )); then
    echo "== deployment mg delete smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg delete smoke test finished =="
