#!/usr/bin/env bash
# Smoke test for the vm show command through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG rule), then exercises every
# show workflow:
#   1. vm show                      -> plain GET
#   2. vm show --include-user-data  -> GET ...?$expand=userData
#   3. vm show -u                   -> same, short form
#   4. vm show --show-details       -> GET ...?$expand=instanceView + NIC + public IP GETs
#   5. vm show -d                   -> same, short form
#   6. vm show -d -u                -> GET ...?$expand=instanceView,userData
#   7. vm show --ids <id>           -> id-addressed show, returns an array
#
# NOTE: `vm create` in this repo does not allocate a public IP, so cases 4-6
# report empty publicIps and fqdns. privateIps, macAddresses and powerState are
# still populated, which is what this script verifies.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Cleanup: the VM is always deleted on exit. The NIC/VNet/NSG and the resource
# group are left behind because this CLI has no delete command for them; remove
# them with the system Azure CLI:
#     az group delete --name "$RESOURCE_GROUP" --yes
# Set AZCLIPS_DELETE_GROUP=1 to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/CLI/VMShow.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
LOCATION="${AZCLIPS_LOCATION:-australiaeast}"
VM_SIZE="${AZCLIPS_VM_SIZE:-Standard_D2ds_v6}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmshow-rg-$SUFFIX}"
VM_NAME="vmshw-vm-$SUFFIX"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"

VM_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$VM_NAME"

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.recovery.trigger --value off

cleanup()
{
    echo "== cleanup: deleting VM $VM_NAME =="
    "$AZ" vm delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$VM_NAME" || true

    if [[ "${AZCLIPS_DELETE_GROUP:-0}" == "1" ]] && command -v az >/dev/null 2>&1
    then
        echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
        az group delete --subscription "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" --yes --no-wait || true
    else
        echo "== cleanup: remove leftover network resources with: az group delete --name $RESOURCE_GROUP --yes =="
    fi
}
trap cleanup EXIT

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

"$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --location "$LOCATION" \
    --size "$VM_SIZE" \
    --admin-username Artemis \
    --admin-password "$ADMIN_PASSWORD" \
    --virtual-network-name "azclips-vmshow-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmshow-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --nsg "azclips-vmshow-nsg-$SUFFIX"

echo "== 1. vm show (plain) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 2. vm show --include-user-data =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --include-user-data

echo "== 3. vm show -u (short form) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -u

echo "== 4. vm show --show-details (expect powerState, privateIps, macAddresses) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --show-details

echo "== 5. vm show -d (short form) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -d

echo "== 6. vm show -d -u (both expands) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -d -u

echo "== 7. vm show --ids (resource-id addressing, returns an array) =="
"$AZ" vm show --ids "$VM_ID"

echo "== vm show smoke test finished =="
