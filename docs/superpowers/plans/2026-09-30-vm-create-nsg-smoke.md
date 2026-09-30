# VMCreate NSG smoke checks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the live `VMCreate` smoke scripts (bash and PowerShell) assert that vm create's default NSG is `{vm}NSG` with only the `rdp` rule, and that an existing NSG attaches untouched by name and by resource ID.

**Architecture:** No new VMs. The create helper stops passing an NSG, so every VM gets its default `{vm}NSG`. Before case 1, each script pre-creates a tagged NSG with one rule through the REST command. Case 1 checks its default NSG; cases 3 and 4 attach the pre-created NSG by name and by resource ID. Every check reads the VM's NIC and the NSG back through REST.

**Tech Stack:** bash (Git Bash on Windows; stays bash 3.2-compatible), PowerShell 7, the azclips `az rest` command (JMESPath `--query` via JmesPath.Net) and the azclips `Invoke-AzRestMethod` cmdlet.

**Spec:** `docs/superpowers/specs/2026-09-30-vm-create-nsg-design.md`, the "NSG smoke cases" bullet under Testing.

## Global Constraints

- Quota stays at four Standard_D2s_v3 VMs (8 vCPUs) and three Standard public IPs: no new VMs.
- Network api-version is `2025-05-01`.
- Default NSG name: the VM name followed by the literal `NSG`, e.g. `vmc-def-<suffix>NSG`.
- Default rule, as the scripts compare it: `rdp/Tcp/3389/Allow/Inbound/1000` (name/protocol/destination port/access/direction/priority).
- Pre-created NSG: name `azclips-vmcreate-existing-nsg-<suffix>`, tag `azclips-smoke=reuse-nsg`, exactly one rule `smoke-allow-https` — compared as `smoke-allow-https/Tcp/443/Allow/Inbound/1100`, with source address, source port and destination address `*`.
- Case 3 attaches it by name (`--nsg` / `-SecurityGroupName`), case 4 by resource ID. If its pre-create fails, the failure is recorded and cases 3 and 4 run their public IP checks without the NSG argument.
- NSG IDs are compared case-insensitively: ARM can return the resource group name in a different case.
- The scripts create real Azure resources: agents must NOT run them. Verify them with the bash and PowerShell parsers only; the user runs them live.
- Commit prefix `{Compute}` (test-only). End every commit message with the trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.

---

## File Structure

| File | Task | Responsibility |
|---|---|---|
| `tests/Scripts/CLI/VMCreate.sh` | 1 | Live smoke test for `az vm create`: public IP modes plus the NSG checks. |
| `tests/Scripts/PS/VMCreate.ps1` | 2 | Live smoke test for `New-AzVM`: the same cases through the Compute module. |

---

### Task 1: NSG checks in the CLI smoke script

**Files:**
- Modify: `tests/Scripts/CLI/VMCreate.sh` (whole file)

