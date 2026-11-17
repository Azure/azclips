#!/usr/bin/env bash
# Smoke test for vm create's public IP and network security group workflows through
# the built CLI.
#
# Creates VMs in one resource group, one per public IP mode/security mode, and checks the
# detail fields vm create returns:
#   1. vm create --security-type TrustedLaunch    -> creates <vm>PublicIP; publicIps is set;
#                --enable-secure-boot true            enables secure boot/vTPM; creates
#                --enable-vtpm true                    <vm>NSG with only the rdp rule;
#                --tags azclips-smoke=vm-tags          the VM, NIC, VNet, NSG and public IP
#                                                    all carry the azclips-smoke tag
#   2. vm create --security-type Standard --public-ip-address <new name>
#                --nsg <existing resource id>        creates that public IP; publicIps is set;
#                                                    attaches the pre-created NSG untouched
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set
#                --nsg <existing name>               attaches the pre-created NSG untouched
#   4. vm create --ssh-key-values <file>          -> Linux VM with no public IP; publicIps is
#                --public-ip-address ""              empty; creates <vm>NSG with only the
#                                                    default-allow-ssh rule
#   5. vm create --public-ip-address <missing id> -> fails with "was not found" before
#                                                    anything is created
#   6. vm show / vm delete / vm show              -> the last show is expected to fail
#   7. optional AZCLIPS_ENABLE_CVM=1              -> creates a ConfidentialVM with OS disk security encryption
#   8. optional AZCLIPS_ENABLE_EXISTING_NIC=1     -> subnet name/ID selection, missing-subnet
#                                                    preflight, two NICs and per-NIC delete options
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
# needs quota for four Standard_D2ls_v7 VMs and three Standard public IPs.
#
# Standard run:
#   export AZCLIPS_SUBSCRIPTION_ID=<subscription-id>
#   export AZCLIPS_VM_ADMIN_PASSWORD='<test-password>'
#   ./tests/Scripts/CLI/VMCreate.sh
#
# The standard run covers TrustedLaunch, Standard, public IP create/reuse/none,
# missing public IP validation, and a Linux SSH VM. The SSH VM uses a generated
# temporary key pair and no public IP by default.
#
# To preserve resources for portal/SSH inspection, temporarily comment out the
# cleanup trap/block below.
#
# Optional ConfidentialVM run:
#   export AZCLIPS_ENABLE_CVM=1
#   export AZCLIPS_CVM_IMAGE=Canonical:0001-com-ubuntu-confidential-vm-jammy:22_04-lts-cvm:latest
#   export AZCLIPS_CVM_SIZE=Standard_DC2as_v5
#   export AZCLIPS_CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE=VMGuestStateOnly
#   ./tests/Scripts/CLI/VMCreate.sh
#
# Optional networking run (three additional Standard_D2ls_v7 VMs, no public IPs):
#   export AZCLIPS_ENABLE_EXISTING_NIC=1
#   ./tests/Scripts/CLI/VMCreate.sh
#
# Case 4 deploys an Ubuntu image with a temporary key pair from ssh-keygen, deleted on exit.
#
# Before case 1 the script pre-creates an NSG through az rest, with its own tag and
# one rule, smoke-allow-https. The NSG checks read the VM's NIC and the NSG back
# through az rest: the NIC must reference the expected NSG, and the NSG must hold
# exactly one rule: rdp for case 1's default <vm>NSG, default-allow-ssh for case 4's,
# and smoke-allow-https, with the tag intact, for the NSG that cases 2 and 3 attach.
#
# ssh-keygen must be on PATH.
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
CVM_VM="vmc-cvm-$SUFFIX"
EXISTING_NIC_VM="vmc-nic-$SUFFIX"
SUBNET_NAME_VM="vmc-sub-$SUFFIX"
SUBNET_ID_VM="vmc-sid-$SUFFIX"
MISSING_SUBNET_VM="vmc-msu-$SUFFIX"
EXISTING_NIC_VNET="vmc-nic-vnet-$SUFFIX"
EXISTING_NIC_SUBNET="default"
EXISTING_NIC_NAME="vmc-existing-nic-$SUFFIX"
EXISTING_NIC_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$EXISTING_NIC_NAME"
SECOND_NIC_NAME="vmc-second-nic-$SUFFIX"
SECOND_NIC_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$SECOND_NIC_NAME"
EXISTING_NIC_SUBNET_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/virtualNetworks/$EXISTING_NIC_VNET/subnets/$EXISTING_NIC_SUBNET"
NAMED_PUBLIC_IP="azclips-vmcreate-pip-$SUFFIX"
REUSE_PUBLIC_IP="azclips-vmcreate-existing-pip-$SUFFIX"
SSH_PUBLIC_IP="azclips-vmcreate-ssh-pip-$SUFFIX"
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
# Case 1 passes this as --tags azclips-smoke=<value> and checks it on every resource it creates.
VM_TAG="vm-tags"
KEY_FILE="${TMPDIR:-/tmp}/azclips-vmcreate-key-$SUFFIX"
SSH_PUBLIC_KEY=""
CVM_ENABLED="${AZCLIPS_ENABLE_CVM:-0}"
CVM_IMAGE="${AZCLIPS_CVM_IMAGE:-}"
CVM_SIZE="${AZCLIPS_CVM_SIZE:-Standard_DC2as_v5}"
CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE="${AZCLIPS_CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE:-VMGuestStateOnly}"
EXISTING_NIC_ENABLED="${AZCLIPS_ENABLE_EXISTING_NIC:-0}"

