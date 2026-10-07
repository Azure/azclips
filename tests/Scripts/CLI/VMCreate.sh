#!/usr/bin/env bash
# Smoke test for vm create's public IP and network security group workflows through
# the built CLI.
#
# Creates four VMs in one resource group, one per public IP mode, and checks the
# detail fields vm create returns:
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set;
#                                                    creates <vm>NSG with only the rdp rule
#   2. vm create --public-ip-address <new name>   -> creates that public IP; publicIps is set;
#                --nsg <existing resource id>        attaches the pre-created NSG untouched
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set;
#                --nsg <existing name>               attaches the pre-created NSG untouched
#   4. vm create --ssh-key-values <file>          -> Linux VM with no public IP; publicIps is
#                --public-ip-address ""              empty; creates <vm>NSG with only the
#                                                    default-allow-ssh rule
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
# Case 4 deploys an Ubuntu image with a temporary key pair from ssh-keygen, deleted on exit.
#
# Before case 1 the script pre-creates an NSG through az rest, with its own tag and
# one rule, smoke-allow-https. The NSG checks read the VM's NIC and the NSG back
# through az rest: the NIC must reference the expected NSG, and the NSG must hold
# exactly one rule: rdp for case 1's default <vm>NSG, default-allow-ssh for case 4's,
# and smoke-allow-https, with the tag intact, for the NSG that cases 2 and 3 attach.
#
# Prereqs: build the solution first with dotnet build, then sign in. ssh-keygen must
# be on PATH. The subscription needs quota for four Standard_D2s_v3 VMs and three
# Standard public IPs.
#
# Cleanup: on exit the resource group this script created is deleted, which removes
# the VMs, public IPs, NICs, VNets and NSGs with it, including the pre-created
# public IP and NSG. A group supplied through AZCLIPS_RESOURCE_GROUP may hold other
# resources, so the script only deletes its VMs there and leaves the public IPs and
# network resources, including the pre-created NSG, to you.
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
EXISTING_NSG="azclips-vmcreate-existing-nsg-$SUFFIX"
EXISTING_NSG_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkSecurityGroups/$EXISTING_NSG"
DEFAULT_NSG_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkSecurityGroups/${DEFAULT_VM}NSG"
LINUX_NSG_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkSecurityGroups/${NONE_VM}NSG"

# A rule as name/protocol/source address/source port/destination address/destination port/access/direction/priority, so whole rules compare as one string.
NSG_RULE_QUERY="json.properties.securityRules[0] | join('/', [name, properties.protocol, properties.sourceAddressPrefix, properties.sourcePortRange, properties.destinationAddressPrefix, properties.destinationPortRange, properties.access, properties.direction, to_string(properties.priority)])"
RDP_NSG_RULE="rdp/Tcp/*/*/*/3389/Allow/Inbound/1000"
SSH_NSG_RULE="default-allow-ssh/Tcp/*/*/*/22/Allow/Inbound/1000"
EXISTING_NSG_RULE="smoke-allow-https/Tcp/*/*/*/443/Allow/Inbound/1100"
KEY_FILE="${TMPDIR:-/tmp}/azclips-vmcreate-key-$SUFFIX"

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

lowercase()
{
    tr '[:upper:]' '[:lower:]' <<< "$1"
}

# ARM URL of a network resource in this script's resource group, e.g. network_url publicIPAddresses <name>.
network_url()
{
    local type="$1" name="$2"
    printf 'https://management.azure.com/subscriptions/%s/resourceGroups/%s/providers/Microsoft.Network/%s/%s?api-version=2025-05-01' \
        "$SUBSCRIPTION_ID" "$RESOURCE_GROUP" "$type" "$name"
}

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.enabled --value false

cleanup()
{
    rm -f "$KEY_FILE" "$KEY_FILE.pub"

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

# vm create with per-VM network resource names; extra arguments are appended. Unless the
# extra arguments pass --nsg, the VM gets its default <vm>NSG.
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
        --output json \
        "$@"
}

