#!/usr/bin/env bash
set -euo pipefail

# inject_builtin_extensions.sh
# Injects built-in extension publish outputs into a platform publish directory.
#
# Required inputs:
#   --publish-dir <path>          target publish output dir (e.g., out/publish/cli/linux-x64)
# Optional inputs:
#   --builtin-root <path>         root containing built-in publish folders (default: out/builtin-publish)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$SCRIPT_DIR/out}"
BUILTIN_PUBLISH_ROOT="$OUT_ROOT/builtin-publish"
PUBLISH_DIR=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-dir)
      PUBLISH_DIR="$2"
      shift 2
      ;;
    --builtin-root)
      BUILTIN_PUBLISH_ROOT="$2"
      shift 2
      ;;
    *)
      echo "ERROR: Unknown argument: $1"
      echo "Usage: $0 --publish-dir <path> [--builtin-root <path>]"
      exit 1
      ;;
  esac
done

if [[ -z "$PUBLISH_DIR" ]]; then
  echo "ERROR: --publish-dir is required"
  exit 1
fi

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "ERROR: Publish directory not found at $PUBLISH_DIR"
  exit 1
fi

if [[ ! -d "$BUILTIN_PUBLISH_ROOT" ]]; then
  echo "ERROR: Built-in publish root not found at $BUILTIN_PUBLISH_ROOT"
  echo "Run ./devops/scripts/distribution/build_common.sh first."
  exit 1
fi

echo "Injecting built-in extensions into $PUBLISH_DIR"
for builtin_dir in "$BUILTIN_PUBLISH_ROOT"/*; do
  [[ -d "$builtin_dir" ]] || continue

  ext_assembly_name=$(basename "$builtin_dir")
  dest="$PUBLISH_DIR/Extensions/$ext_assembly_name"
  mkdir -p "$dest"

  copied=0
  skipped=0
  for dll in "$builtin_dir"/*.dll; do
    [[ -f "$dll" ]] || continue

    dll_name=$(basename "$dll")
    host_dll="$PUBLISH_DIR/$dll_name"
    if [[ -f "$host_dll" ]] && cmp -s "$dll" "$host_dll"; then
      skipped=$((skipped + 1))
    else
      cp "$dll" "$dest/"
      copied=$((copied + 1))
    fi
  done

  cp "$builtin_dir"/*.deps.json "$dest/" 2>/dev/null || true
  echo "  BUNDLE $ext_assembly_name -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
done
