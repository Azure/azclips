#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: az storage blob upload
# Prereqs: build, sign in, and create the test container.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

ACCOUNT="${AZCLIPS_STORAGE_ACCOUNT:-mytestsa52379}"
CONTAINER="${AZCLIPS_STORAGE_CONTAINER:-azclips-manual-test}"
SOURCE="$REPO_ROOT/tests/Scripts/CLI/StorageBlobUpload.sh"
BLOB="azclips-cli-upload-$(date +%s)-$$.sh"
URI="https://$ACCOUNT.blob.core.windows.net/$CONTAINER/$BLOB"
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

if duplicate_output="$("$AZ" storage blob upload \
  --account-name "$ACCOUNT" \
  --container-name "$CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" 2>&1)"; then
  echo "Expected duplicate upload without --overwrite to fail." >&2
  exit 1
fi
grep -qi overwrite <<<"$duplicate_output"

echo "Storage Blob CLI upload assertions passed."
