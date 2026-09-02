#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: az storage blob list/show/exists
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
AZURE_CLI="${AZURE_CLI:-az}"

ACCOUNT="${AZCLIPS_STORAGE_ACCOUNT:-mytestsa52379}"
CONTAINER="${AZCLIPS_STORAGE_CONTAINER:-azclips-manual-test}"
SOURCE="$REPO_ROOT/tests/Scripts/CLI/StorageBlobGet.sh"
BLOB="azclips-cli-get-$(date +%s)-$$.sh"
SOURCE_LENGTH="$(wc -c < "$SOURCE" | tr -d '[:space:]')"

cleanup() {
  local exit_code=$?
  local remaining
  set +e
  "$AZURE_CLI" storage blob delete \
    --account-name "$ACCOUNT" \
    --container-name "$CONTAINER" \
    --name "$BLOB" \
    --auth-mode login \
    --only-show-errors >/dev/null
  local delete_code=$?
  remaining="$("$AZURE_CLI" storage blob exists \
    --account-name "$ACCOUNT" \
    --container-name "$CONTAINER" \
    --name "$BLOB" \
    --auth-mode login \
    --query exists \
    --output tsv \
    --only-show-errors 2>/dev/null)"
  local verify_code=$?
  if (( delete_code != 0 || verify_code != 0 )) || [[ "${remaining,,}" != "false" ]]; then
    echo "Failed to delete test blob '$BLOB'." >&2
    (( exit_code != 0 )) || exit_code=1
  fi
  exit "$exit_code"
}
trap cleanup EXIT

"$AZ" config set --key agent.enabled --value false >/dev/null

missing="$("$AZ" storage blob exists \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB")"
[[ "$(tr -d '[:space:]' <<<"$missing")" == "false" ]]

"$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" \
  --tags kind=get-test >/dev/null

listed="$("$AZ" storage blob list \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --prefix "$BLOB" \
  --include tags)"
grep -Eq "\"name\"[[:space:]]*:[[:space:]]*\"$BLOB\"" <<<"$listed"
grep -Eq '"kind"[[:space:]]*:[[:space:]]*"get-test"' <<<"$listed"

shown="$("$AZ" storage blob show \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB")"
grep -Eq "\"name\"[[:space:]]*:[[:space:]]*\"$BLOB\"" <<<"$shown"
grep -Eq "\"contentLength\"[[:space:]]*:[[:space:]]*$SOURCE_LENGTH([,}[:space:]]|$)" <<<"$shown"

exists="$("$AZ" storage blob exists \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB")"
[[ "$(tr -d '[:space:]' <<<"$exists")" == "true" ]]

echo "Storage Blob CLI get assertions passed."
