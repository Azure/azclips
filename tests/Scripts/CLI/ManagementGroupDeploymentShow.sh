# Management group scope deployment show - CLI (az deployment mg show)
# Equivalent of: az deployment mg show
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
NAME="azclips-mg-show-$(date +%y%m%d%H%M%S)"

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

echo "== 1. show by --name =="
shown=$(az deployment mg show \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --name "$NAME" \
    --query name --output tsv --debug)
assert_eq "show" "$NAME" "$shown"

echo "== 2. negative: show a deployment name that does not exist must be rejected =="
if output=$(az deployment mg show --management-group-id "$MANAGEMENT_GROUP_ID" --name azclips-mg-show-does-not-exist --debug 2>&1); then
    echo "$output"
    didFail=false
else
    echo "$output"
    grep -qiE "not found|NotFound" <<< "$output" && didFail=true || didFail=false
fi
assert_eq "show of a nonexistent deployment failed as expected" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg show smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg show smoke test finished =="
