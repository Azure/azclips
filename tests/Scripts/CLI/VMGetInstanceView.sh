#!/usr/bin/env bash
# Smoke test for the vm get-instance-view command through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then reads its instance view
# after each power-state change and checks the power state it reports:
#   1. vm get-instance-view --resource-group --name -> GET ...?$expand=instanceView;
#                                                      prints the whole VM, with the
#                                                      instance view in place
#   2. vm get-instance-view -g -n                   -> same request, short forms;
#                                                      VM running
#   3. vm stop, then vm get-instance-view           -> VM stopped
#   4. vm deallocate, then
#      vm get-instance-view --ids <id>              -> an array of one VM; VM deallocated
#   5. vm start, then vm get-instance-view          -> VM running
#   6. vm get-instance-view --name, no group        -> fails before any request
#
# The checks read the power state with the query the command's help documents:
#     --query "properties.instanceView.statuses[?starts_with(code, 'PowerState/')].displayStatus"
#
# The VM gets no public IP: the script never connects to it, so RDP stays closed to
# the internet.
#
# Prereqs: build the solution first with dotnet build, then sign in with the built
# CLI, not an Azure CLI on PATH:
#     ./artifacts/Debug/Core.Cli/az login --subscription-id <subscription-id>
# The subscription needs quota for one VM of AZCLIPS_VM_SIZE in AZCLIPS_LOCATION.
#
# Cleanup: on exit the resource group this script created is deleted, which removes
# the VM and its network resources. A group supplied through AZCLIPS_RESOURCE_GROUP
# may hold other resources, so the script only deletes its VM there and leaves the
# network resources to you.
#
# Usage:
#   export AZCLIPS_SUBSCRIPTION_ID=<subscription-id>
#   export AZCLIPS_VM_ADMIN_PASSWORD='<test-password>'
#   ./tests/Scripts/CLI/VMGetInstanceView.sh
#
# Optional: AZCLIPS_LOCATION (default eastus), AZCLIPS_VM_SIZE (default
# Standard_D2s_v3), AZCLIPS_RESOURCE_GROUP (default azclips-vmiv-rg-<pid>).
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
if [[ ! -x "$AZ" ]]; then
    echo "Built CLI not found at $REPO_ROOT/artifacts/Debug/Core.Cli/az[.exe]. Run dotnet build first." >&2
    exit 1
fi

# Git Bash rewrites /subscriptions/... arguments for Windows .exe targets unless disabled.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
VM_SIZE="${AZCLIPS_VM_SIZE:-Standard_D2s_v3}"
SUFFIX=$(( $$ % 1000000 ))
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmiv-rg-$SUFFIX}"

# Only a group this script named itself is safe to delete.
OWNS_RESOURCE_GROUP=1
[[ -n "${AZCLIPS_RESOURCE_GROUP:-}" ]] && OWNS_RESOURCE_GROUP=0

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
VM_NAME="vmiv-vm-$SUFFIX"
VM_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$VM_NAME"

# The query the command's help documents for reading the power state.
POWER_STATE_QUERY="properties.instanceView.statuses[?starts_with(code, 'PowerState/')].displayStatus"

FAILURES=0

# Cleanup deletes only what this run got as far as creating.
GROUP_CREATED=0
VM_CREATE_ATTEMPTED=0

fail()
{
    echo "FAIL: $1"
    FAILURES=$((FAILURES + 1))
}

# vm get-instance-view with the given arguments, as TSV. az.exe ends lines with CRLF on Windows.
instance_view_tsv()
{
    local output
    output=$("$AZ" vm get-instance-view "$@" --output tsv) || return $?
    printf '%s' "${output//$'\r'/}"
}

# check_power_state <label> <expected display status> <query> <arguments that address the VM>
check_power_state()
{
    local label="$1" expected="$2" query="$3" actual
    shift 3

    if ! actual=$(instance_view_tsv "$@" --query "$query")
    then
        fail "$label: vm get-instance-view failed"
        return
    fi

    echo "$label: $actual"
    [[ "$actual" == "$expected" ]] || fail "$label: expected power state '$expected', got '$actual'"
}

