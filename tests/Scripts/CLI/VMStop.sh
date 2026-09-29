#!/usr/bin/env bash
# Smoke test for the vm stop / deallocate commands through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG rule), then exercises every
# stop workflow:
#   1. vm stop                      -> POST .../powerOff
#   2. vm stop --skip-shutdown      -> POST .../powerOff?skipShutdown=true
#   3. vm deallocate                -> POST .../deallocate
#   4. vm deallocate --force-deallocate
#   5. vm stop --ids <id>           -> id-addressed power off
# vm start is used between the power-off cases so each stop acts on a running VM.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Cleanup: the VM is always deleted on exit. The NIC/VNet/NSG and the resource
# group are left behind because this CLI has no delete command for them; remove
# them with the system Azure CLI:
#     az group delete --name "$RESOURCE_GROUP" --yes
# Set AZCLIPS_DELETE_GROUP=1 to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/CLI/VMStop.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:-f758ac53-3e63-4317-a956-0997793808d7}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmstop-rg-$SUFFIX}"
VM_NAME="azclips-vmstop-vm-$SUFFIX"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"

VM_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$VM_NAME"

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
    --size Standard_D2s_v3 \
    --admin-username Artemis \
    --admin-password "$ADMIN_PASSWORD" \
    --network-interface-name "azclips-vmstop-nic-$SUFFIX" \
    --virtual-network-name "azclips-vmstop-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmstop-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "azclips-vmstop-nsg-$SUFFIX" \
    --rule-names rdp \
    --rule-protocols tcp \
    --rule-directions Inbound \
    --rule-priorities 1001 \
    --rule-source-address-prefixes "*" \
    --rule-source-port-ranges "*" \
    --rule-destination-port-ranges 3389 \
    --rule-destination-address-prefixes "*" \
    --rule-accesses Allow

echo "== 1. vm stop (power off, VM stays allocated and billed) =="
"$AZ" vm stop \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

"$AZ" vm start \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 2. vm stop --skip-shutdown (non-graceful power off) =="
"$AZ" vm stop \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --skip-shutdown

"$AZ" vm start \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 3. vm deallocate (releases compute, stops compute billing) =="
"$AZ" vm deallocate \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

"$AZ" vm start \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 4. vm deallocate --force-deallocate =="
"$AZ" vm deallocate \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --force-deallocate

"$AZ" vm start \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 5. vm stop --ids (resource-id addressing) =="
"$AZ" vm stop --ids "$VM_ID"

"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== vm stop / vm deallocate smoke test finished =="
