#!/usr/bin/env bash
# End-to-end smoke test for the vm delete command through the built CLI.
#
# Steps:
#   1. Create the resource group (delegated to CreateResourceGroup.sh).
#   2. Create two VMs in it.
#   3. Delete one with --name/--resource-group --force-deletion, and the other with --ids.
#   4. Show each afterwards to confirm removal — those shows are EXPECTED to fail.
#   5. Best-effort cleanup of both VMs on exit.
#
# The resource group itself is left behind: azclips has no `group delete` command yet.
#
# Prereqs: build the solution first with dotnet build, then sign in. The resource
# group must not have a delete lock, or every delete will fail with ScopeLocked.
#
# Usage: ./tests/Scripts/CLI/VMDelete.sh <resource-group> [options]
#   -g, --resource-group   required (or pass it as the first positional argument).
#                          Created if it does not already exist.
#   -l, --location         defaults to eastus
#   -p, --prefix           defaults to vmdel. Keep it <= 13 chars: the VM name is used as
#                          the Windows computer name, which is capped at 15 characters.
#   -s, --subscription-id  defaults to the sample test subscription
#
# Example: ./tests/Scripts/CLI/VMDelete.sh jttest-ae --location australiaeast
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
if [[ ! -x "$AZ" ]]; then
    echo "Built CLI not found at $REPO_ROOT/artifacts/Debug/Core.Cli/az[.exe]. Run dotnet build first." >&2
    exit 1
fi

RESOURCE_GROUP=""
LOCATION=eastus
PREFIX=vmdel
SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7

readonly VM_SIZE=Standard_D2s_v3

usage() {
    echo "Usage: $0 <resource-group> [-l location] [-p prefix] [-s subscription-id]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -g|--resource-group)  RESOURCE_GROUP="${2:-}"; shift 2 ;;
        -l|--location)        LOCATION="${2:-}"; shift 2 ;;
        -p|--prefix)          PREFIX="${2:-}"; shift 2 ;;
        -s|--subscription-id) SUBSCRIPTION_ID="${2:-}"; shift 2 ;;
        -h|--help)            usage; exit 0 ;;
        -*)                   echo "Unknown option '$1'." >&2; usage; exit 1 ;;
        *)
            if [[ -n "$RESOURCE_GROUP" ]]; then
                echo "Unexpected argument '$1'. Only the resource group is positional; everything else takes a flag." >&2
                usage
                exit 1
            fi
            RESOURCE_GROUP="$1"; shift ;;
    esac
done

if [[ -z "$RESOURCE_GROUP" ]]; then
    usage
    exit 1
fi

# Azure caps the Windows computer name (derived from the VM name) at 15 characters.
BY_NAME_VM="${PREFIX}-1"
BY_ID_VM="${PREFIX}-2"
for name in "$BY_NAME_VM" "$BY_ID_VM"; do
    if [[ ${#name} -gt 15 ]]; then
        echo "VM name '$name' is ${#name} characters; Azure caps the Windows computer name at 15. Use a shorter --prefix." >&2
        exit 1
    fi
done

echo "Resource group: $RESOURCE_GROUP | location: $LOCATION | prefix: $PREFIX"

# Run unattended: disable the interactive agent so a command error (e.g. a show
# after the VM is deleted) surfaces a classic non-zero exit instead of handing off
# to the agent-mode TUI, which would block on input. No `set -e`: the shows after
# each delete are EXPECTED to fail and cleanup must still run.
"$AZ" config set --key agent.enabled --value false

create_vm() {
    local name="$1" suffix="$2"
    echo "--- Create VM '$name' ---"
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --size "$VM_SIZE" \
        --admin-username azadmin \
        --admin-password 'ArtemisCriticalX4!' \
        --network-interface-name "${PREFIX}-nic-$suffix" \
        --virtual-network-name "${PREFIX}-vnet-$suffix" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "${PREFIX}-subnet-$suffix" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --nsg "${PREFIX}-nsg-$suffix"
}

FAILED=0

# Confirms a VM is gone. `vm show` exits non-zero for a missing VM, which is the pass
# condition here; a successful show means the delete did not take effect.
assert_vm_removed() {
    local name="$1"
    if "$AZ" vm show \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" >/dev/null 2>&1
    then
        echo "FAIL: VM '$name' still exists after delete." >&2
        FAILED=1
    else
        echo "PASS: VM '$name' was deleted."
    fi
}

# Best-effort cleanup so a mid-script failure never leaves VMs behind.
cleanup() {
    echo "--- Cleanup ---"
    for name in "$BY_NAME_VM" "$BY_ID_VM"; do
        "$AZ" vm delete \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$name" >/dev/null 2>&1 || true
    done
    echo "Resource group '$RESOURCE_GROUP' was left in place; delete it manually when finished."
}

# Setup: the resource group must exist before the VMs can be created. Registered
# before the cleanup trap so a group-create failure exits without pointless deletes.
"$SCRIPT_DIR/CreateResourceGroup.sh" "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --subscription-id "$SUBSCRIPTION_ID" || exit 1

trap cleanup EXIT

create_vm "$BY_NAME_VM" 1 || exit 1
create_vm "$BY_ID_VM" 2 || exit 1

echo "--- Delete by --name/--resource-group with --force-deletion ---"
"$AZ" vm delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$BY_NAME_VM" \
    --force-deletion true

# Expected to fail: the VM no longer exists.
assert_vm_removed "$BY_NAME_VM"

echo "--- Delete by --ids ---"
"$AZ" vm delete \
    --ids "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$BY_ID_VM"

# Expected to fail: the VM no longer exists.
assert_vm_removed "$BY_ID_VM"

if [[ $FAILED -ne 0 ]]; then
    echo "VMDelete smoke test failed: at least one VM still existed after its delete." >&2
    exit 1
fi

echo "VMDelete smoke test passed."
