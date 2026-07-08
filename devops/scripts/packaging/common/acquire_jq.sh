#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
CONFIG_PATH="${JQ_BINARIES_CONFIG:-$PACKAGING_ROOT/config/jq_binaries.config}"
RID="${RID:-}"
OUTPUT_DIR=""

usage() {
  cat <<'EOF'
Usage: acquire_jq.sh --rid <rid> --output-dir <path>

Downloads the pinned jq binary for the given RID, verifies its SHA-256, and
copies it into the output directory as ./jq.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)
      [[ $# -ge 2 ]] || { echo "ERROR: --rid requires a value" >&2; exit 1; }
      RID="$2"; shift 2 ;;
    --output-dir)
      [[ $# -ge 2 ]] || { echo "ERROR: --output-dir requires a value" >&2; exit 1; }
      OUTPUT_DIR="$2"; shift 2 ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

[[ -n "$RID" ]] || { echo "ERROR: --rid is required" >&2; exit 1; }
[[ -n "$OUTPUT_DIR" ]] || { echo "ERROR: --output-dir is required" >&2; exit 1; }
[[ -f "$CONFIG_PATH" ]] || { echo "ERROR: jq config not found: $CONFIG_PATH" >&2; exit 1; }

read_config() {
  local key="$1"
  awk -F= -v key="$key" '
    /^[[:space:]]*(#|$)/ { next }
    {
      k=$1
      sub(/^[[:space:]]+/, "", k)
      sub(/[[:space:]]+$/, "", k)
      if (k == key) {
        $1=""
        sub(/^=/, "")
        v=$0
        sub(/^[[:space:]]+/, "", v)
        sub(/[[:space:]]+$/, "", v)
        print v
        found=1
        exit
      }
    }
    END { if (!found) exit 1 }
  ' "$CONFIG_PATH"
}

hash_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{print $1}'
  else
    shasum -a 256 "$file" | awk '{print $1}'
  fi
}

command -v curl >/dev/null 2>&1 || { echo "ERROR: curl is required to acquire jq" >&2; exit 1; }

version="$(read_config version)"
asset="$(read_config "$RID.asset")"
expected_sha="$(read_config "$RID.sha256")"
url="https://github.com/jqlang/jq/releases/download/jq-$version/$asset"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT
download_path="$tmp_dir/$asset"

echo "Acquiring jq $version for $RID"
curl -fsSL "$url" -o "$download_path"

actual_sha="$(hash_file "$download_path" | tr '[:upper:]' '[:lower:]')"
expected_sha="$(printf '%s' "$expected_sha" | tr '[:upper:]' '[:lower:]')"
if [[ "$actual_sha" != "$expected_sha" ]]; then
  echo "ERROR: jq checksum mismatch for $asset" >&2
  echo "Expected: $expected_sha" >&2
  echo "Actual  : $actual_sha" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
cp "$download_path" "$OUTPUT_DIR/jq"
chmod +x "$OUTPUT_DIR/jq"
cat > "$OUTPUT_DIR/version.txt" <<EOF
version=$version
rid=$RID
asset=$asset
url=$url
sha256=$expected_sha
EOF

echo "Bundled jq at $OUTPUT_DIR/jq"