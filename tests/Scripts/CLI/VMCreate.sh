#!/usr/bin/env bash
# Smoke test for the vm create / show / delete commands through the built CLI.
#
# Creates a resource group, creates a VM (and its NIC, VNet, subnet, and NSG rule),
# shows it, deletes it, then shows again to confirm removal (the final show is
# expected to report the VM no longer exists). The resource group is deleted on exit.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Usage: ./tests/Scripts/CLI/VMCreate.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
if [[ ! -x "$AZ" ]]; then
    echo "Built CLI not found at $REPO_ROOT/artifacts/Debug/Core.Cli/az[.exe]. Run dotnet build first." >&2
    exit 1
fi

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:-f758ac53-3e63-4317-a956-0997793808d7}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
STAMP="$(date +%Y%m%d%H%M%S)-$$-$RANDOM"
SHORT_SUFFIX="$(printf '%04x%04x' "$$" "$RANDOM" | cut -c1-8)"
PREFIX="vmcreate-$STAMP"
RESOURCE_GROUP="azclips-$PREFIX-rg"
VM_NAME="vmc$SHORT_SUFFIX"
NIC_NAME="$PREFIX-nic"
VNET_NAME="$PREFIX-vnet"
SUBNET_NAME="$PREFIX-subnet"
NSG_NAME="$PREFIX-nsg"

cleanup() {
    local exit_code=$?

    echo "--- Cleanup ---"
    "$AZ" vm delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$VM_NAME" >/dev/null 2>&1 || true

    "$AZ" group delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --name "$RESOURCE_GROUP" >/dev/null 2>&1 || true

    exit "$exit_code"
}
trap cleanup EXIT

# Run unattended: disable the interactive agent so a command error (e.g. the final
# show after the VM is deleted) surfaces a classic non-zero exit instead of handing
# off to the agent-mode TUI, which would block on input. No `set -e`: the final show
# is EXPECTED to fail once the VM is gone, and it must still run to verify the delete.
"$AZ" config set --key agent.enabled --value false

echo "--- Create resource group '$RESOURCE_GROUP' in '$LOCATION' ---"
"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" || exit 1

echo "--- Create VM '$VM_NAME' ---"
"$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --location "$LOCATION" \
    --size Standard_D2s_v3 \
    --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest \
    --admin-username Artemis \
    --admin-password 'ArtemisCriticalX4!' \
    --network-interface-name "$NIC_NAME" \
    --virtual-network-name "$VNET_NAME" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "$SUBNET_NAME" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "$NSG_NAME" \
    --rule-names rdp \
    --rule-protocols tcp \
    --rule-directions Inbound \
    --rule-priorities 1001 \
    --rule-source-address-prefixes "*" \
    --rule-source-port-ranges "*" \
    --rule-destination-port-ranges 3389 \
    --rule-destination-address-prefixes "*" \
    --rule-accesses Allow \
    --debug || exit 1

echo "--- Show VM '$VM_NAME' ---"
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || exit 1

echo "--- Delete VM '$VM_NAME' ---"
"$AZ" vm delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || exit 1

echo "--- Confirm VM '$VM_NAME' is gone ---"
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" >/dev/null 2>&1 && {
        echo "FAIL: VM '$VM_NAME' still exists after delete." >&2
        exit 1
    }

echo "PASS: VM '$VM_NAME' was deleted."
