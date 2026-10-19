# Management group scope deployment wait - CLI (az deployment mg wait)
# Equivalent of: az deployment mg wait
#
# AzClips has no --no-wait flag on `deployment mg create`, so a deployment always finishes
# synchronously by the time `wait` runs against it; these cases exercise `wait` against an
# already-resolved deployment rather than a cancel-then-wait flow (see
# ManagementGroupDeploymentCancel.sh for the cancel path).
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)

export MSYS_NO_PATHCONV=1
win_path() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else echo "$1"; fi; }

az config set --key agent.enabled --value false || exit 1

# No `set -e`: collect assertion failures before exiting.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:-azclips-test-mg}"
NAME="azclips-mg-wait-$(date +%y%m%d%H%M%S)"

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
    --location westeurope \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --debug || { fail "setup deployment creation failed"; exit 1; }

echo "== 1. --exists (returns immediately since the deployment already exists) =="
output=$(az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --exists --interval 1 --timeout 10 --output json --debug)
status=$?
assert_eq "--exists returned 0" "0" "$status"
assert_eq "--exists returned no output" "" "$output"

echo "== 2. --created (returns immediately since the deployment already succeeded) =="
output=$(az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --created --interval 1 --timeout 10 --output json --debug)
status=$?
assert_eq "--created returned 0" "0" "$status"
assert_eq "--created returned no output" "" "$output"

echo "== 2b. --updated checks the same Succeeded state as --created =="
output=$(az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --updated --interval 1 --timeout 10 --output json --debug)
status=$?
assert_eq "--updated returned 0" "0" "$status"
assert_eq "--updated returned no output" "" "$output"

echo "== 3. --custom (returns immediately once the JMESPath expression evaluates to true) =="
output=$(az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --custom "properties.provisioningState=='Succeeded'" --interval 1 --timeout 10 --output json --debug)
status=$?
assert_eq "--custom returned 0" "0" "$status"
assert_eq "--custom returned no output" "" "$output"

echo "== 4. --custom timeout returns an empty JSON object with exit code 0 =="
output=$(az deployment mg wait \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --custom "properties.provisioningState=='Canceled'" \
    --interval 2 \
    --timeout 5 \
    --output json)
status=$?
assert_eq "wait timeout returned 0" "0" "$status"
assert_eq "wait timeout output" "{}" "$(printf '%s' "$output" | tr -d '[:space:]')"

az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME"
assert_eq "delete exit code" "0" "$?"

echo "== 5. --deleted (returns immediately once the deployment is removed) =="
output=$(az deployment mg wait --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --deleted --interval 1 --timeout 10 --output json --debug)
status=$?
assert_eq "--deleted returned 0" "0" "$status"
assert_eq "--deleted returned no output" "" "$output"

if (( FAILURES > 0 )); then
    echo "== deployment mg wait smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg wait smoke test finished =="
