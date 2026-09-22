#!/usr/bin/env bash
# Storage account advanced create live test for the Azclips CLI surface.
# Exercises the shared Storage account create request-shaping behavior selected in storage-account-create.
#
# Upstream evidence from remote main:
#   Azure CLI: azure-cli/src/azure-cli/azure/cli/command_modules/storage/tests/latest/test_storage_account_scenarios.py
#     - test_create_storage_account_with_assigned_identity
#     - test_storage_create_with_encryption_key_type
#     - test_create_storage_account_with_double_encryption
#     - test_create_storage_account_with_files_aadkerb
#     - test_storage_account_smb_oauth
#     - test_storage_account_service_endpoints
#     - test_storage_account_with_routing_preference
#     - test_storage_account_allowed_copy_scope
#     - test_storage_account_with_shared_key_access
#     - test_storage_account_sas_expiration_policy
#     - test_storage_account_with_key_and_sas_policy
#     - test_storage_account_with_alw
#     - test_create_storage_account
#   Azure PowerShell: azure-powershell/src/Storage/Storage.Management.Test/ScenarioTests/StorageAccountTests.ps1
#     - Test-SetAzureRmStorageAccountKeySource
#     - Test-NewAzureStorageAccountQueueTableEncrytionKeyType
#     - Test-NewSetAzStorageAccountFileAADKERB
#     - Test-StorageAccountEnableSmbOauth
#     - Test-NetworkRule
#     - Test-NewSetAzStorageAccount_RoutingPreference
#     - Test-AzureStorageAccountKeySASPolicy
#     - Test-NewSetAzureStorageAccountAllowSharedKeyAccess
#     - Test-AzureStorageAccountWorm
#     - Test-NewSetAzureStorageAccount_LargeFileShare
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. Have python3 or python on PATH for JSON assertions.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID                         - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP                    - resource group name; defaults to timestamped test group
#   AZCLIPS_LOCATION                          - Azure region; defaults to eastus
#   AZCLIPS_STORAGE_ACCOUNT_PREFIX            - lowercase account-name prefix, 3-20 chars; defaults to timestamped value
#   AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID   - optional subnet resource ID to include in network ACL creation
#   AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE      - optional tenantId=...;resourceId=... network ACL resource access rule
#   AZCLIPS_STORAGE_CMK_KEY_NAME              - optional Key Vault key name for CMK create coverage
#   AZCLIPS_STORAGE_CMK_KEY_VAULT_URI         - optional Key Vault URI for CMK create coverage
#   AZCLIPS_STORAGE_CMK_KEY_VERSION           - optional Key Vault key version for CMK create coverage
#   AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID - optional user-assigned identity resource ID for CMK coverage
#   AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID   - optional federated identity client ID for CMK coverage
#   AZCLIPS_KEEP_STORAGE                      - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG                        - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/Storage/StorageAccountCreate/StorageAccountCreateAdvanced.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../.." && pwd)"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
if [[ ! -x "$AZ" ]]; then
  echo "Built Azclips CLI was not found under artifacts/Debug/Core.Cli. Build the solution first." >&2
  exit 1
fi

export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
STAMP="$(date +%y%m%d%H%M%S)"
RAND="$(printf '%02d' "$((RANDOM % 100))")"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-storage-create-it-$STAMP-$RAND}"
ACCOUNT_PREFIX="${AZCLIPS_STORAGE_ACCOUNT_PREFIX:-azcsa$STAMP$RAND}"
SKU_NAME="Standard_LRS"
KIND="StorageV2"
SAS_EXPIRATION_PERIOD="1.02:03:04"

if [[ ! "$ACCOUNT_PREFIX" =~ ^[a-z0-9]{3,20}$ ]]; then
  echo "AZCLIPS_STORAGE_ACCOUNT_PREFIX must be 3-20 lowercase alphanumeric characters." >&2
  exit 1
fi

IDENTITY_ACCOUNT="${ACCOUNT_PREFIX}id"
FILES_ACCOUNT="${ACCOUNT_PREFIX}fl"
NETWORK_ACCOUNT="${ACCOUNT_PREFIX}nw"
POLICY_ACCOUNT="${ACCOUNT_PREFIX}pl"
IMMUTABLE_ACCOUNT="${ACCOUNT_PREFIX}im"
LARGE_FILE_ACCOUNT="${ACCOUNT_PREFIX}lf"
CMK_ACCOUNT="${ACCOUNT_PREFIX}cm"
CREATED_ACCOUNTS=()

