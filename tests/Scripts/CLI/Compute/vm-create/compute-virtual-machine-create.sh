#!/usr/bin/env bash
# Live test for selected compute-virtual-machine-create custom behavior.
# Prereqs: build Azclips, sign in, and set AZCLIPS_SUBSCRIPTION_ID.

set -euo pipefail

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

require_env() {
    local name="$1"
    [[ -n "${!name:-}" ]] || fail "Required environment variable '$name' is not set."
}

assert_json_eq() {
    local json="$1"
    local filter="$2"
    local expected="$3"
    local message="$4"
    local actual
    actual="$(jq -r "($filter) // empty" <<<"$json")"
    [[ "$actual" == "$expected" ]] || fail "$message: expected '$expected', got '${actual:-<empty>}'"
}

assert_json_non_empty() {
    local json="$1"
    local filter="$2"
    local message="$3"
    local actual
    actual="$(jq -r "($filter) // empty" <<<"$json")"
    [[ -n "$actual" ]] || fail "$message: '$filter' was empty"
}

assert_status_in() {
    local json="$1"
    local allowed="$2"
    local message="$3"
    local status
    status="$(jq -r '.statusCode // empty' <<<"$json")"
    [[ " $allowed " == *" $status "* ]] || fail "$message: expected one of '$allowed', got '${status:-<empty>}'"
}

assert_rest_content_eq() {
    local response="$1"
    local filter="$2"
    local expected="$3"
    local message="$4"
    local actual
    actual="$(jq -r ".content | fromjson | ($filter)" <<<"$response")"
    [[ "$actual" == "$expected" ]] || fail "$message: expected '$expected', got '${actual:-<empty>}'"
}

assert_rest_content_eq_ci() {
    local response="$1"
    local filter="$2"
    local expected="$3"
    local message="$4"
    local actual_lower expected_lower
    actual_lower="$(jq -r ".content | fromjson | ($filter) // empty" <<<"$response" | tr '[:upper:]' '[:lower:]')"
    expected_lower="$(printf '%s' "$expected" | tr '[:upper:]' '[:lower:]')"
    [[ "$actual_lower" == "$expected_lower" ]] || fail "$message: expected '$expected', got '${actual_lower:-<empty>}'"
}

assert_rest_content_true() {
    local response="$1"
    local filter="$2"
    local message="$3"
    jq -e ".content | fromjson | ($filter)" <<<"$response" >/dev/null || fail "$message"
}

assert_rest_content_absent() {
    local response="$1"
    local filter="$2"
    local message="$3"
    jq -e ".content | fromjson | ($filter) == null" <<<"$response" >/dev/null || fail "$message"
}

command -v jq >/dev/null 2>&1 || fail "jq is required for JSON assertions."
require_env AZCLIPS_SUBSCRIPTION_ID

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
[[ -x "$AZ" ]] || fail "Built Azclips CLI not found. Build the solution first."

export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="$AZCLIPS_SUBSCRIPTION_ID"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
RUN_ID_RAW="${AZCLIPS_LIVE_TEST_RUN_ID:-$(date +%s)}"
RUN_ID="$(printf '%s' "$RUN_ID_RAW" | tr -cd '[:alnum:]' | tr '[:upper:]' '[:lower:]' | cut -c1-8)"
[[ -n "$RUN_ID" ]] || RUN_ID="$(date +%s | cut -c1-8)"

RESOURCE_GROUP="azclips-vm-create-$RUN_ID"
ADMIN_USERNAME="azclipstest"
ADMIN_PASSWORD="Azc${RUN_ID}!Pw9x"
STORAGE_ACCOUNT="azcdiag$RUN_ID"
BOOT_DIAGNOSTICS_URI="https://$STORAGE_ACCOUNT.blob.core.windows.net/"
IDENTITY_NAME="azc${RUN_ID}id"
IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IDENTITY_NAME"

TL_VM_NAME="azc${RUN_ID}a"
TL_NIC_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$TL_VM_NAME-nic"
TL_VNET_NAME="$TL_VM_NAME-vnet"
TL_NSG_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkSecurityGroups/$TL_VM_NAME-nsg"
PUBLIC_IP_NAME="azc${RUN_ID}pip"
PUBLIC_IP_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/publicIPAddresses/$PUBLIC_IP_NAME"
DNS_LABEL="$PUBLIC_IP_NAME"

STANDARD_VM_NAME="azc${RUN_ID}b"
STANDARD_NIC_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$STANDARD_VM_NAME-nic"