# Linux vm create variant that exercises --ssh-key-values with a public key file.
create_ssh_vm()
{
    local name="$1"
    shift
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --size Standard_D2s_v3 \
        --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest \
        --admin-username azadmin \
        --ssh-key-values "$KEY_FILE.pub" \
        --network-interface-name "$name-nic" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --output json \
        "$@"
}

# Polls url until the resource's provisioningState is Succeeded; what names it in messages.
wait_succeeded()
{
    local label="$1" what="$2" url="$3" state=""

    for _ in {1..24}
    do
        if state=$("$AZ" rest --method GET --uri "$url" --query json.properties.provisioningState --output tsv 2>&1)
        then
            if [[ "$state" == "Succeeded" ]]
            then
                echo "OK: $label: $what provisioningState is Succeeded"
                return 0
            fi
            echo "== waiting for $what: provisioningState='$state' =="
        else
            echo "== waiting for $what: GET failed: $state =="
        fi
        sleep 5
    done

    fail "$label: $what did not reach provisioningState Succeeded after about 2 minutes (last result: ${state:-<none>})"
    return 1
}

check_public_ip_resource()
{
    local label="$1" expected_name="$2" expected_address="$3" expected_tag="${4:-}"
    local url actual_address actual_tag
    url=$(network_url publicIPAddresses "$expected_name")

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

# The NIC <vm>-nic must reference the NSG expected_id, compared case-insensitively because ARM
# can return the resource group in another case. That NSG must hold exactly one rule, equal to
# expected_rule, and carry the azclips-smoke tag expected_tag when one is given.
check_nsg()
{
    local label="$1" vm="$2" expected_id="$3" expected_rule="$4" expected_tag="${5:-}"
    local nsg_name="${expected_id##*/}"
    local nsg_url actual_id rule_count actual_rule actual_tag
    nsg_url=$(network_url networkSecurityGroups "$nsg_name")

    if ! actual_id=$("$AZ" rest --method GET --uri "$(network_url networkInterfaces "$vm-nic")" --query json.properties.networkSecurityGroup.id --output tsv 2>&1)
    then
        fail "$label: could not read NIC '$vm-nic': $actual_id"
        return
    fi
    if [[ "$(lowercase "$actual_id")" != "$(lowercase "$expected_id")" ]]
    then
        fail "$label: NIC '$vm-nic' references NSG '$actual_id', expected '$expected_id'"
        return
    fi

    if ! rule_count=$("$AZ" rest --method GET --uri "$nsg_url" --query 'length(json.properties.securityRules)' --output tsv 2>&1)
    then
        fail "$label: could not read NSG '$nsg_name': $rule_count"
        return
    fi
    actual_rule=$("$AZ" rest --method GET --uri "$nsg_url" --query "$NSG_RULE_QUERY" --output tsv 2>&1)
    if [[ "$rule_count" != "1" || "$actual_rule" != "$expected_rule" ]]
    then
        fail "$label: NSG '$nsg_name' has $rule_count rule(s), the first '$actual_rule'; expected only '$expected_rule'"
        return
    fi

    if [[ -n "$expected_tag" ]]
    then
        actual_tag=$("$AZ" rest --method GET --uri "$nsg_url" --query 'json.tags."azclips-smoke"' --output tsv 2>&1)
        if [[ "$actual_tag" != "$expected_tag" ]]
        then
            fail "$label: NSG '$nsg_name' azclips-smoke tag '$actual_tag' did not match '$expected_tag'"
            return
        fi
    fi

    echo "OK: $label: NIC '$vm-nic' uses NSG '$nsg_name', whose only rule is '$expected_rule'"
}

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

if ! command -v ssh-keygen >/dev/null 2>&1; then
    echo "ssh-keygen is required to generate a temporary public key for --ssh-key-values." >&2
    exit 1
fi

ssh-keygen -q -t ed25519 -N "" -C "azclips-vmcreate-$SUFFIX" -f "$KEY_FILE" || exit 1

echo "== pre-create NSG $EXISTING_NSG (attached by cases 2 and 3) =="
existing_nsg_url=$(network_url networkSecurityGroups "$EXISTING_NSG")
existing_nsg_body=$(printf '{"location":"%s","tags":{"azclips-smoke":"reuse-nsg"},"properties":{"securityRules":[{"name":"smoke-allow-https","properties":{"protocol":"Tcp","sourcePortRange":"*","destinationPortRange":"443","sourceAddressPrefix":"*","destinationAddressPrefix":"*","access":"Allow","priority":1100,"direction":"Inbound"}}]}}' "$LOCATION")
# Without a ready NSG, cases 2 and 3 still run their public IP checks, just without --nsg.
EXISTING_NSG_READY=0
named_nsg_args=()
reuse_nsg_args=()
if "$AZ" rest --method PUT --uri "$existing_nsg_url" --body "$existing_nsg_body" --output json >/dev/null
then
    if wait_succeeded "existing nsg" "NSG '$EXISTING_NSG'" "$existing_nsg_url"
    then
        EXISTING_NSG_READY=1
        named_nsg_args=(--nsg "$EXISTING_NSG_ID")
        reuse_nsg_args=(--nsg "$EXISTING_NSG")
    fi
else
    fail "existing nsg: failed to pre-create NSG '$EXISTING_NSG'"
fi

echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP, default NSG ${DEFAULT_VM}NSG) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE"
else
    fail "default: vm create failed"
