#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: az storage blob upload
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
AZURE_CLI="${AZURE_CLI:-az}"

ACCOUNT="${AZCLIPS_STORAGE_ACCOUNT:-mytestsa52379}"
CONTAINER="${AZCLIPS_STORAGE_CONTAINER:-azclips-manual-test}"
BLOB="azclips-cli-upload-$(date +%s)-$$.bin"
SOURCE="$REPO_ROOT/artifacts/$BLOB"
URI="https://$ACCOUNT.blob.core.windows.net/$CONTAINER/$BLOB"

cleanup() {
  local exit_code=$?
  local remaining
  set +e
  rm -f "$SOURCE"
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

upload="$("$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE")"

grep -Eq '"operation"[[:space:]]*:[[:space:]]*"upload"' <<<"$upload"
grep -Fq "$URI" <<<"$upload"
grep -Eq "\"contentLength\"[[:space:]]*:[[:space:]]*$SOURCE_LENGTH([,}[:space:]]|$)" <<<"$upload"
printf 'Upload result:\n%s\n' "$upload"

if duplicate_output="$("$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" 2>&1)"; then
  echo "Expected duplicate upload without --overwrite to fail." >&2
  exit 1
fi
if ! grep -Eqi 'BlobAlreadyExists|already exists' <<<"$duplicate_output"; then
  echo "Unexpected duplicate upload error: $duplicate_output" >&2
  exit 1
fi

printf '\x03' >> "$SOURCE"
SOURCE_LENGTH="$(wc -c < "$SOURCE" | tr -d '[:space:]')"
overwrite="$("$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" \
  --overwrite)"
grep -Eq "\"contentLength\"[[:space:]]*:[[:space:]]*$SOURCE_LENGTH([,}[:space:]]|$)" <<<"$overwrite"
printf 'Overwrite result:\n%s\n' "$overwrite"

echo "Storage Blob CLI upload assertions passed."