cleanup() {
    local exit_code=$?
    if [[ "${AZCLIPS_KEEP_RESOURCES:-0}" != "1" ]]; then
        "$AZ" rest \
            --method DELETE \
            --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" >/dev/null 2>&1 || true
    fi
    exit "$exit_code"
}
trap cleanup EXIT

"$AZ" config set --key agent.enabled --value false >/dev/null

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" >/dev/null

storage_body="{\"sku\":{\"name\":\"Standard_LRS\"},\"kind\":\"StorageV2\",\"location\":\"$LOCATION\",\"properties\":{\"minimumTlsVersion\":\"TLS1_2\",\"allowBlobPublicAccess\":false}}"
storage_response="$("$AZ" rest \
    --method PUT \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01" \
    --body "$storage_body")"
assert_status_in "$storage_response" "200 201 202" "storage account setup should be accepted for boot diagnostics"

identity_json="$("$AZ" identity create \
    --subscription "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$IDENTITY_NAME" \
    --location "$LOCATION")"
assert_json_eq "$identity_json" '.id | ascii_downcase' "${IDENTITY_ID,,}" "identity create should return the requested user-assigned identity id"

create_json="$("$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$TL_VM_NAME" \
    --location "$LOCATION" \
    --vm-size Standard_D2s_v3 \
    --admin-username "$ADMIN_USERNAME" \
    --admin-password "$ADMIN_PASSWORD" \
    --public-ip-address-name "$PUBLIC_IP_NAME" \
    --public-ip-address-domain-name-label "$DNS_LABEL" \
    --boot-diagnostics-enabled true \
    --boot-diagnostics-storage-uri "$BOOT_DIAGNOSTICS_URI" \
    --enable-accelerated-networking true \
    --system-assigned-identity true \
    --user-assigned-identity-ids "$IDENTITY_ID" \
    --enable-integrity-monitoring true \
    --disable-integrity-monitoring-auto-upgrade true)"

assert_json_eq "$create_json" '.name' "$TL_VM_NAME" "vm create should return the created TrustedLaunch VM name"
assert_json_non_empty "$create_json" '.id' "vm create should return the created TrustedLaunch VM id"

vm_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$TL_VM_NAME?api-version=2025-11-01")"
assert_status_in "$vm_response" "200" "TrustedLaunch VM GET should succeed"
assert_rest_content_eq "$vm_response" '.properties.storageProfile.imageReference.publisher' 'MicrosoftWindowsServer' "default image publisher should be applied"
assert_rest_content_eq "$vm_response" '.properties.storageProfile.imageReference.offer' 'WindowsServer' "default image offer should be applied"
assert_rest_content_eq "$vm_response" '.properties.storageProfile.imageReference.sku' '2022-datacenter-azure-edition' "default image SKU should be applied"
assert_rest_content_eq "$vm_response" '.properties.storageProfile.imageReference.version' 'latest' "default image version should be applied"
assert_rest_content_eq "$vm_response" '.properties.securityProfile.securityType' 'TrustedLaunch' "default security type should be TrustedLaunch"
assert_rest_content_eq "$vm_response" '.properties.securityProfile.uefiSettings.secureBootEnabled' 'true' "secure boot should be enabled by default"
assert_rest_content_eq "$vm_response" '.properties.securityProfile.uefiSettings.vTpmEnabled' 'true' "vTPM should be enabled by default"
assert_rest_content_eq "$vm_response" '.properties.diagnosticsProfile.bootDiagnostics.enabled' 'true' "boot diagnostics should be enabled when requested"
assert_rest_content_eq "$vm_response" '.properties.diagnosticsProfile.bootDiagnostics.storageUri' "$BOOT_DIAGNOSTICS_URI" "boot diagnostics should use the requested storage URI"
assert_rest_content_eq_ci "$vm_response" '.properties.networkProfile.networkInterfaces[0].id' "$TL_NIC_ID" "VM should reference the derived NIC id"
assert_rest_content_true "$vm_response" '.identity.type | ascii_downcase | contains("systemassigned")' "VM identity type should include SystemAssigned"
assert_rest_content_true "$vm_response" '.identity.type | ascii_downcase | contains("userassigned")' "VM identity type should include UserAssigned"
jq -e --arg id "$IDENTITY_ID" '.content | fromjson | .identity.userAssignedIdentities | has($id)' <<<"$vm_response" >/dev/null \
    || fail "VM identity should include the requested user-assigned identity id"