find_python() {
  if command -v python3 >/dev/null 2>&1; then
    command -v python3
    return
  fi

  if command -v python >/dev/null 2>&1; then
    command -v python
    return
  fi

  echo "python3 or python must be on PATH for JSON assertions." >&2
  exit 1
}

PYTHON_BIN="$(find_python)"

json_scalar() {
  local json_payload="$1"
  local path="$2"

  JSON_PAYLOAD="$json_payload" "$PYTHON_BIN" - "$path" <<'PY'
import json
import os
import sys

data = json.loads(os.environ["JSON_PAYLOAD"])
for part in sys.argv[1].split("."):
    if not part:
        continue
    if isinstance(data, list):
        data = data[int(part)]
    elif isinstance(data, dict):
        data = data.get(part)
    else:
        data = None
    if data is None:
        break

if isinstance(data, bool):
    print("true" if data else "false")
elif data is None:
    print("")
else:
    print(data)
PY
}

assert_json_equals() {
  local json_payload="$1"
  local path="$2"
  local expected="$3"
  local actual

  actual="$(json_scalar "$json_payload" "$path")"
  if [[ "$actual" != "$expected" ]]; then
    echo "ASSERTION FAILED: expected JSON path '$path' to be '$expected', got '$actual'." >&2
    exit 1
  fi
}

assert_json_not_empty() {
  local json_payload="$1"
  local path="$2"
  local actual

  actual="$(json_scalar "$json_payload" "$path")"
  if [[ -z "$actual" ]]; then
    echo "ASSERTION FAILED: expected JSON path '$path' to be populated." >&2
    exit 1
  fi
}

assert_json_csv_contains() {
  local json_payload="$1"
  local path="$2"
  local expected="$3"
  local actual

  actual="$(json_scalar "$json_payload" "$path")"
  if ! tr ',' '\n' <<<"$actual" | sed 's/^ *//;s/ *$//' | grep -Fxq "$expected"; then
    echo "ASSERTION FAILED: expected JSON path '$path' value '$actual' to contain '$expected'." >&2
    exit 1
  fi
}

register_account() {
  CREATED_ACCOUNTS+=("$1")
}

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_STORAGE:-0}" != "1" ]]; then
    for account_name in "${CREATED_ACCOUNTS[@]}"; do
      echo "=== [storage account delete] cleanup $account_name ==="
      "$AZ" storage account delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$account_name" || true
    done
  fi

  if [[ "${AZCLIPS_CLEANUP_RG:-0}" == "1" ]]; then
    echo "=== [group delete] cleanup resource group ==="
    "$AZ" rest \
      --method DELETE \
      --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" || true
  fi

  exit "$exit_code"
}
trap cleanup EXIT

"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo "=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== [storage account create] identity and encryption ==="
register_account "$IDENTITY_ACCOUNT"
IDENTITY_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_ACCOUNT" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND" \
  --assign-identity \
  --encryption-services blob file \
  --encryption-key-type-for-table Account \
  --encryption-key-type-for-queue Account \
  --require-infrastructure-encryption)"

assert_json_equals "$IDENTITY_OUTPUT" "name" "$IDENTITY_ACCOUNT"
assert_json_equals "$IDENTITY_OUTPUT" "identity.type" "SystemAssigned"
assert_json_not_empty "$IDENTITY_OUTPUT" "identity.principalId"
assert_json_equals "$IDENTITY_OUTPUT" "properties.encryption.services.blob.enabled" "true"
assert_json_equals "$IDENTITY_OUTPUT" "properties.encryption.services.file.enabled" "true"
assert_json_equals "$IDENTITY_OUTPUT" "properties.encryption.services.table.keyType" "Account"
assert_json_equals "$IDENTITY_OUTPUT" "properties.encryption.services.queue.keyType" "Account"
assert_json_equals "$IDENTITY_OUTPUT" "properties.encryption.requireInfrastructureEncryption" "true"

echo "=== [storage account create] Azure Files identity authentication ==="
register_account "$FILES_ACCOUNT"
FILES_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FILES_ACCOUNT" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND" \
  --enable-files-aadkerb true \
  --domain-name mydomain.com \
  --domain-guid 12345678-1234-1234-1234-123456789012 \
  --default-share-permission StorageFileDataSmbShareReader \
  --enable-smb-oauth true)"