**Interfaces:**
- Consumes: `az vm create --nsg <name | resource id>` (a name is looked up in the VM's resource group and attached untouched when it exists; a resource ID must exist); the default NSG `<vm>NSG` with the `rdp` rule; `az rest --method GET|PUT --uri <url> [--body <json>] --query <jmespath> --output tsv|json`, whose response JMESPath root is `json`.
- Produces: bash functions `network_url TYPE NAME`, `lowercase TEXT`, `wait_succeeded LABEL WHAT URL` (replaces `wait_public_ip_succeeded`, and `network_url` replaces `public_ip_url`) and `check_nsg LABEL VM EXPECTED_ID EXPECTED_RULE [EXPECTED_TAG]`.

What changes from today's script:
- The header documents the NSG checks.
- New constants: the existing NSG's name and ID, the default NSG's ID, the rule query and the two expected rule strings.
- `network_url` replaces `public_ip_url`, and `wait_succeeded` replaces `wait_public_ip_succeeded`, so the public IP and the NSG share them.
- `create_vm` no longer passes `--nsg`.
- New `lowercase` and `check_nsg` functions.
- The NSG is pre-created right after `group create`.
- Case 1 calls `check_nsg`; cases 3 and 4 pass the pre-created NSG and call `check_nsg`.

- [ ] **Step 1: Replace `tests/Scripts/CLI/VMCreate.sh`**

```bash
#!/usr/bin/env bash
# Smoke test for vm create's public IP and network security group workflows through
# the built CLI.
#
# Creates four VMs in one resource group, one per public IP mode, and checks the
# detail fields vm create returns:
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set;
#                                                    creates <vm>NSG with only the rdp rule
#   2. vm create --public-ip-address <new name>   -> creates that public IP; publicIps is set
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set;
#                --nsg <existing name>               attaches the pre-created NSG untouched
#   4. vm create --public-ip-address ""           -> no public IP; publicIps is empty;
#                --nsg <existing resource id>        attaches the pre-created NSG untouched
#   5. vm create --public-ip-address <missing id> -> fails with "was not found" before
#                                                    anything is created
#   6. vm show / vm delete / vm show              -> the last show is expected to fail
#
# privateIps is checked alongside publicIps to prove the output was enriched: an
# empty publicIps with an empty privateIps means the detail lookup never ran.
#
# Case 3 pre-creates its public IP through az rest, then verifies vm create
# attaches that resource without replacing its tags.
#
# Before case 1 the script pre-creates an NSG through az rest, with its own tag and
# one rule, smoke-allow-https. The NSG checks read the VM's NIC and the NSG back
# through az rest: the NIC must reference the expected NSG, and the NSG must hold
# exactly one rule: rdp for case 1's default <vm>NSG, and smoke-allow-https, with
# the tag intact, for the NSG that cases 3 and 4 attach. Every other VM gets its own
# default <vm>NSG.
#
# Prereqs: build the solution first with dotnet build, then sign in. The subscription
# needs quota for four Standard_D2s_v3 VMs and three Standard public IPs.
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

# A rule as name/protocol/port/access/direction/priority, so whole rules compare as one string.
NSG_RULE_QUERY="json.properties.securityRules[0] | join('/', [name, properties.protocol, properties.destinationPortRange, properties.access, properties.direction, to_string(properties.priority)])"
DEFAULT_NSG_RULE="rdp/Tcp/3389/Allow/Inbound/1000"
EXISTING_NSG_RULE="smoke-allow-https/Tcp/443/Allow/Inbound/1100"

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

echo "== pre-create NSG $EXISTING_NSG (attached by cases 3 and 4) =="
existing_nsg_url=$(network_url networkSecurityGroups "$EXISTING_NSG")
existing_nsg_body=$(printf '{"location":"%s","tags":{"azclips-smoke":"reuse-nsg"},"properties":{"securityRules":[{"name":"smoke-allow-https","properties":{"protocol":"Tcp","sourcePortRange":"*","destinationPortRange":"443","sourceAddressPrefix":"*","destinationAddressPrefix":"*","access":"Allow","priority":1100,"direction":"Inbound"}}]}}' "$LOCATION")
# Without a ready NSG, cases 3 and 4 still run their public IP checks, just without --nsg.
EXISTING_NSG_READY=0
reuse_nsg_args=()
none_nsg_args=()
if "$AZ" rest --method PUT --uri "$existing_nsg_url" --body "$existing_nsg_body" --output json >/dev/null
then
    if wait_succeeded "existing nsg" "NSG '$EXISTING_NSG'" "$existing_nsg_url"
    then
        EXISTING_NSG_READY=1
        reuse_nsg_args=(--nsg "$EXISTING_NSG")
        none_nsg_args=(--nsg "$EXISTING_NSG_ID")
    fi
else
    fail "existing nsg: failed to pre-create NSG '$EXISTING_NSG'"
fi

echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP, default NSG ${DEFAULT_VM}NSG) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$DEFAULT_NSG_RULE"
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

echo "== 3. vm create --public-ip-address $REUSE_PUBLIC_IP --nsg $EXISTING_NSG (existing names) =="
reuse_public_ip_url=$(network_url publicIPAddresses "$REUSE_PUBLIC_IP")
reuse_public_ip_body=$(printf '{"location":"%s","sku":{"name":"Standard"},"tags":{"azclips-smoke":"reuse"},"properties":{"publicIPAllocationMethod":"Static"}}' "$LOCATION")
if "$AZ" rest --method PUT --uri "$reuse_public_ip_url" --body "$reuse_public_ip_body" --output json >/dev/null
then
    if wait_succeeded "reuse" "public IP '$REUSE_PUBLIC_IP'" "$reuse_public_ip_url"
    then
        # ${args[@]+"${args[@]}"} expands an empty array safely under set -u on older bash.
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

echo "== 4. vm create --public-ip-address \"\" --nsg <existing NSG id> (no public IP, existing NSG) =="
if output=$(create_vm "$NONE_VM" --public-ip-address "" ${none_nsg_args[@]+"${none_nsg_args[@]}"})
then
    check_public_ip "none" "$output" no
    if [[ "$EXISTING_NSG_READY" == "1" ]]
    then
        check_nsg "none" "$NONE_VM" "$EXISTING_NSG_ID" "$EXISTING_NSG_RULE" reuse-nsg
    fi
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
```

Keep the file's existing line endings (LF) and executable bit; replacing its content in place does both.

- [ ] **Step 2: Parse-check the script (do not run it)**

Run with Git's bash (`"C:\Program Files\Git\bin\bash.exe"` if `bash` on PATH is the WSL relay):

```bash
bash -n tests/Scripts/CLI/VMCreate.sh && echo "syntax OK"
```

Expected: `syntax OK`.

- [ ] **Step 3: Confirm nothing else references the removed helpers**

Run: `git grep -n -e "wait_public_ip_succeeded" -e "public_ip_url" -- tests/Scripts/CLI`
Expected: no output.

- [ ] **Step 4: Commit**

```bash
git add tests/Scripts/CLI/VMCreate.sh
git commit -m "{Compute} VMCreate.sh: check the default NSG and attaching an existing NSG by name and id" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: NSG checks in the PowerShell smoke script

**Files:**
- Modify: `tests/Scripts/PS/VMCreate.ps1` (whole file)

**Interfaces:**
- Consumes: `New-AzVM -SecurityGroupName <name | resource id>`, with the same behaviour as `--nsg`; the default NSG `<vm>NSG` with the `rdp` rule; `Invoke-AzRestMethod -Method GET|PUT -Uri <url> [-Payload <json>]`, whose response `.Content` is the ARM JSON.
- Produces: PowerShell functions `Get-NetworkUrl -Type -Name`, `Get-NetworkResource -Type -Name`, `Wait-ResourceSucceeded -Label -Type -Name -What` (replaces `Wait-PublicIpSucceeded`, while `Get-NetworkUrl` and `Get-NetworkResource` replace `Get-PublicIpUrl` and `Get-PublicIpByName`), `Format-NsgRule $Rule` and `Test-Nsg -Label -VmName -ExpectedId -ExpectedRule [-ExpectedSmokeTag]`.

What changes from today's script: the same changes as Task 1's bash script, in PowerShell. `-eq` / `-ne` already compare strings case-insensitively, which gives the case-insensitive NSG ID comparison.

- [ ] **Step 1: Replace `tests/Scripts/PS/VMCreate.ps1`**

```powershell
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for New-AzVM's public IP and network security group workflows through
# the built Compute module.
#
# Creates four VMs in one resource group, one per public IP mode, and checks the
# detail fields New-AzVM returns:
#   1. New-AzVM                                    -> creates <vm>PublicIP; PublicIps is set;
#                                                     creates <vm>NSG with only the rdp rule
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName <existing>    -> reuses that public IP; PublicIps is set;
#               -SecurityGroupName <existing name>    attaches the pre-created NSG untouched
#   4. New-AzVM -PublicIpAddressName ''            -> no public IP; PublicIps is empty;
#               -SecurityGroupName <existing id>      attaches the pre-created NSG untouched
#   5. New-AzVM -PublicIpAddressName <missing id>  -> fails with "was not found" before
#                                                     anything is created
#   6. Get-AzVM / Remove-AzVM / Get-AzVM           -> the last get is expected to fail
#
# PrivateIps is checked alongside PublicIps to prove the output was enriched: an
# empty PublicIps with an empty PrivateIps means the detail lookup never ran.
#
# Case 3 pre-creates its public IP through Invoke-AzRestMethod, then verifies
# New-AzVM attaches that resource without replacing its tags.
#
# Before case 1 the script pre-creates an NSG through Invoke-AzRestMethod, with its
# own tag and one rule, smoke-allow-https. The NSG checks read the VM's NIC and the
# NSG back through Invoke-AzRestMethod: the NIC must reference the expected NSG, and
# the NSG must hold exactly one rule: rdp for case 1's default <vm>NSG, and
# smoke-allow-https, with the tag intact, for the NSG that cases 3 and 4 attach.
# Every other VM gets its own default <vm>NSG.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
# The subscription needs quota for four Standard_D2s_v3 VMs and three Standard public IPs.
#
# Cleanup: on exit, whether the run passed or failed, the resource group this script
# created is deleted with Remove-AzResourceGroup, which removes the VMs, public IPs,
# NICs, VNets and NSGs with it, including the pre-created public IP and NSG. A group
# named through $env:AZCLIPS_RESOURCE_GROUP may already hold other resources, so the
# script only removes its VMs there and leaves the public IPs and network resources,
# including the pre-created NSG, to you.
#
# Usage: ./tests/Scripts/PS/VMCreate.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID % 1000000
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmcreate-rg-$suffix" }

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
$defaultVm         = "vmc-def-$suffix"
$namedVm           = "vmc-nam-$suffix"
$reuseVm           = "vmc-reu-$suffix"
$noneVm            = "vmc-non-$suffix"
$missingVm         = "vmc-mis-$suffix"
$namedPublicIp     = "azclips-vmcreate-pip-$suffix"
$reusePublicIp     = "azclips-vmcreate-existing-pip-$suffix"
$missingPublicIpId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$suffix"
$existingNsg       = "azclips-vmcreate-existing-nsg-$suffix"
$existingNsgId     = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkSecurityGroups/$existingNsg"
$defaultNsgId      = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkSecurityGroups/${defaultVm}NSG"

# Rules as name/protocol/port/access/direction/priority; see Format-NsgRule.
$defaultNsgRule    = 'rdp/Tcp/3389/Allow/Inbound/1000'
$existingNsgRule   = 'smoke-allow-https/Tcp/443/Allow/Inbound/1100'

# Fail fast with an actionable message. Checking existence is not enough: the official
# Az modules (Az.Resources, Az.Compute) export the same cmdlet names and PowerShell
# auto-loads them on first use, so an un-imported azclips module silently resolves to
# the official one — which rejects -SubscriptionId and fails mid-script.
$requiredCommands = [ordered]@{
    'New-AzResourceGroup'    = 'Resources'
    'Remove-AzResourceGroup' = 'Resources'
    'Invoke-AzRestMethod'    = 'Resources'
    'New-AzVM'               = 'Compute'
    'Get-AzVM'               = 'Compute'
    'Remove-AzVM'            = 'Compute'
}

foreach ($requiredCommand in $requiredCommands.Keys)
{
    $expectedModule = $requiredCommands[$requiredCommand]
    $resolved = Get-Command $requiredCommand -ErrorAction SilentlyContinue

    if (-not $resolved)
    {
        throw "$requiredCommand is not available. Import the azclips modules first:`n" +
              "    Import-Module ./src/PS/Accounts/Accounts.psd1`n" +
              "    Import-Module ./src/PS/Resources/Resources.psd1`n" +
              "    Import-Module ./src/PS/Compute/Compute.psd1"
    }

    if ($resolved.ModuleName -ne $expectedModule)
    {
        throw "$requiredCommand resolves to module '$($resolved.ModuleName)' " +
              "(version $($resolved.Version)), not the azclips '$expectedModule' module.`n" +
              "The official Az modules shadow azclips unless you import it first:`n" +
              "    Import-Module ./src/PS/$expectedModule/$expectedModule.psd1`n" +
              "If the official Az module is already loaded in this session, start a fresh " +
              "pwsh session and import the azclips modules before running this script."
    }
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message)
{
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
}

# ARM URL of a network resource in this script's resource group, e.g. -Type publicIPAddresses.
function Get-NetworkUrl([string]$Type, [string]$Name)
{
    "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/$Type/${Name}?api-version=2025-05-01"
}

function Get-NetworkResource([string]$Type, [string]$Name)
{
    $response = Invoke-AzRestMethod -Method GET -Uri (Get-NetworkUrl -Type $Type -Name $Name)
    $response.Content | ConvertFrom-Json
}

# Polls a network resource until its provisioningState is Succeeded; $What names it in messages.
function Wait-ResourceSucceeded([string]$Label, [string]$Type, [string]$Name, [string]$What)
{
    $lastResult = $null
    for ($attempt = 0; $attempt -lt 24; $attempt++)
    {
        try
        {
            $resource = Get-NetworkResource -Type $Type -Name $Name
            $lastResult = $resource.properties.provisioningState
            if ($lastResult -eq 'Succeeded')
            {
                Write-Host "OK: ${Label}: $What provisioningState is Succeeded"
                return $true
            }
            Write-Host "== waiting for ${What}: provisioningState='$lastResult' =="
        }
        catch
        {
            $lastResult = $_.Exception.Message
            Write-Host "== waiting for ${What}: GET failed: $lastResult =="
        }
        Start-Sleep -Seconds 5
    }

    Add-Failure "${Label}: $What did not reach provisioningState Succeeded after about 2 minutes (last result: $lastResult)"
    $false
}

# New-AzVM with per-VM network resource names; $Extra is splatted on top. Unless $Extra
# sets SecurityGroupName, the VM gets its default <vm>NSG.
function New-SmokeVM([string]$Name, [hashtable]$Extra = @{})
{
    $parameters = @{
        SubscriptionId        = $subscriptionId
        ResourceGroupName     = $resourceGroup
        Name                  = $Name
        Location              = $location
        VmSize                = 'Standard_D2s_v3'
        AdminUsername         = 'Artemis'
        AdminPassword         = $env:AZCLIPS_VM_ADMIN_PASSWORD
        NetworkInterfaceName  = "$Name-nic"
        VirtualNetworkName    = "$Name-vnet"
        AddressPrefixes       = '10.0.0.0/16'
        SubnetNames           = "$Name-subnet"
        SubnetAddressPrefixes = '10.0.2.0/24'
    }
    New-AzVM @parameters @Extra
}

function Test-PublicIpResource([string]$Label, [string]$ExpectedName, [string]$ExpectedAddress, [string]$ExpectedSmokeTag = '')
{
    try
    {
        $publicIp = Get-NetworkResource -Type 'publicIPAddresses' -Name $ExpectedName
    }
    catch
    {
        Add-Failure "${Label}: could not read public IP '$ExpectedName': $($_.Exception.Message)"
        return
    }

    if ($publicIp.properties.ipAddress -ne $ExpectedAddress)
    {
        Add-Failure "${Label}: public IP '$ExpectedName' address '$($publicIp.properties.ipAddress)' did not match PublicIps '$ExpectedAddress'"
        return
    }

    if ($ExpectedSmokeTag -and $publicIp.tags.'azclips-smoke' -ne $ExpectedSmokeTag)
    {
        Add-Failure "${Label}: public IP '$ExpectedName' azclips-smoke tag '$($publicIp.tags.'azclips-smoke')' did not match '$ExpectedSmokeTag'"
        return
    }

    Write-Host "OK: ${Label}: public IP '$ExpectedName' address matches PublicIps='$ExpectedAddress'"
}

# PrivateIps proves the detail lookup ran; then PublicIps must be set or empty as expected.
function Test-PublicIp([string]$Label, $Vm, [bool]$ExpectPublicIp, [string]$ExpectedPublicIpName = '', [string]$ExpectedSmokeTag = '')
{
    if ([string]::IsNullOrEmpty($Vm.PrivateIps))
    {
        Add-Failure "${Label}: PrivateIps is empty, so the output was not enriched"
    }
    elseif ($ExpectPublicIp -and [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected a public IP, got PublicIps=''"
    }
    elseif (-not $ExpectPublicIp -and -not [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected no public IP, got PublicIps='$($Vm.PublicIps)'"
    }
    else
    {
        Write-Host "OK: ${Label}: PublicIps='$($Vm.PublicIps)' PrivateIps='$($Vm.PrivateIps)'"
    }

    if ($ExpectPublicIp -and -not [string]::IsNullOrEmpty($Vm.PublicIps) -and -not [string]::IsNullOrEmpty($ExpectedPublicIpName))
    {
        Test-PublicIpResource -Label $Label -ExpectedName $ExpectedPublicIpName -ExpectedAddress $Vm.PublicIps -ExpectedSmokeTag $ExpectedSmokeTag
    }
}

# A rule as name/protocol/port/access/direction/priority, so whole rules compare as one string.
function Format-NsgRule($Rule)
{
    $p = $Rule.properties
    "$($Rule.name)/$($p.protocol)/$($p.destinationPortRange)/$($p.access)/$($p.direction)/$($p.priority)"
}

# The NIC <vm>-nic must reference the NSG $ExpectedId; -ne compares case-insensitively, which
# matters because ARM can return the resource group in another case. That NSG must hold exactly
# one rule, equal to $ExpectedRule, and carry the azclips-smoke tag $ExpectedSmokeTag when given.
function Test-Nsg([string]$Label, [string]$VmName, [string]$ExpectedId, [string]$ExpectedRule, [string]$ExpectedSmokeTag = '')
{
    $nsgName = ($ExpectedId -split '/')[-1]
    try
    {
        $nic = Get-NetworkResource -Type 'networkInterfaces' -Name "$VmName-nic"
        $nsg = Get-NetworkResource -Type 'networkSecurityGroups' -Name $nsgName
    }
    catch
    {
        Add-Failure "${Label}: could not read NIC '$VmName-nic' or NSG '$nsgName': $($_.Exception.Message)"
        return
    }

    if ($nic.properties.networkSecurityGroup.id -ne $ExpectedId)
    {
        Add-Failure "${Label}: NIC '$VmName-nic' references NSG '$($nic.properties.networkSecurityGroup.id)', expected '$ExpectedId'"
        return
    }

    $rules = @($nsg.properties.securityRules | Where-Object { $_ })
    $actualRule = if ($rules.Count -gt 0) { Format-NsgRule $rules[0] } else { '' }
    if ($rules.Count -ne 1 -or $actualRule -ne $ExpectedRule)
    {
        Add-Failure "${Label}: NSG '$nsgName' has $($rules.Count) rule(s), the first '$actualRule'; expected only '$ExpectedRule'"
        return
    }

    if ($ExpectedSmokeTag -and $nsg.tags.'azclips-smoke' -ne $ExpectedSmokeTag)
    {
        Add-Failure "${Label}: NSG '$nsgName' azclips-smoke tag '$($nsg.tags.'azclips-smoke')' did not match '$ExpectedSmokeTag'"
        return
    }

    Write-Host "OK: ${Label}: NIC '$VmName-nic' uses NSG '$nsgName', whose only rule is '$ExpectedRule'"
}

# Guard the cleanup block: each resource is only deleted once it exists.
$groupCreated = $false
$createdVms   = [System.Collections.Generic.List[string]]::new()

# Only a group this script named itself is safe to delete. One supplied through
# AZCLIPS_RESOURCE_GROUP may already hold other resources.
$ownsResourceGroup = -not $env:AZCLIPS_RESOURCE_GROUP

try
{
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Location $location
    $groupCreated = $true

    Write-Host "== pre-create NSG $existingNsg (attached by cases 3 and 4) =="
    $nsgBody = @{
        location = $location
        tags = @{ 'azclips-smoke' = 'reuse-nsg' }
        properties = @{
            securityRules = @(
                @{
                    name = 'smoke-allow-https'
                    properties = @{
                        protocol                 = 'Tcp'
                        sourcePortRange          = '*'
                        destinationPortRange     = '443'
                        sourceAddressPrefix      = '*'
                        destinationAddressPrefix = '*'
                        access                   = 'Allow'
                        priority                 = 1100
                        direction                = 'Inbound'
                    }
                }
            )
        }
    }
    # Without a ready NSG, cases 3 and 4 still run their public IP checks, just without it.
    $existingNsgReady = $false
    try
    {
        $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'networkSecurityGroups' -Name $existingNsg) -Payload ($nsgBody | ConvertTo-Json -Depth 6 -Compress)
        $existingNsgReady = Wait-ResourceSucceeded -Label 'existing nsg' -Type 'networkSecurityGroups' -Name $existingNsg -What "NSG '$existingNsg'"
    }
    catch
    {
        Add-Failure "existing nsg: failed to pre-create NSG '$existingNsg': $($_.Exception.Message)"
    }

    Write-Host "== 1. New-AzVM (default public IP ${defaultVm}PublicIP, default NSG ${defaultVm}NSG) =="
    $createdVms.Add($defaultVm)
    $defaultResult = New-SmokeVM -Name $defaultVm
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule

    Write-Host "== 2. New-AzVM -PublicIpAddressName $namedPublicIp (new name) =="
    $createdVms.Add($namedVm)
    $namedResult = New-SmokeVM -Name $namedVm -Extra @{ PublicIpAddressName = $namedPublicIp }
    Test-PublicIp -Label 'named' -Vm $namedResult -ExpectPublicIp $true -ExpectedPublicIpName $namedPublicIp

    Write-Host "== 3. New-AzVM -PublicIpAddressName $reusePublicIp -SecurityGroupName $existingNsg (existing names) =="
    $body = @{
        location = $location
        sku = @{ name = 'Standard' }
        tags = @{ 'azclips-smoke' = 'reuse' }
        properties = @{ publicIPAllocationMethod = 'Static' }
    }
    $reusePublicIpCreated = $false
    try
    {
        $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'publicIPAddresses' -Name $reusePublicIp) -Payload ($body | ConvertTo-Json -Depth 5 -Compress)
        $reusePublicIpCreated = $true
    }
    catch
    {
        Add-Failure "reuse: failed to pre-create public IP '$reusePublicIp': $($_.Exception.Message)"
    }

    if ($reusePublicIpCreated)
    {
        if (Wait-ResourceSucceeded -Label 'reuse' -Type 'publicIPAddresses' -Name $reusePublicIp -What "public IP '$reusePublicIp'")
        {
            $createdVms.Add($reuseVm)
            $reuseExtra = @{ PublicIpAddressName = $reusePublicIp }
            if ($existingNsgReady) { $reuseExtra.SecurityGroupName = $existingNsg }
            $reuseResult = New-SmokeVM -Name $reuseVm -Extra $reuseExtra
            Test-PublicIp -Label 'reuse' -Vm $reuseResult -ExpectPublicIp $true -ExpectedPublicIpName $reusePublicIp -ExpectedSmokeTag 'reuse'
            if ($existingNsgReady)
            {
                Test-Nsg -Label 'reuse' -VmName $reuseVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
            }
        }
        else
        {
            Write-Host '== reuse: skipping New-AzVM because public IP pre-create did not complete =='
        }
    }

    Write-Host "== 4. New-AzVM -PublicIpAddressName '' -SecurityGroupName <existing NSG id> (no public IP, existing NSG) =="
    $createdVms.Add($noneVm)
    $noneExtra = @{ PublicIpAddressName = '' }
    if ($existingNsgReady) { $noneExtra.SecurityGroupName = $existingNsgId }
    Test-PublicIp -Label 'none' -Vm (New-SmokeVM -Name $noneVm -Extra $noneExtra) -ExpectPublicIp $false
    if ($existingNsgReady)
    {
        Test-Nsg -Label 'none' -VmName $noneVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
    }

    Write-Host "== 5. New-AzVM -PublicIpAddressName <missing resource id> (expect 'was not found') =="
    $createdVms.Add($missingVm)
    $expected = "Public IP address '$missingPublicIpId' was not found."
    try
    {
        $null = New-SmokeVM -Name $missingVm -Extra @{ PublicIpAddressName = $missingPublicIpId }
        Add-Failure 'missing id: expected New-AzVM to fail'
    }
    catch
    {
        if ("$_".Contains($expected) -or $_.Exception.Message.Contains($expected))
        {
            Write-Host 'OK: missing id: failed with the expected message'
        }
        else
        {
            Add-Failure "missing id: New-AzVM failed, but not with the expected message: $_"
        }
    }

    Write-Host '== 6. Get-AzVM, Remove-AzVM, Get-AzVM (the last get is expected to fail) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
    Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm -Force
    $null = $createdVms.Remove($defaultVm)
    try
    {
        $null = Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
        Add-Failure "Get-AzVM still finds $defaultVm after Remove-AzVM"
    }
    catch
    {
        Write-Host "OK: $defaultVm no longer exists"
    }

    if ($failures.Count -gt 0)
    {
        throw "New-AzVM smoke test finished with $($failures.Count) failure(s)."
    }
    Write-Host '== New-AzVM smoke test finished =='
}
finally
{
    # Cleanup failures are warnings, not throws: a throw here would replace the exception
    # that ended the run.
    if ($groupCreated -and $ownsResourceGroup)
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        try
        {
            $null = Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Force
        }
        catch
        {
            Write-Warning ("Could not delete resource group ${resourceGroup}. Remove it with " +
                "'az group delete --name $resourceGroup --yes'. Error: $($_.Exception.Message)")
        }
    }
    elseif ($groupCreated)
    {
        foreach ($vm in $createdVms)
        {
            Write-Host "== cleanup: removing VM $vm =="
            try
            {
                Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vm -Force
            }
            catch
            {
                Write-Warning "Could not remove VM ${vm}. Error: $($_.Exception.Message)"
            }
        }
        Write-Host "== cleanup: kept resource group $resourceGroup because AZCLIPS_RESOURCE_GROUP supplied it; remove its public IPs and network resources yourself =="
    }
}
```

Keep the file's existing line endings.

- [ ] **Step 2: Parse-check the script (do not run it)**

```powershell
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path tests/Scripts/PS/VMCreate.ps1), [ref]$null, [ref]$errors) | Out-Null
if ($errors) { $errors } else { 'syntax OK' }
```

Expected: `syntax OK`.

- [ ] **Step 3: Confirm nothing else references the removed helpers**

Run: `git grep -n -e "Wait-PublicIpSucceeded" -e "Get-PublicIpUrl" -e "Get-PublicIpByName" -- tests/Scripts/PS`
Expected: no output.

- [ ] **Step 4: Commit**

```bash
git add tests/Scripts/PS/VMCreate.ps1
git commit -m "{Compute} VMCreate.ps1: check the default NSG and attaching an existing NSG by name and id" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Run both smoke scripts live (user, with credentials)

