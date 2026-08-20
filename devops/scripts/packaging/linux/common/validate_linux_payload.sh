#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST_TOOL="$SCRIPT_DIR/create_payload_manifest.py"
PAYLOAD_DIR=""
MANIFEST_PATH=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --payload-dir) PAYLOAD_DIR="$2"; shift 2 ;;
    --manifest) MANIFEST_PATH="$2"; shift 2 ;;
    *)
      echo "Usage: $0 --payload-dir <path> --manifest <path>" >&2
      exit 2
      ;;
  esac
done

if [[ -z "$PAYLOAD_DIR" || -z "$MANIFEST_PATH" ]]; then
  echo "Usage: $0 --payload-dir <path> --manifest <path>" >&2
  exit 2
fi

for required_path in \
  "$PAYLOAD_DIR/az" \
  "$PAYLOAD_DIR/NOTICE" \
  "$PAYLOAD_DIR/LICENSE" \
  "$PAYLOAD_DIR/Extensions/Microsoft.Azure.Azclips.Extensions.Agent"; do
  if [[ ! -e "$required_path" ]]; then
    echo "ERROR: Required payload path is missing: $required_path" >&2
    exit 1
  fi
done

python3 "$MANIFEST_TOOL" verify \
  --payload-dir "$PAYLOAD_DIR" \
  --manifest "$MANIFEST_PATH"