assert_json_equals "$FILES_OUTPUT" "name" "$FILES_ACCOUNT"
assert_json_equals "$FILES_OUTPUT" "properties.azureFilesIdentityBasedAuthentication.directoryServiceOptions" "AADKERB"
assert_json_equals "$FILES_OUTPUT" "properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainName" "mydomain.com"
assert_json_equals "$FILES_OUTPUT" "properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainGuid" "12345678-1234-1234-1234-123456789012"
assert_json_equals "$FILES_OUTPUT" "properties.azureFilesIdentityBasedAuthentication.defaultSharePermission" "StorageFileDataSmbShareReader"
assert_json_equals "$FILES_OUTPUT" "properties.azureFilesIdentityBasedAuthentication.smbOAuthSettings.isSmbOAuthEnabled" "true"

echo "=== [storage account create] network, routing, and copy scope ==="
register_account "$NETWORK_ACCOUNT"
NETWORK_ARGS=(
  storage account create
  --subscription-id "$SUBSCRIPTION_ID"
  --resource-group "$RESOURCE_GROUP"
  --name "$NETWORK_ACCOUNT"
  --location "$LOCATION"
  --sku "$SKU_NAME"
  --kind "$KIND"
  --bypass Logging Metrics AzureServices
  --default-action Deny
  --ip-rule 20.11.0.0/16
  --action Allow
  --routing-choice MicrosoftRouting
  --publish-microsoft-endpoints true
  --publish-internet-endpoints false
  --allowed-copy-scope PrivateLink
)
if [[ -n "${AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID:-}" ]]; then
  NETWORK_ARGS+=(--virtual-network-rule "$AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID")
fi
if [[ -n "${AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE:-}" ]]; then
  NETWORK_ARGS+=(--resource-access-rule "$AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE")
fi
NETWORK_OUTPUT="$("$AZ" "${NETWORK_ARGS[@]}")"

assert_json_equals "$NETWORK_OUTPUT" "name" "$NETWORK_ACCOUNT"
assert_json_csv_contains "$NETWORK_OUTPUT" "properties.networkAcls.bypass" "Logging"
assert_json_csv_contains "$NETWORK_OUTPUT" "properties.networkAcls.bypass" "Metrics"
assert_json_csv_contains "$NETWORK_OUTPUT" "properties.networkAcls.bypass" "AzureServices"
assert_json_equals "$NETWORK_OUTPUT" "properties.networkAcls.defaultAction" "Deny"
assert_json_equals "$NETWORK_OUTPUT" "properties.networkAcls.ipRules.0.value" "20.11.0.0/16"
assert_json_equals "$NETWORK_OUTPUT" "properties.networkAcls.ipRules.0.action" "Allow"
assert_json_equals "$NETWORK_OUTPUT" "properties.routingPreference.routingChoice" "MicrosoftRouting"
assert_json_equals "$NETWORK_OUTPUT" "properties.routingPreference.publishMicrosoftEndpoints" "true"
assert_json_equals "$NETWORK_OUTPUT" "properties.routingPreference.publishInternetEndpoints" "false"
assert_json_equals "$NETWORK_OUTPUT" "properties.allowedCopyScope" "PrivateLink"
if [[ -n "${AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID:-}" ]]; then
  assert_json_equals "$NETWORK_OUTPUT" "properties.networkAcls.virtualNetworkRules.0.id" "$AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID"
fi
if [[ -n "${AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE:-}" ]]; then
  assert_json_not_empty "$NETWORK_OUTPUT" "properties.networkAcls.resourceAccessRules.0.resourceId"
fi

echo "=== [storage account create] key, SAS, and cross-tenant policies ==="
register_account "$POLICY_ACCOUNT"
POLICY_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$POLICY_ACCOUNT" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND" \
  --allow-shared-key-access false \
  --sas-expiration-period "$SAS_EXPIRATION_PERIOD" \
  --key-expiration-period-in-days 5 \
  --allow-cross-tenant-replication false)"

assert_json_equals "$POLICY_OUTPUT" "name" "$POLICY_ACCOUNT"
assert_json_equals "$POLICY_OUTPUT" "properties.allowSharedKeyAccess" "false"
assert_json_equals "$POLICY_OUTPUT" "properties.sasPolicy.sasExpirationPeriod" "$SAS_EXPIRATION_PERIOD"
assert_json_equals "$POLICY_OUTPUT" "properties.sasPolicy.expirationAction" "Log"
assert_json_equals "$POLICY_OUTPUT" "properties.keyPolicy.keyExpirationPeriodInDays" "5"
assert_json_equals "$POLICY_OUTPUT" "properties.allowCrossTenantReplication" "false"

echo "=== [storage account create] account-level immutability ==="
register_account "$IMMUTABLE_ACCOUNT"
IMMUTABLE_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IMMUTABLE_ACCOUNT" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND" \
  --enable-alw \
  --immutability-period-since-creation-in-days 1 \
  --immutability-policy-state Disabled \
  --allow-protected-append-writes true)"

