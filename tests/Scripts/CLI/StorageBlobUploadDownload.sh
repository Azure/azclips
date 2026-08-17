#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_STORAGE_ACCOUNT:?Set AZCLIPS_STORAGE_ACCOUNT}"
: "${AZCLIPS_STORAGE_CONTAINER:?Set AZCLIPS_STORAGE_CONTAINER}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SOURCE="$(mktemp)"
DESTINATION="$(mktemp)"
ERROR_OUTPUT="$(mktemp)"
trap 'rm -f "$SOURCE" "$DESTINATION" "$ERROR_OUTPUT"' EXIT

BLOB="azclips-ramp-up-$(date +%s).bin"
printf '\x00hello from azclips\xff' >"$SOURCE"
rm -f "$DESTINATION"

assert_contains() {
  local output="$1"
  local expected="$2"
  if ! grep -Fq "$expected" <<<"$output"; then
    echo "Expected output to contain: $expected" >&2
    echo "$output" >&2
    exit 1
  fi
}

UPLOAD_OUTPUT="$("$AZ" storage blob upload \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE")"
echo "$UPLOAD_OUTPUT"

assert_contains "$UPLOAD_OUTPUT" '"operation": "upload"'
assert_contains "$UPLOAD_OUTPUT" "\"source\":"
assert_contains "$UPLOAD_OUTPUT" "$(basename "$SOURCE")"
assert_contains "$UPLOAD_OUTPUT" "\"destination\": \"https://$AZCLIPS_STORAGE_ACCOUNT.blob.core.windows.net/$AZCLIPS_STORAGE_CONTAINER/$BLOB\""
assert_contains "$UPLOAD_OUTPUT" "\"contentLength\": $(wc -c <"$SOURCE" | tr -d ' ')"
assert_contains "$UPLOAD_OUTPUT" '"eTag":'

if AZCLIPS_IN_AGENT_MODE=1 "$AZ" storage blob upload \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE" >"$ERROR_OUTPUT" 2>&1; then
  echo "Upload unexpectedly overwrote an existing blob without --overwrite." >&2
  exit 1
fi
grep -Fq -- '--overwrite' "$ERROR_OUTPUT"

DOWNLOAD_OUTPUT="$("$AZ" storage blob download \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION")"
echo "$DOWNLOAD_OUTPUT"

assert_contains "$DOWNLOAD_OUTPUT" '"operation": "download"'
assert_contains "$DOWNLOAD_OUTPUT" "\"source\": \"https://$AZCLIPS_STORAGE_ACCOUNT.blob.core.windows.net/$AZCLIPS_STORAGE_CONTAINER/$BLOB\""
assert_contains "$DOWNLOAD_OUTPUT" '"destination":'
assert_contains "$DOWNLOAD_OUTPUT" "$(basename "$DESTINATION")"
assert_contains "$DOWNLOAD_OUTPUT" "\"contentLength\": $(wc -c <"$SOURCE" | tr -d ' ')"
assert_contains "$DOWNLOAD_OUTPUT" '"eTag":'

if ! cmp -s "$SOURCE" "$DESTINATION"; then
  echo "Downloaded bytes do not match uploaded bytes." >&2
  exit 1
fi

if AZCLIPS_IN_AGENT_MODE=1 "$AZ" storage blob download \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION" >"$ERROR_OUTPUT" 2>&1; then
  echo "Download unexpectedly overwrote an existing file without --overwrite." >&2
  exit 1
fi
grep -Fq -- '--overwrite' "$ERROR_OUTPUT"

echo "Blob upload/download verification passed: $BLOB"
