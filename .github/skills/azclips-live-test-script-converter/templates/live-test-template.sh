#!/usr/bin/env bash
# Azclips CLI live test template.
# Custom-code integration CLI scripts belong under tests/Scripts/CLI/<ModuleName>/<CommandName>/<testFileName>.sh.
# Use a filesystem-safe Azclips command name for <CommandName>.
# Replace this VM example with verified commands, parameters, output fields, and assertions for the selected behavior.

set -euo pipefail

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

require_env() {
    local name="$1"
    [[ -n "${!name:-}" ]] || fail "Required environment variable '$name' is not set."
}

assert_json_eq() {
    local json="$1"
    local filter="$2"
    local expected="$3"
    local message="$4"
    local actual
    actual="$(jq -r "$filter // empty" <<<"$json")"
    [[ "$actual" == "$expected" ]] || fail "$message: expected '$expected', got '${actual:-<empty>}'"
}

assert_json_non_empty() {
    local json="$1"
    local filter="$2"
    local message="$3"
    local actual
    actual="$(jq -r "$filter // empty" <<<"$json")"
    [[ -n "$actual" ]] || fail "$message: '$filter' was empty"
}

command -v jq >/dev/null 2>&1 || fail "jq is required for JSON assertions."
require_env AZCLIPS_SUBSCRIPTION_ID
require_env AZCLIPS_VM_ADMIN_PASSWORD

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
[[ -x "$AZ" ]] || fail "Built Azclips CLI not found. Build the solution first."

"$AZ" config set --key agent.enabled --value false >/dev/null

SUBSCRIPTION_ID="$AZCLIPS_SUBSCRIPTION_ID"
RUN_ID="${AZCLIPS_LIVE_TEST_RUN_ID:-$(uuidgen | tr '[:upper:]' '[:lower:]' | cut -c1-8)}"
NAME_PREFIX="azclips-vm"

RESOURCE_GROUP="${NAME_PREFIX}-rg-${RUN_ID}"
LOCATION="eastus"
VM_NAME="${NAME_PREFIX}-vm-${RUN_ID}"
NETWORK_INTERFACE_NAME="${NAME_PREFIX}-nic-${RUN_ID}"
VIRTUAL_NETWORK_NAME="${NAME_PREFIX}-vnet-${RUN_ID}"
SUBNET_NAME="${NAME_PREFIX}-subnet-${RUN_ID}"
NETWORK_SECURITY_GROUP_NAME="${NAME_PREFIX}-nsg-${RUN_ID}"

create_json="$("$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --location "$LOCATION" \
    --vm-size Standard_D2s_v3 \
    --admin-username azclipsadmin \
    --admin-password "$AZCLIPS_VM_ADMIN_PASSWORD" \
    --network-interface-name "$NETWORK_INTERFACE_NAME" \
    --virtual-network-name "$VIRTUAL_NETWORK_NAME" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "$SUBNET_NAME" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "$NETWORK_SECURITY_GROUP_NAME" \
    --rule-names rdp \
    --rule-protocols tcp \
    --rule-directions Inbound \
    --rule-priorities 1001 \
    --rule-source-address-prefixes "*" \
    --rule-source-port-ranges "*" \
    --rule-destination-port-ranges 3389 \
    --rule-destination-address-prefixes "*" \
    --rule-accesses Allow)"

assert_json_eq "$create_json" '.name' "$VM_NAME" "vm create should return the created VM name"
assert_json_eq "$create_json" '.location' "$LOCATION" "vm create should return the requested location"
assert_json_non_empty "$create_json" '.id' "vm create should return a resource id"

show_json="$("$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME")"

assert_json_eq "$show_json" '.name' "$VM_NAME" "vm show should return the created VM name"
assert_json_non_empty "$show_json" '.properties.networkProfile.networkInterfaces[0].id' "vm show should expose the NIC reference"

"$AZ" vm delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

if "$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" >/tmp/azclips_vm_show_after_delete.out 2>&1; then
    cat /tmp/azclips_vm_show_after_delete.out >&2
    fail "vm show should fail after vm delete"
fi

echo "All assertions passed."