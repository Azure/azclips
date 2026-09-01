#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: az storage blob download
# Prereqs: build, sign in, and create the test container.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

ACCOUNT="${AZCLIPS_STORAGE_ACCOUNT:-mytestsa52379}"
CONTAINER="${AZCLIPS_STORAGE_CONTAINER:-azclips-manual-test}"
SOURCE="$REPO_ROOT/tests/Scripts/CLI/StorageBlobDownload.sh"
DESTINATION="$REPO_ROOT/artifacts/StorageBlobDownload.downloaded.sh"
BLOB="azclips-cli-download-$(date +%s)-$$.sh"
URI="https://$ACCOUNT.blob.core.windows.net/$CONTAINER/$BLOB"
SOURCE_LENGTH="$(wc -c < "$SOURCE" | tr -d '[:space:]')"

trap 'rm -f "$DESTINATION"' EXIT
rm -f "$DESTINATION"

"$AZ" config set --key agent.enabled --value false >/dev/null

# Arrange an independently downloadable blob.
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

if existing_output="$("$AZ" storage blob download \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION" 2>&1)"; then
  echo "Expected download onto an existing file without --overwrite to fail." >&2
  exit 1
fi
grep -qi overwrite <<<"$existing_output"

echo "Storage Blob CLI download assertions passed."
