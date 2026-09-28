#!/usr/bin/env bash
# Smoke test for vm create's public IP workflows through the built CLI.
#
# Creates three VMs in one resource group, one per public IP mode, and checks the
# detail fields vm create returns:
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set
#   2. vm create --public-ip-address <new name>   -> creates that public IP; publicIps is set
#   3. vm create --public-ip-address ""           -> no public IP; publicIps is empty
#   4. vm create --public-ip-address <missing id> -> fails with "was not found" before
#                                                    anything is created
#   5. vm show / vm delete / vm show              -> the last show is expected to fail
#
# privateIps is checked alongside publicIps to prove the output was enriched: an
# empty publicIps with an empty privateIps means the detail lookup never ran.
#
# Reusing an existing public IP is not covered: an unattached public IP can't be
# created with this CLI.
#
# Prereqs: build the solution first with dotnet build, then sign in. The subscription
# needs quota for three Standard_D2s_v3 VMs and two Standard public IPs.
#
# Cleanup: on exit the resource group this script created is deleted, which removes
# the VMs, public IPs, NICs, VNets and NSGs with it. A group supplied through
# AZCLIPS_RESOURCE_GROUP may hold other resources, so the script only deletes its
# VMs there and leaves the rest to you.
#
# Usage: ./tests/Scripts/CLI/VMCreate.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmcreate-rg-$SUFFIX}"

# Only a group this script named itself is safe to delete.
OWNS_RESOURCE_GROUP=1
[[ -n "${AZCLIPS_RESOURCE_GROUP:-}" ]] && OWNS_RESOURCE_GROUP=0

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
DEFAULT_VM="vmc-def-$SUFFIX"
NAMED_VM="vmc-nam-$SUFFIX"
NONE_VM="vmc-non-$SUFFIX"
MISSING_VM="vmc-mis-$SUFFIX"
NAMED_PUBLIC_IP="azclips-vmcreate-pip-$SUFFIX"
MISSING_PUBLIC_IP_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$SUFFIX"

FAILURES=0

fail()
{
    echo "FAIL: $1"
    FAILURES=$((FAILURES + 1))
}

# Prints the value of a string field from a JSON document; empty when the field is absent.
json_field()
{
    sed -n "s/.*\"$1\": *\"\([^\"]*\)\".*/\1/p" <<< "$2" | head -n 1
}

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.enabled --value false

cleanup()
{
    if [[ "$OWNS_RESOURCE_GROUP" == "1" ]]
    then
        echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
        "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" || true
    else
        for vm in "$DEFAULT_VM" "$NAMED_VM" "$NONE_VM"
        do
            echo "== cleanup: deleting VM $vm =="
            "$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$vm" || true
        done
        echo "== cleanup: kept resource group $RESOURCE_GROUP because AZCLIPS_RESOURCE_GROUP supplied it; remove its public IPs and network resources yourself =="
    fi
}
trap cleanup EXIT

# vm create with per-VM network resource names; extra arguments are appended.
create_vm()
{
    local name="$1"
    shift
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --vm-size Standard_D2s_v3 \
        --admin-username Artemis \
        --admin-password "$ADMIN_PASSWORD" \
        --network-interface-name "$name-nic" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --network-security-group-name "$name-nsg" \
        --output json \
        "$@"
}

# privateIps proves the detail lookup ran; then publicIps must be set ("yes") or empty ("no").
check_public_ip()
{
    local label="$1" output="$2" expect_public_ip="$3"
    local public_ips private_ips
    public_ips=$(json_field publicIps "$output")
    private_ips=$(json_field privateIps "$output")

    if [[ -z "$private_ips" ]]
    then
        fail "$label: privateIps is empty, so the output was not enriched"
    elif [[ "$expect_public_ip" == "yes" && -z "$public_ips" ]]
    then
        fail "$label: expected a public IP, got publicIps=''"
    elif [[ "$expect_public_ip" == "no" && -n "$public_ips" ]]
    then
        fail "$label: expected no public IP, got publicIps='$public_ips'"
    else
        echo "OK: $label: publicIps='$public_ips' privateIps='$private_ips'"
    fi
}

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes
else
    fail "default: vm create failed"
fi

echo "== 2. vm create --public-ip-address $NAMED_PUBLIC_IP (new name) =="
if output=$(create_vm "$NAMED_VM" --public-ip-address "$NAMED_PUBLIC_IP")
then
    check_public_ip "named" "$output" yes
else
    fail "named: vm create failed"
fi

echo "== 3. vm create --public-ip-address \"\" (no public IP) =="
if output=$(create_vm "$NONE_VM" --public-ip-address "")
then
    check_public_ip "none" "$output" no
else
    fail "none: vm create failed"
fi

echo "== 4. vm create --public-ip-address <missing resource id> (expect 'was not found') =="
if output=$(create_vm "$MISSING_VM" --public-ip-address "$MISSING_PUBLIC_IP_ID" 2>&1)
then
    echo "$output"
    fail "missing id: expected vm create to fail"
elif grep -qF "Public IP address '$MISSING_PUBLIC_IP_ID' was not found." <<< "$output"
then
    echo "OK: missing id: failed with the expected message"
else
    echo "$output"
    fail "missing id: vm create failed, but not with the expected message"
fi

echo "== 5. vm show, vm delete, vm show (the last show is expected to fail) =="
"$AZ" vm show --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" \
    || fail "vm show failed for $DEFAULT_VM"
"$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" \
    || fail "vm delete failed for $DEFAULT_VM"
if "$AZ" vm show --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" >/dev/null 2>&1
then
    fail "vm show still finds $DEFAULT_VM after vm delete"
else
    echo "OK: $DEFAULT_VM no longer exists"
fi

if (( FAILURES > 0 ))
then
    echo "== vm create smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== vm create smoke test finished =="