FAILURES=0

fail()
{
    echo "FAIL: $1"
    FAILURES=$((FAILURES + 1))
}

warn_public_ssh_cleanup()
{
    echo "WARNING: Public SSH may still be exposed for VM '$NONE_VM' in resource group '$RESOURCE_GROUP'." >&2
    echo "WARNING: Remove VM '$NONE_VM' and its public IP/network resources in resource group '$RESOURCE_GROUP' as soon as possible." >&2
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

# Run unattended: prevents automatic agent launch
"$AZ" config set --key agent.recovery.trigger --value manual

cleanup()
{
    rm -f "$KEY_FILE" "$KEY_FILE.pub"

    if [[ "$OWNS_RESOURCE_GROUP" == "1" ]]
    then
        echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
        "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" || warn_public_ssh_cleanup
    else
        for vm in "$DEFAULT_VM" "$NAMED_VM" "$REUSE_VM" "$NONE_VM" "$MISSING_VM" "$CVM_VM" "$EXISTING_NIC_VM" "$SUBNET_NAME_VM" "$SUBNET_ID_VM" "$MISSING_SUBNET_VM"
        do
            echo "== cleanup: deleting VM $vm =="
            "$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$vm" || [[ "$vm" != "$NONE_VM" ]] || warn_public_ssh_cleanup
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
        --size Standard_D2ds_v6 \
        --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest \
        --admin-username Artemis \
        --admin-password "$ADMIN_PASSWORD" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --output json \
        "$@"
}

create_network_vm()
{
    local name="$1"
    shift
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" --location "$LOCATION" --size Standard_D2ds_v6 \
        --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest \
        --admin-username Artemis --admin-password "$ADMIN_PASSWORD" \
        --output json "$@"
}

check_vm_nics()
{
    local name="$1" expected="$2" actual
    local url="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$name?api-version=2025-11-01"
    if ! actual=$("$AZ" rest --method GET --uri "$url" \
        --query "json.properties.networkProfile.networkInterfaces[].join('|', [id, to_string(properties.primary), properties.deleteOption])" \
        --output tsv)
    then
        fail "$name: could not read VM NIC references"
    elif [[ "$(lowercase "$actual")" != "$(lowercase "$expected")" ]]
    then
        fail "$name: NIC references '$actual', expected '$expected'"
    fi
}

check_no_network_side_effects()
{
    local name="$1" type names
    for type in networkInterfaces networkSecurityGroups publicIPAddresses virtualNetworks
    do
        if ! names=$("$AZ" rest --method GET \
            --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/$type?api-version=2025-05-01" \
            --query 'json.value[].name' --output tsv)
        then
            fail "$name: could not list $type"
        elif grep -Fxiq -e "${name}VMNic" -e "${name}NSG" -e "${name}PublicIP" -e "$name-vnet" <<< "$names"
        then
            fail "$name: unexpected generated resource in $type"
        fi
    done
}

# Linux vm create variant that exercises --ssh-key-values with a generated public key.
create_ssh_vm()
{
    local name="$1"
    shift
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --size Standard_D2ds_v6 \
        --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest \
        --admin-username azadmin \
        --ssh-key-values "$SSH_PUBLIC_KEY" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --output json \
        "$@"
}

create_cvm()
{
    local name="$1"
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --size "$CVM_SIZE" \
        --image "$CVM_IMAGE" \
        --security-type ConfidentialVM \
        --os-disk-security-encryption-type "$CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE" \
        --admin-username Artemis \
        --admin-password "$ADMIN_PASSWORD" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --nsg "$name-nsg" \
        --public-ip-address "" \
        --output json
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

# The NIC <vm>VMNic must reference the NSG expected_id, compared case-insensitively because ARM
# can return the resource group in another case. That NSG must hold exactly one rule, equal to
# expected_rule, and carry the azclips-smoke tag expected_tag when one is given.
check_nsg()
{
    local label="$1" vm="$2" expected_id="$3" expected_rule="$4" expected_tag="${5:-}"
    local nsg_name="${expected_id##*/}"
    local nsg_url actual_id rule_count actual_rule actual_tag
    nsg_url=$(network_url networkSecurityGroups "$nsg_name")

    if ! actual_id=$("$AZ" rest --method GET --uri "$(network_url networkInterfaces "${vm}VMNic")" --query json.properties.networkSecurityGroup.id --output tsv 2>&1)
    then
        fail "$label: could not read NIC '${vm}VMNic': $actual_id"
        return
    fi
    if [[ "$(lowercase "$actual_id")" != "$(lowercase "$expected_id")" ]]
    then
        fail "$label: NIC '${vm}VMNic' references NSG '$actual_id', expected '$expected_id'"
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

    echo "OK: $label: NIC '${vm}VMNic' uses NSG '$nsg_name', whose only rule is '$expected_rule'"
}

# The network resource <type>/<name> must carry the azclips-smoke tag expected_tag.
check_network_tag()
{
    local label="$1" type="$2" name="$3" expected_tag="$4"
    local actual_tag

    if ! actual_tag=$("$AZ" rest --method GET --uri "$(network_url "$type" "$name")" --query 'json.tags."azclips-smoke"' --output tsv 2>&1)
    then
        fail "$label: could not read $type '$name': $actual_tag"
        return
    fi
    if [[ "$actual_tag" != "$expected_tag" ]]
    then
        fail "$label: $type '$name' azclips-smoke tag '$actual_tag' did not match '$expected_tag'"
        return
    fi

    echo "OK: $label: $type '$name' has azclips-smoke tag '$expected_tag'"
}

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

if ! command -v ssh-keygen >/dev/null 2>&1; then
    echo "ssh-keygen is required to generate a temporary public key for --ssh-key-values." >&2
    exit 1
fi

rm -f "$KEY_FILE" "$KEY_FILE.pub"
ssh-keygen -q -t ed25519 -N "" -C "azclips-vmcreate-$SUFFIX" -f "$KEY_FILE" || exit 1
SSH_PUBLIC_KEY="$(tr -d '\r\n' < "$KEY_FILE.pub")"

echo "== 1. vm create --security-type TrustedLaunch --enable-secure-boot true --enable-vtpm true --tags azclips-smoke=$VM_TAG (default public IP ${DEFAULT_VM}PublicIP) =="
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

if output=$(create_vm "$DEFAULT_VM" --security-type TrustedLaunch --enable-secure-boot true --enable-vtpm true --tags "azclips-smoke=$VM_TAG")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP" "$VM_TAG"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE" "$VM_TAG"
    vm_tag=$(json_field azclips-smoke "$output")
    if [[ "$vm_tag" == "$VM_TAG" ]]
    then
        echo "OK: default: VM '$DEFAULT_VM' has azclips-smoke tag '$VM_TAG'"
    else
        fail "default: VM '$DEFAULT_VM' azclips-smoke tag '$vm_tag' did not match '$VM_TAG'"
    fi
    check_network_tag "default" networkInterfaces "${DEFAULT_VM}VMNic" "$VM_TAG"
    check_network_tag "default" virtualNetworks "$DEFAULT_VM-vnet" "$VM_TAG"
else
    fail "default: vm create failed"
fi

echo "== 2. vm create --security-type Standard --public-ip-address $NAMED_PUBLIC_IP (new name) =="
# ${args[@]+"${args[@]}"} expands an empty array safely under set -u on older bash.
if output=$(create_vm "$NAMED_VM" --security-type Standard --public-ip-address "$NAMED_PUBLIC_IP" ${named_nsg_args[@]+"${named_nsg_args[@]}"})
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
    fail "ssh: vm create failed"
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

if [[ "$CVM_ENABLED" == "1" ]]
then
    echo "== 7. vm create --security-type ConfidentialVM --os-disk-security-encryption-type $CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE =="
    if [[ -z "$CVM_IMAGE" ]]
    then
        fail "cvm: AZCLIPS_CVM_IMAGE must be set when AZCLIPS_ENABLE_CVM=1"
    elif output=$(create_cvm "$CVM_VM")
    then
        check_public_ip "cvm" "$output" no
    else
        fail "cvm: vm create failed"
    fi
else
    echo "== 7. skipping ConfidentialVM live create; set AZCLIPS_ENABLE_CVM=1 and AZCLIPS_CVM_IMAGE to enable =="
fi

if [[ "$EXISTING_NIC_ENABLED" == "1" ]]
then
    echo "== 8. subnet selection, preflight and existing-NIC networking =="
    existing_nic_vnet_url=$(network_url virtualNetworks "$EXISTING_NIC_VNET")
    existing_nic_vnet_body=$(printf '{"location":"%s","tags":{"azclips-smoke":"network-selection"},"properties":{"addressSpace":{"addressPrefixes":["10.50.0.0/16"]},"subnets":[{"name":"%s","properties":{"addressPrefix":"10.50.0.0/24"}}]}}' "$LOCATION" "$EXISTING_NIC_SUBNET")
    existing_nic_url=$(network_url networkInterfaces "$EXISTING_NIC_NAME")
    existing_nic_body=$(printf '{"location":"%s","properties":{"ipConfigurations":[{"name":"ipconfig1","properties":{"privateIPAllocationMethod":"Dynamic","subnet":{"id":"%s"}}}]}}' "$LOCATION" "$EXISTING_NIC_SUBNET_ID")

    if "$AZ" rest --method PUT --uri "$existing_nic_vnet_url" --body "$existing_nic_vnet_body" --output json >/dev/null \
        && wait_succeeded "existing nic" "VNet '$EXISTING_NIC_VNET'" "$existing_nic_vnet_url" \
        && "$AZ" rest --method PUT --uri "$existing_nic_url" --body "$existing_nic_body" --output json >/dev/null \
        && wait_succeeded "existing nic" "NIC '$EXISTING_NIC_NAME'" "$existing_nic_url"
    then
        for vm in "$SUBNET_NAME_VM" "$SUBNET_ID_VM"
        do
            subnet_args=(--subnet "$EXISTING_NIC_SUBNET_ID")
            if [[ "$vm" == "$SUBNET_NAME_VM" ]]
            then
                subnet_args=(--subnet "$EXISTING_NIC_SUBNET" --virtual-network-name "$EXISTING_NIC_VNET")
            fi
            if create_network_vm "$vm" \
                "${subnet_args[@]}" --public-ip-address "" --nic-delete-option Delete >/dev/null
            then
                actual_subnet=$("$AZ" rest --method GET --uri "$(network_url networkInterfaces "${vm}VMNic")" \
                    --query 'json.properties.ipConfigurations[0].properties.subnet.id' --output tsv) \
                    || fail "$vm: could not read created NIC"
                [[ "$(lowercase "$actual_subnet")" == "$(lowercase "$EXISTING_NIC_SUBNET_ID")" ]] \
                    || fail "$vm: incorrect subnet '$actual_subnet'"
                # ARM can omit primary for a single NIC; check only ID and delete option.
                vm_url="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$vm?api-version=2025-11-01"
                actual_reference=$("$AZ" rest --method GET --uri "$vm_url" \
                    --query "join('|', [to_string(length(json.properties.networkProfile.networkInterfaces)), json.properties.networkProfile.networkInterfaces[0].id, json.properties.networkProfile.networkInterfaces[0].properties.deleteOption])" --output tsv) \
                    || fail "$vm: could not read VM NIC reference"
                expected_reference="1|/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/${vm}VMNic|Delete"
                [[ "$(lowercase "$actual_reference")" == "$(lowercase "$expected_reference")" ]] \
                    || fail "$vm: unexpected VM NIC reference '$actual_reference'"
            else
                fail "$vm: subnet selection failed"
            fi
        done
        vnet_tag=$("$AZ" rest --method GET --uri "$existing_nic_vnet_url" \
            --query 'json.tags."azclips-smoke"' --output tsv) || fail "subnet selection: could not read VNet tag"
        [[ "$vnet_tag" == "network-selection" ]] || fail "subnet selection: existing VNet was overwritten"

        missing_subnet_id="${EXISTING_NIC_SUBNET_ID}-missing"
        if error=$(create_network_vm "$MISSING_SUBNET_VM" \
            --subnet "$missing_subnet_id" 2>&1)
        then
            fail "missing subnet: expected failure"
        elif ! grep -Fq "Subnet '$missing_subnet_id' was not found." <<< "$error"
        then
            fail "missing subnet: unexpected error: $error"
        fi
        check_no_network_side_effects "$MISSING_SUBNET_VM"
        vm_names=$("$AZ" rest --method GET \
            --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01" \
            --query 'json.value[].name' --output tsv) || fail "missing subnet: could not list VMs"
        if grep -Fxiq "$MISSING_SUBNET_VM" <<< "$vm_names"
        then
            fail "missing subnet: VM was unexpectedly created"
        fi

        second_nic_url=$(network_url networkInterfaces "$SECOND_NIC_NAME")
        if "$AZ" rest --method PUT --uri "$second_nic_url" --body "$existing_nic_body" --output json >/dev/null \
            && wait_succeeded "existing nic" "NIC '$SECOND_NIC_NAME'" "$second_nic_url" \
            && create_network_vm "$EXISTING_NIC_VM" --nics "$EXISTING_NIC_ID" "$SECOND_NIC_NAME" \
                --nic-delete-option "$EXISTING_NIC_NAME=Detach" "$SECOND_NIC_NAME=Delete" >/dev/null
        then
            check_vm_nics "$EXISTING_NIC_VM" "$(printf '%s|true|Detach\n%s|false|Delete' "$EXISTING_NIC_ID" "$SECOND_NIC_ID")"
            check_no_network_side_effects "$EXISTING_NIC_VM"
        else
            fail "existing nic: vm create failed"
        fi
    else
        fail "existing nic: failed to pre-create VNet or NIC"
    fi
else
    echo "== 8. skipping existing-NIC live create; set AZCLIPS_ENABLE_EXISTING_NIC=1 to enable =="
fi

if (( FAILURES > 0 ))
then
    echo "== vm create smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== vm create smoke test finished =="