**This task is run by the user.** It creates real Azure resources. An agent must not run it.

- [ ] **Step 1: Build (Debug)**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`.

- [ ] **Step 2: Run the CLI script** (Git Bash, from the repository root)

```bash
AZCLIPS_SUBSCRIPTION_ID=<test-subscription> AZCLIPS_VM_ADMIN_PASSWORD='<strong-password>' ./tests/Scripts/CLI/VMCreate.sh
```

Expected: `OK:` lines for the pre-created NSG, then for each case. In particular:
- `OK: default: NIC '<vm>-nic' uses NSG '<vm>NSG', whose only rule is 'rdp/Tcp/3389/Allow/Inbound/1000'`
- the same line for `reuse` and for `none`, naming `azclips-vmcreate-existing-nsg-<suffix>` and `smoke-allow-https/Tcp/443/Allow/Inbound/1100`

The run should end with `== vm create smoke test finished ==` and delete its resource group.

- [ ] **Step 3: Run the PowerShell script** (a fresh `pwsh` session, from the repository root)

```powershell
$env:AZCLIPS_SUBSCRIPTION_ID = '<test-subscription>'
$env:AZCLIPS_VM_ADMIN_PASSWORD = '<strong-password>'
Import-Module ./src/PS/Accounts/Accounts.psd1
Import-Module ./src/PS/Resources/Resources.psd1
Import-Module ./src/PS/Compute/Compute.psd1
Connect-AzAccount
./tests/Scripts/PS/VMCreate.ps1
```

Expected: the same `OK:` lines, ending with `== New-AzVM smoke test finished ==` and the resource group deleted.

---

## Spec coverage

| Spec requirement ("NSG smoke cases") | Task |
|---|---|
| Folded into the existing four VMs; quota unchanged | 1, 2 |
| Create helper passes no NSG argument; VMs get `{vm}NSG` | 1, 2 |
| Pre-created NSG `azclips-vmcreate-existing-nsg-<suffix>`, tag `azclips-smoke=reuse-nsg`, one rule `smoke-allow-https` (TCP 443, priority 1100), waited to `Succeeded`; a failed pre-create is recorded and cases 3 and 4 run without it | 1, 2 |
| Case 1: `{vm}NSG` has exactly the `rdp` rule and its NIC references it | 1, 2 |
| Case 3 attaches by name, case 4 by resource ID; NIC references it; tag and single rule unchanged | 1, 2 |
| Case-insensitive NSG ID comparison | 1 (`lowercase`), 2 (`-ne`) |
| Shared wait-for-`Succeeded` helper; headers and cleanup notes updated | 1, 2 |
| Scripts are run live by the user | 3 |
