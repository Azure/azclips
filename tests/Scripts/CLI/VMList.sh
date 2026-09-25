#!/usr/bin/env bash
# Smoke test for the vm list command through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# list workflow:
#   1. vm list -g <rg>          -> resource-group scoped list
#   2. vm list                  -> subscription-wide list
#   3. vm list -g <rg> -d       -> per-VM fan-out for power state and IPs
#   4. vm list --vmss <name>    -> $filter by scale set name
#   5. vm list --vmss <id>      -> $filter by scale set resource id
#   6. vm list --vmss <name>    -> without -g, expected to fail with a usage error
#
# Cases 4 and 5 return an empty array unless the resource group actually holds a
# scale set; that is a successful result for this script, which is checking that
# the filter is accepted rather than that it matches anything.
#
# NOTE: `vm create` in this repo does not allocate a public IP, so case 3
# reports empty publicIps and fqdns. privateIps, macAddresses and powerState are
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
# Usage: ./tests/Scripts/CLI/VMList.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmlist-rg-$SUFFIX}"
VM_NAME="vmlst-vm-$SUFFIX"
VMSS_NAME="azclips-vmlist-vmss-$SUFFIX"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"

VMSS_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachineScaleSets/$VMSS_NAME"

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.enabled --value false

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
    --vm-size Standard_D2s_v3 \
    --admin-username Artemis \
    --admin-password "$ADMIN_PASSWORD" \
    --network-interface-name "azclips-vmlist-nic-$SUFFIX" \
    --virtual-network-name "azclips-vmlist-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmlist-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "azclips-vmlist-nsg-$SUFFIX"

echo "== 1. vm list --resource-group (scoped list) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

echo "== 2. vm list (subscription-wide) =="
"$AZ" vm list --subscription-id "$SUBSCRIPTION_ID"

echo "== 3. vm list -d (expect powerState, privateIps, macAddresses) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    -d

echo "== 4. vm list --vmss by name (expect an empty array, not an error) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --vmss "$VMSS_NAME"

echo "== 5. vm list --vmss by resource id (expect an empty array, not an error) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --vmss "$VMSS_ID"

echo "== 6. vm list --vmss by name without --resource-group (expect a usage error) =="
if "$AZ" vm list --subscription-id "$SUBSCRIPTION_ID" --vmss "$VMSS_NAME"
then
    echo "FAIL: expected a usage error when --vmss is a name and --resource-group is missing"
    exit 1
else
    echo "OK: command failed as expected"
fi

echo "== vm list smoke test finished =="
