# Management group scope deployment create - CLI (az deployment mg create)
# Equivalent of: az deployment mg create
# Show/Delete are covered by ManagementGroupDeploymentShow.sh / Delete.sh.
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to
# Unlike the resource-group scope (fixed azclips-test-group-1), no fixed test management group
# exists across environments, so the management group ID must come from the environment.

# Run unattended: on a command ERROR az normally hands off to the interactive agent-mode TUI
# (which would block on input). Disable the agent via config so the classic error and a non-zero
# exit surface instead.
az config set --key agent.enabled --value false

# No `set -e`: the negative cases below are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"

# Management-group-scope templates have no public quickstart equivalent, so generate a minimal
# empty template inline.
TEMPLATE_FILE="$(mktemp -t azclips-mg-template-XXXXXX.json)"
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

echo "== 1. create: provisioning state =="
NAME="azclips-mg-$(date +%y%m%d%H%M%S)"
state=$(az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --query properties.provisioningState --output tsv --debug)
assert_eq "create" "Succeeded" "$state"
az deployment mg delete --management-group-id "$MANAGEMENT_GROUP_ID" --name "$NAME" --debug

echo "== 2. --mode Incremental =="
NAME="azclips-mg-mode-$(date +%y%m%d%H%M%S)"
state=$(az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --mode Incremental \
    --query properties.provisioningState --output tsv --debug)
assert_eq "mode incremental" "Succeeded" "$state"

echo "== 3. --validation-level Provider =="
NAME="azclips-mg-vlevel-$(date +%y%m%d%H%M%S)"
state=$(az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --name "$NAME" \
    --template-file "$TEMPLATE_FILE" \
    --validation-level Provider \
    --query properties.provisioningState --output tsv --debug)
assert_eq "validation-level Provider" "Succeeded" "$state"

echo "== 4. negative: invalid --mode Foo (must be rejected, no deployment created) =="
if output=$(az deployment mg create \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --name azclips-mg-mode-negative \
    --template-file "$TEMPLATE_FILE" \
    --mode Foo \
    --debug 2>&1)
then
    echo "$output"
    didFail=false
else
    echo "$output"
    grep -qF "Parameter 'Mode' value 'Foo' is not allowed. Allowed values: Incremental, Complete" <<< "$output" && didFail=true || didFail=false
fi
assert_eq "invalid --mode Foo failed with the expected message" "true" "$didFail"

echo "== 5. --template-spec-id (optional; requires AZCLIPS_TEMPLATE_SPEC_ID) =="
if [[ -n "${AZCLIPS_TEMPLATE_SPEC_ID:-}" ]]; then
    NAME="azclips-mg-templatespec-$(date +%y%m%d%H%M%S)"
    state=$(az deployment mg create \
        --management-group-id "$MANAGEMENT_GROUP_ID" \
        --location "West US 2" \
        --name "$NAME" \
        --template-spec-id "$AZCLIPS_TEMPLATE_SPEC_ID" \
        --query properties.provisioningState --output tsv --debug)
    assert_eq "template-spec-id" "Succeeded" "$state"
else
    echo "Skipping --template-spec-id case: AZCLIPS_TEMPLATE_SPEC_ID not set."
fi

if (( FAILURES > 0 )); then
    echo "== deployment mg create smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg create smoke test finished =="