fi

echo "== 2. vm create --public-ip-address $NAMED_PUBLIC_IP --nsg <existing NSG id> (new public IP name, existing NSG) =="
# ${args[@]+"${args[@]}"} expands an empty array safely under set -u on older bash.
if output=$(create_vm "$NAMED_VM" --public-ip-address "$NAMED_PUBLIC_IP" ${named_nsg_args[@]+"${named_nsg_args[@]}"})
then
    check_public_ip "named" "$output" yes "$NAMED_PUBLIC_IP"
    if [[ "$EXISTING_NSG_READY" == "1" ]]
    then
        check_nsg "named" "$NAMED_VM" "$EXISTING_NSG_ID" "$EXISTING_NSG_RULE" reuse-nsg
    fi
else
    fail "named: vm create failed"
fi

echo "== 3. vm create --public-ip-address $REUSE_PUBLIC_IP --nsg $EXISTING_NSG (existing names) =="
reuse_public_ip_url=$(network_url publicIPAddresses "$REUSE_PUBLIC_IP")
reuse_public_ip_body=$(printf '{"location":"%s","sku":{"name":"Standard"},"tags":{"azclips-smoke":"reuse"},"properties":{"publicIPAllocationMethod":"Static"}}' "$LOCATION")
if "$AZ" rest --method PUT --uri "$reuse_public_ip_url" --body "$reuse_public_ip_body" --output json >/dev/null
then
    if wait_succeeded "reuse" "public IP '$REUSE_PUBLIC_IP'" "$reuse_public_ip_url"
    then
        if output=$(create_vm "$REUSE_VM" --public-ip-address "$REUSE_PUBLIC_IP" ${reuse_nsg_args[@]+"${reuse_nsg_args[@]}"})
        then
            check_public_ip "reuse" "$output" yes "$REUSE_PUBLIC_IP" reuse
            if [[ "$EXISTING_NSG_READY" == "1" ]]
            then
                check_nsg "reuse" "$REUSE_VM" "$EXISTING_NSG_ID" "$EXISTING_NSG_RULE" reuse-nsg
            fi
        else
            fail "reuse: vm create failed"
        fi
    else
        echo "== reuse: skipping vm create because public IP pre-create did not complete =="
    fi
else
    fail "reuse: failed to pre-create public IP '$REUSE_PUBLIC_IP'"
fi

echo "== 4. vm create --ssh-key-values <file> --public-ip-address \"\" (Linux, no public IP, default NSG ${NONE_VM}NSG) =="
if output=$(create_ssh_vm "$NONE_VM" --public-ip-address "")
then
    check_public_ip "none" "$output" no
    check_nsg "none" "$NONE_VM" "$LINUX_NSG_ID" "$SSH_NSG_RULE"
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