# Run unattended: prevents automatic agent launch after a failure.
"$AZ" config set --key agent.recovery.trigger --value manual

cleanup()
{
    local exit_code=$?

    if [[ "$OWNS_RESOURCE_GROUP" == "1" ]]
    then
        if [[ "$GROUP_CREATED" == "1" ]]
        then
            echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
            "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" || true
        fi
    elif [[ "$VM_CREATE_ATTEMPTED" == "1" ]]
    then
        echo "== cleanup: deleting VM $VM_NAME =="
        "$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$VM_NAME" || true
        echo "== cleanup: kept resource group $RESOURCE_GROUP because AZCLIPS_RESOURCE_GROUP supplied it; remove its network resources yourself =="
    fi

    exit "$exit_code"
}
trap cleanup EXIT

echo "== setup: creating VM $VM_NAME ($VM_SIZE) in $RESOURCE_GROUP ($LOCATION) =="
"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" >/dev/null || { echo "group create failed; stopping." >&2; exit 1; }
GROUP_CREATED=1

VM_CREATE_ATTEMPTED=1
"$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --location "$LOCATION" \
    --size "$VM_SIZE" \
    --admin-username Artemis \
    --admin-password "$ADMIN_PASSWORD" \
    --virtual-network-name "azclips-vmiv-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmiv-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --nsg "azclips-vmiv-nsg-$SUFFIX" \
    --public-ip-address "" >/dev/null || { echo "vm create failed; stopping." >&2; exit 1; }

echo "== 1. vm get-instance-view --resource-group --name (whole VM, instance view in properties.instanceView) =="
"$AZ" vm get-instance-view \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || fail "1. vm get-instance-view failed"

echo "== 2. vm get-instance-view -g -n (short forms; expect VM running) =="
check_power_state "2. after vm create" "VM running" "$POWER_STATE_QUERY" \
    --subscription-id "$SUBSCRIPTION_ID" -g "$RESOURCE_GROUP" -n "$VM_NAME"

echo "== 3. vm stop, then vm get-instance-view (expect VM stopped) =="
"$AZ" vm stop \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || fail "3. vm stop failed"
check_power_state "3. after vm stop" "VM stopped" "$POWER_STATE_QUERY" \
    --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$VM_NAME"

echo "== 4. vm deallocate, then vm get-instance-view --ids (expect an array of one VM, VM deallocated) =="
"$AZ" vm deallocate \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || fail "4. vm deallocate failed"
if count=$(instance_view_tsv --ids "$VM_ID" --query 'length(@)')
then
    [[ "$count" == "1" ]] || fail "4. --ids: expected an array of one VM, got length '$count'"
else
    fail "4. --ids: vm get-instance-view failed"
fi
check_power_state "4. after vm deallocate" "VM deallocated" "[0].$POWER_STATE_QUERY" --ids "$VM_ID"

echo "== 5. vm start, then vm get-instance-view (expect VM running) =="
"$AZ" vm start \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" || fail "5. vm start failed"
check_power_state "5. after vm start" "VM running" "$POWER_STATE_QUERY" \
    --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$VM_NAME"

echo "== 6. vm get-instance-view --name without --resource-group (expect an error) =="
if output=$("$AZ" vm get-instance-view --subscription-id "$SUBSCRIPTION_ID" --name "$VM_NAME" 2>&1)
then
    fail "6. missing resource group: the command succeeded"
elif [[ "$output" == *"Specify the virtual machine by both name and resource group"* ]]
then
    echo "6. failed as expected"
else
    echo "${output//$'\r'/}"
    fail "6. missing resource group: unexpected error"
fi

if (( FAILURES > 0 ))
then
    echo "== vm get-instance-view smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== vm get-instance-view smoke test finished =="
