#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: az storage blob download
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
AZURE_CLI="${AZURE_CLI:-az}"

ACCOUNT="${AZCLIPS_STORAGE_ACCOUNT:-mytestsa52379}"
CONTAINER="${AZCLIPS_STORAGE_CONTAINER:-azclips-manual-test}"
BLOB="azclips-cli-download-$(date +%s)-$$.bin"
SOURCE="$REPO_ROOT/artifacts/$BLOB"
DESTINATION="$SOURCE.downloaded"
URI="https://$ACCOUNT.blob.core.windows.net/$CONTAINER/$BLOB"

cleanup() {
  local exit_code=$?
  local remaining
  set +e
  rm -f "$SOURCE" "$DESTINATION"
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

printf '\x00\x01\x02\x7f\x80\xffAzclips\r\n' > "$SOURCE"
SOURCE_LENGTH="$(wc -c < "$SOURCE" | tr -d '[:space:]')"

"$AZ" config set --key agent.enabled --value false >/dev/null

"$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" >/dev/null

download="$("$AZ" storage blob download \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION")"

grep -Eq '"operation"[[:space:]]*:[[:space:]]*"download"' <<<"$download"
grep -Fq "$URI" <<<"$download"
grep -Eq "\"contentLength\"[[:space:]]*:[[:space:]]*$SOURCE_LENGTH([,}[:space:]]|$)" <<<"$download"
cmp "$SOURCE" "$DESTINATION"
printf 'Download result:\n%s\n' "$download"

if existing_output="$("$AZ" storage blob download \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION" 2>&1)"; then
  echo "Expected download onto an existing file without --overwrite to fail." >&2
  exit 1
fi
grep -qi overwrite <<<"$existing_output"

printf 'wrong content' > "$DESTINATION"
overwrite="$("$AZ" storage blob download \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION" \
  --overwrite)"
cmp "$SOURCE" "$DESTINATION"
printf 'Overwrite result:\n%s\n' "$overwrite"

echo "Storage Blob CLI download assertions passed."
