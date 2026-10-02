#!/usr/bin/env bash
# Smoke test for vm create's public IP workflows through the built CLI.
#
# Creates four VMs in one resource group, one per public IP mode, and checks the
# detail fields vm create returns:
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set
#   2. vm create --public-ip-address <new name>   -> creates that public IP; publicIps is set
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set
#   4. vm create --public-ip-address ""           -> no public IP; publicIps is empty
#   5. vm create --public-ip-address <missing id> -> fails with "was not found" before
#                                                    anything is created
#   6. vm show / vm delete / vm show              -> the last show is expected to fail
#
# privateIps is checked alongside publicIps to prove the output was enriched: an
# empty publicIps with an empty privateIps means the detail lookup never ran.
#
# Every vm create passes a marketplace image URN with version "latest", so each
# case also exercises the image version lookup.
#
# Case 3 pre-creates its public IP through az rest, then verifies vm create
# attaches that resource without replacing its tags.
#
# Prereqs: build the solution first with dotnet build, then sign in. The subscription
# needs quota for four Standard_D2s_v3 VMs and three Standard public IPs.
#
# Cleanup: on exit the resource group this script created is deleted, which removes
# the VMs, public IPs, NICs, VNets and NSGs with it, including the pre-created
# public IP. A group supplied through AZCLIPS_RESOURCE_GROUP may hold other
# resources, so the script only deletes its VMs there and leaves the public IPs and
# network resources to you.
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

# Git Bash rewrites /subscriptions/... arguments for Windows .exe targets unless disabled.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX=$(( $$ % 1000000 ))
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmcreate-rg-$SUFFIX}"

# Only a group this script named itself is safe to delete.
OWNS_RESOURCE_GROUP=1
[[ -n "${AZCLIPS_RESOURCE_GROUP:-}" ]] && OWNS_RESOURCE_GROUP=0

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
DEFAULT_VM="vmc-def-$SUFFIX"
NAMED_VM="vmc-nam-$SUFFIX"
REUSE_VM="vmc-reu-$SUFFIX"
NONE_VM="vmc-non-$SUFFIX"
MISSING_VM="vmc-mis-$SUFFIX"
NAMED_PUBLIC_IP="azclips-vmcreate-pip-$SUFFIX"
REUSE_PUBLIC_IP="azclips-vmcreate-existing-pip-$SUFFIX"
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

public_ip_url()
{
    local name="$1"
    printf 'https://management.azure.com/subscriptions/%s/resourceGroups/%s/providers/Microsoft.Network/publicIPAddresses/%s?api-version=2025-05-01' \
        "$SUBSCRIPTION_ID" "$RESOURCE_GROUP" "$name"
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
        for vm in "$DEFAULT_VM" "$NAMED_VM" "$REUSE_VM" "$NONE_VM" "$MISSING_VM"
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
        --size Standard_D2s_v3 \
        --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest \
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

wait_public_ip_succeeded()
{
    local label="$1" name="$2" state=""
    local url
    url=$(public_ip_url "$name")

    for _ in {1..24}
    do
        if state=$("$AZ" rest --method GET --uri "$url" --query json.properties.provisioningState --output tsv 2>&1)
        then
            if [[ "$state" == "Succeeded" ]]
            then
                echo "OK: $label: public IP '$name' provisioningState is Succeeded"
                return 0
            fi
            echo "== waiting for public IP $name: provisioningState='$state' =="
        else
            echo "== waiting for public IP $name: GET failed: $state =="
        fi
        sleep 5
    done

    fail "$label: public IP '$name' did not reach provisioningState Succeeded after about 2 minutes (last result: ${state:-<none>})"
    return 1
}

check_public_ip_resource()
{
    local label="$1" expected_name="$2" expected_address="$3" expected_tag="${4:-}"
    local url actual_address actual_tag
    url=$(public_ip_url "$expected_name")

    if actual_address=$("$AZ" rest --method GET --uri "$url" --query json.properties.ipAddress --output tsv 2>&1)
    then
        if [[ "$actual_address" != "$expected_address" ]]
        then
            fail "$label: public IP '$expected_name' address '$actual_address' did not match publicIps '$expected_address'"
            return
        fi
    else
        fail "$label: could not read public IP '$expected_name': $actual_address"
        return
    fi

    if [[ -n "$expected_tag" ]]
    then
        if actual_tag=$("$AZ" rest --method GET --uri "$url" --query 'json.tags."azclips-smoke"' --output tsv 2>&1)
        then
            if [[ "$actual_tag" != "$expected_tag" ]]
            then
                fail "$label: public IP '$expected_name' azclips-smoke tag '$actual_tag' did not match '$expected_tag'"
                return
            fi
        else
            fail "$label: could not read public IP '$expected_name' tag: $actual_tag"
            return
        fi
    fi

    echo "OK: $label: public IP '$expected_name' address matches publicIps='$expected_address'"
}

# privateIps proves the detail lookup ran; then publicIps must be set ("yes") or empty ("no").
check_public_ip()
{
    local label="$1" output="$2" expect_public_ip="$3" expected_name="${4:-}" expected_tag="${5:-}"
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

    if [[ "$expect_public_ip" == "yes" && -n "$public_ips" && -n "$expected_name" ]]
    then
        check_public_ip_resource "$label" "$expected_name" "$public_ips" "$expected_tag"
    fi
}

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
else
    fail "default: vm create failed"
fi

echo "== 2. vm create --public-ip-address $NAMED_PUBLIC_IP (new name) =="
if output=$(create_vm "$NAMED_VM" --public-ip-address "$NAMED_PUBLIC_IP")
then
    check_public_ip "named" "$output" yes "$NAMED_PUBLIC_IP"
else
    fail "named: vm create failed"
fi

echo "== 3. vm create --public-ip-address $REUSE_PUBLIC_IP (existing name) =="
reuse_public_ip_url=$(public_ip_url "$REUSE_PUBLIC_IP")
reuse_public_ip_body=$(printf '{"location":"%s","sku":{"name":"Standard"},"tags":{"azclips-smoke":"reuse"},"properties":{"publicIPAllocationMethod":"Static"}}' "$LOCATION")
if "$AZ" rest --method PUT --uri "$reuse_public_ip_url" --body "$reuse_public_ip_body" --output json >/dev/null
then
    if wait_public_ip_succeeded "reuse" "$REUSE_PUBLIC_IP"
    then
        if output=$(create_vm "$REUSE_VM" --public-ip-address "$REUSE_PUBLIC_IP")
        then
            check_public_ip "reuse" "$output" yes "$REUSE_PUBLIC_IP" reuse
        else
            fail "reuse: vm create failed"
        fi
    else
        echo "== reuse: skipping vm create because public IP pre-create did not complete =="
    fi
else
    fail "reuse: failed to pre-create public IP '$REUSE_PUBLIC_IP'"
fi

echo "== 4. vm create --public-ip-address \"\" (no public IP) =="
if output=$(create_vm "$NONE_VM" --public-ip-address "")
then
    check_public_ip "none" "$output" no
else
    fail "none: vm create failed"
fi

echo "== 5. vm create --public-ip-address <missing resource id> (expect 'was not found') =="
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

echo "== 6. vm show, vm delete, vm show (the last show is expected to fail) =="
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