nic_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$TL_VM_NAME-nic?api-version=2025-05-01")"
assert_status_in "$nic_response" "200" "derived NIC GET should succeed"
assert_rest_content_eq_ci "$nic_response" '.properties.networkSecurityGroup.id' "$TL_NSG_ID" "derived NIC should reference the derived NSG"
assert_rest_content_eq_ci "$nic_response" '.properties.ipConfigurations[0].properties.publicIPAddress.id' "$PUBLIC_IP_ID" "derived NIC should reference the requested public IP"
assert_rest_content_eq "$nic_response" '.properties.enableAcceleratedNetworking' 'true' "NIC should enable accelerated networking when requested"

public_ip_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/publicIPAddresses/$PUBLIC_IP_NAME?api-version=2025-05-01")"
assert_status_in "$public_ip_response" "200" "public IP GET should succeed"
assert_rest_content_eq "$public_ip_response" '.sku.name' 'Standard' "orchestrated public IP should default to Standard SKU"
assert_rest_content_eq "$public_ip_response" '.properties.publicIPAllocationMethod' 'Static' "orchestrated public IP should default to static allocation"
assert_rest_content_eq "$public_ip_response" '.properties.dnsSettings.domainNameLabel' "$DNS_LABEL" "orchestrated public IP should use the requested DNS label"

extension_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$TL_VM_NAME/extensions/GuestAttestation?api-version=2025-11-01")"
assert_status_in "$extension_response" "200" "GuestAttestation extension GET should succeed when integrity monitoring is requested"
assert_rest_content_eq "$extension_response" '.name' 'GuestAttestation' "integrity monitoring should create the GuestAttestation extension"
assert_rest_content_eq "$extension_response" '.properties.type' 'GuestAttestation' "GuestAttestation extension type should be set"
assert_rest_content_eq "$extension_response" '.properties.publisher' 'Microsoft.Azure.Security.WindowsAttestation' "default Windows image should use the Windows attestation publisher"
assert_rest_content_eq "$extension_response" '.properties.enableAutomaticUpgrade' 'false' "disable integrity monitoring auto-upgrade should set extension auto-upgrade false"

standard_create_json="$("$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$STANDARD_VM_NAME" \
    --location "$LOCATION" \
    --vm-size Standard_D2s_v3 \
    --admin-username "$ADMIN_USERNAME" \
    --admin-password "$ADMIN_PASSWORD" \
    --security-type Standard \
    --no-public-ip-address true \
    --disable-accelerated-networking true \
    --disable-boot-diagnostics true)"
assert_json_eq "$standard_create_json" '.name' "$STANDARD_VM_NAME" "vm create should return the created Standard VM name"

standard_vm_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$STANDARD_VM_NAME?api-version=2025-11-01")"
assert_status_in "$standard_vm_response" "200" "Standard VM GET should succeed"
assert_rest_content_eq "$standard_vm_response" '.properties.storageProfile.imageReference.publisher' 'MicrosoftWindowsServer' "Standard security should preserve default image publisher"
assert_rest_content_eq "$standard_vm_response" '.properties.securityProfile.securityType' 'Standard' "Standard security should be effective"
assert_rest_content_eq "$standard_vm_response" '.properties.diagnosticsProfile.bootDiagnostics.enabled' 'false' "disable boot diagnostics should set boot diagnostics false"
assert_rest_content_eq_ci "$standard_vm_response" '.properties.networkProfile.networkInterfaces[0].id' "$STANDARD_NIC_ID" "Standard VM should reference its derived NIC id"

standard_nic_response="$("$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/networkInterfaces/$STANDARD_VM_NAME-nic?api-version=2025-05-01")"
assert_status_in "$standard_nic_response" "200" "Standard VM derived NIC GET should succeed"
assert_rest_content_absent "$standard_nic_response" '.properties.ipConfigurations[0].properties.publicIPAddress' "no-public-IP should omit the NIC public IP reference"
assert_rest_content_eq "$standard_nic_response" '.properties.enableAcceleratedNetworking' 'false' "disable accelerated networking should set the NIC property false"

if "$AZ" rest \
    --method GET \
    --uri "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$STANDARD_VM_NAME/extensions/GuestAttestation?api-version=2025-11-01" >/tmp/azclips_vm_standard_extension.out 2>&1; then
    cat /tmp/azclips_vm_standard_extension.out >&2
    fail "integrity monitoring omission should not create a GuestAttestation extension"
fi

echo "All compute-virtual-machine-create CLI live-test assertions passed."