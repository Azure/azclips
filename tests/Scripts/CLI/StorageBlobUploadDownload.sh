#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_STORAGE_ACCOUNT:?Set AZCLIPS_STORAGE_ACCOUNT}"
: "${AZCLIPS_STORAGE_CONTAINER:?Set AZCLIPS_STORAGE_CONTAINER}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SOURCE="$(mktemp)"
DESTINATION="$(mktemp)"
trap 'rm -f "$SOURCE" "$DESTINATION"' EXIT

BLOB="azclips-ramp-up-$(date +%s).bin"
printf '\x00hello from azclips\xff' >"$SOURCE"
rm -f "$DESTINATION"

"$AZ" storage blob upload \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$SOURCE"

"$AZ" storage blob download \
  --account-name "$AZCLIPS_STORAGE_ACCOUNT" \
  --container-name "$AZCLIPS_STORAGE_CONTAINER" \
  --name "$BLOB" \
  --file "$DESTINATION"

cmp "$SOURCE" "$DESTINATION"
echo "Blob upload/download verification passed: $BLOB"