assert_json_equals "$IMMUTABLE_OUTPUT" "name" "$IMMUTABLE_ACCOUNT"
assert_json_equals "$IMMUTABLE_OUTPUT" "properties.immutableStorageWithVersioning.enabled" "true"
assert_json_equals "$IMMUTABLE_OUTPUT" "properties.immutableStorageWithVersioning.immutabilityPolicy.immutabilityPeriodSinceCreationInDays" "1"
assert_json_equals "$IMMUTABLE_OUTPUT" "properties.immutableStorageWithVersioning.immutabilityPolicy.state" "Disabled"
assert_json_equals "$IMMUTABLE_OUTPUT" "properties.immutableStorageWithVersioning.immutabilityPolicy.allowProtectedAppendWrites" "true"

echo "=== [storage account create] large file shares ==="
register_account "$LARGE_FILE_ACCOUNT"
LARGE_FILE_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$LARGE_FILE_ACCOUNT" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND" \
  --enable-large-file-share)"

assert_json_equals "$LARGE_FILE_OUTPUT" "name" "$LARGE_FILE_ACCOUNT"
assert_json_equals "$LARGE_FILE_OUTPUT" "properties.largeFileSharesState" "Enabled"

if [[ -n "${AZCLIPS_STORAGE_CMK_KEY_NAME:-}" && -n "${AZCLIPS_STORAGE_CMK_KEY_VAULT_URI:-}" ]]; then
  echo "=== [storage account create] customer-managed key ==="
  register_account "$CMK_ACCOUNT"
  CMK_ARGS=(
    storage account create
    --subscription-id "$SUBSCRIPTION_ID"
    --resource-group "$RESOURCE_GROUP"
    --name "$CMK_ACCOUNT"
    --location "$LOCATION"
    --sku "$SKU_NAME"
    --kind "$KIND"
    --encryption-key-source Microsoft.Keyvault
    --encryption-key-name "$AZCLIPS_STORAGE_CMK_KEY_NAME"
    --encryption-key-vault "$AZCLIPS_STORAGE_CMK_KEY_VAULT_URI"
  )
  if [[ -n "${AZCLIPS_STORAGE_CMK_KEY_VERSION:-}" ]]; then
    CMK_ARGS+=(--encryption-key-version "$AZCLIPS_STORAGE_CMK_KEY_VERSION")
  fi
  if [[ -n "${AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID:-}" ]]; then
    CMK_ARGS+=(--identity-type UserAssigned --user-identity-id "$AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID" --key-vault-user-identity-id "$AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID")
  fi
  if [[ -n "${AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID:-}" ]]; then
    CMK_ARGS+=(--federated-identity-client-id "$AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID")
  fi
  CMK_OUTPUT="$("$AZ" "${CMK_ARGS[@]}")"

  assert_json_equals "$CMK_OUTPUT" "name" "$CMK_ACCOUNT"
  assert_json_equals "$CMK_OUTPUT" "properties.encryption.keySource" "Microsoft.Keyvault"
  assert_json_equals "$CMK_OUTPUT" "properties.encryption.keyVaultProperties.keyName" "$AZCLIPS_STORAGE_CMK_KEY_NAME"
  assert_json_not_empty "$CMK_OUTPUT" "properties.encryption.keyVaultProperties.keyVaultUri"
  if [[ -n "${AZCLIPS_STORAGE_CMK_KEY_VERSION:-}" ]]; then
    assert_json_equals "$CMK_OUTPUT" "properties.encryption.keyVaultProperties.keyVersion" "$AZCLIPS_STORAGE_CMK_KEY_VERSION"
  fi
  if [[ -n "${AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID:-}" ]]; then
    assert_json_equals "$CMK_OUTPUT" "identity.type" "UserAssigned"
    assert_json_equals "$CMK_OUTPUT" "properties.encryption.encryptionIdentity.encryptionUserAssignedIdentity" "$AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID"
  fi
  if [[ -n "${AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID:-}" ]]; then
    assert_json_equals "$CMK_OUTPUT" "properties.encryption.encryptionIdentity.encryptionFederatedIdentityClientId" "$AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID"
  fi
else
  echo "Skipping customer-managed key create coverage; set AZCLIPS_STORAGE_CMK_KEY_NAME and AZCLIPS_STORAGE_CMK_KEY_VAULT_URI to enable it."
fi

echo "All advanced storage account create CLI checks completed."