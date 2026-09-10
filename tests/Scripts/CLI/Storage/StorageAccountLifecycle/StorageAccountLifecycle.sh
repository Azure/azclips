#!/usr/bin/env bash
# Storage account lifecycle smoke test for the azclips CLI surface.
# Exercises create, show, list, and delete.
#
# Upstream evidence from remote main:
#   Azure CLI: azure-cli/src/azure-cli/azure/cli/command_modules/storage/tests/latest/test_storage_account_scenarios.py
#     - test_create_storage_account creates a Standard_LRS StorageV2 account, validates list/show, and deletes it.
#     - test_list_storage_accounts validates subscription-scope list contains created accounts.
#   Azure PowerShell: azure-powershell/src/Storage/Storage.Management.Test/ScenarioTests/StorageAccountTests.ps1
#     - Test-NewAzureStorageAccount, Test-GetAzureStorageAccount, and Test-RemoveAzureStorageAccount assert name/SKU/location/kind lifecycle behavior.
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
#   AZCLIPS_TENANT_ID            - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP       - resource group name; defaults to timestamped test group
#   AZCLIPS_STORAGE_ACCOUNT_NAME - storage account name; defaults to timestamped lowercase name
#   AZCLIPS_LOCATION             - Azure region; defaults to westus2
#   AZCLIPS_KEEP_STORAGE         - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG           - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/Storage/StorageAccountLifecycle/StorageAccountLifecycle.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Git Bash/MSYS rewrites slash-prefixed arguments for native Windows exes
# (for example /subscriptions/... -> C:/Program Files/Git/subscriptions/...).
# ARM resource IDs and REST paths must be passed through unchanged.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%y%m%d%H%M%S)"
RAND="$(printf '%02d' "$((RANDOM % 100))")"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-storage-it-$STAMP-$RAND}"
STORAGE_ACCOUNT_NAME="${AZCLIPS_STORAGE_ACCOUNT_NAME:-azclipssa$STAMP$RAND}"
STORAGE_ACCOUNT_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT_NAME"
SKU_NAME="Standard_LRS"
KIND="StorageV2"

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

assert_json_array_contains_name() {
  local json_payload="$1"
  local expected_name="$2"

  JSON_PAYLOAD="$json_payload" "$PYTHON_BIN" - "$expected_name" <<'PY'
import json
import os
import sys

data = json.loads(os.environ["JSON_PAYLOAD"])
expected_name = sys.argv[1]
if not isinstance(data, list):
    print("ASSERTION FAILED: expected a JSON array from list output.", file=sys.stderr)
    sys.exit(1)

for item in data:
    if isinstance(item, dict) and item.get("name") == expected_name:
        sys.exit(0)

print(f"ASSERTION FAILED: storage account '{expected_name}' was not present in list output.", file=sys.stderr)
sys.exit(1)
PY
}

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_STORAGE:-0}" != "1" ]]; then
    echo "=== [storage account delete] cleanup ==="
    "$AZ" storage account delete \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$STORAGE_ACCOUNT_NAME" || true
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

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI. Disable the agent so failures surface as non-zero exits.
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

echo "=== [storage account create] $STORAGE_ACCOUNT_NAME ==="
CREATE_OUTPUT="$("$AZ" storage account create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --location "$LOCATION" \
  --sku "$SKU_NAME" \
  --kind "$KIND")"

assert_json_equals "$CREATE_OUTPUT" "name" "$STORAGE_ACCOUNT_NAME"
assert_json_equals "$CREATE_OUTPUT" "location" "$LOCATION"
assert_json_equals "$CREATE_OUTPUT" "sku.name" "$SKU_NAME"
assert_json_equals "$CREATE_OUTPUT" "kind" "$KIND"

echo "=== [storage account show] $STORAGE_ACCOUNT_NAME ==="
SHOW_OUTPUT="$("$AZ" storage account show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME")"

assert_json_equals "$SHOW_OUTPUT" "id" "$STORAGE_ACCOUNT_ID"
assert_json_equals "$SHOW_OUTPUT" "name" "$STORAGE_ACCOUNT_NAME"
assert_json_equals "$SHOW_OUTPUT" "location" "$LOCATION"
assert_json_equals "$SHOW_OUTPUT" "sku.name" "$SKU_NAME"
assert_json_equals "$SHOW_OUTPUT" "kind" "$KIND"

echo "=== [storage account list] resource group ==="
LIST_OUTPUT="$("$AZ" storage account list \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP")"

assert_json_array_contains_name "$LIST_OUTPUT" "$STORAGE_ACCOUNT_NAME"

echo "=== [storage account list] subscription ==="
SUBSCRIPTION_LIST_OUTPUT="$("$AZ" storage account list \
  --subscription-id "$SUBSCRIPTION_ID")"

assert_json_array_contains_name "$SUBSCRIPTION_LIST_OUTPUT" "$STORAGE_ACCOUNT_NAME"

echo "=== [storage account delete] $STORAGE_ACCOUNT_NAME ==="
"$AZ" storage account delete \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME"

if SHOW_AFTER_DELETE_OUTPUT="$("$AZ" storage account show \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME" 2>&1)"; then
  echo "ASSERTION FAILED: storage account '$STORAGE_ACCOUNT_NAME' was still readable after delete." >&2
  echo "$SHOW_AFTER_DELETE_OUTPUT" >&2
  exit 1
fi

echo "All storage account lifecycle smoke checks completed."