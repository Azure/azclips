#!/usr/bin/env bash
set -euo pipefail

# inject_builtin_extensions.sh
# Injects built-in extension publish outputs into a platform publish directory.
#
# Required inputs:
#   --publish-dir <path>          target publish output dir (e.g., out/publish/cli/linux-x64)
# Optional inputs:
#   --builtin-root <path>         root containing built-in publish folders (default: out/common/builtin-publish)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/common}"
BUILTIN_PUBLISH_ROOT="$OUT_ROOT/builtin-publish"
PUBLISH_DIR=""
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
LEAN_PACKAGE_CONFIG="$PACKAGING_ROOT/config/lean_package.config"
FULL_PACKAGE_CONFIG="$PACKAGING_ROOT/config/full_package.config"

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
  echo "Run ./devops/scripts/packaging/common/build_common.sh first."
  exit 1
fi

PACKAGE_PROFILE="$(echo "$PACKAGE_PROFILE" | tr '[:upper:]' '[:lower:]')"
case "$PACKAGE_PROFILE" in
  lean)
    PACKAGE_CONFIG="$LEAN_PACKAGE_CONFIG"
    ;;
  full)
    PACKAGE_CONFIG="$FULL_PACKAGE_CONFIG"
    ;;
  *)
    echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
    exit 1
    ;;
esac

if [[ ! -f "$PACKAGE_CONFIG" ]]; then
  echo "ERROR: package config not found at $PACKAGE_CONFIG"
  exit 1
fi

CONFIG_INCLUDE_ENTRIES=()
while IFS= read -r line; do
  line="${line%%#*}"
  line="$(echo "$line" | xargs)"
  [[ -z "$line" ]] && continue
  CONFIG_INCLUDE_ENTRIES+=("$line")
done < "$PACKAGE_CONFIG"

if [[ ${#CONFIG_INCLUDE_ENTRIES[@]} -eq 0 ]]; then
  echo "ERROR: package config '$PACKAGE_CONFIG' has no include entries"
  exit 1
fi

SELECTED_BUILTIN_DIRS=()
has_wildcard=0
for entry in "${CONFIG_INCLUDE_ENTRIES[@]}"; do
  if [[ "$entry" == "*" ]]; then
    has_wildcard=1
    break
  fi
done

if [[ $has_wildcard -eq 1 ]]; then
  while IFS= read -r dir_path; do
    [[ -d "$dir_path" ]] || continue
    SELECTED_BUILTIN_DIRS+=("$dir_path")
  done < <(find "$BUILTIN_PUBLISH_ROOT" -mindepth 1 -maxdepth 1 -type d | sort)
else
  for entry in "${CONFIG_INCLUDE_ENTRIES[@]}"; do
    ext_assembly_name="Microsoft.Azure.Azclips.Extensions.$entry"
    builtin_dir="$BUILTIN_PUBLISH_ROOT/$ext_assembly_name"
    if [[ ! -d "$builtin_dir" ]]; then
      echo "ERROR: Built-in publish directory not found for '$entry' at $builtin_dir"
      exit 1
    fi
    SELECTED_BUILTIN_DIRS+=("$builtin_dir")
  done
fi

echo "Inject profile: $PACKAGE_PROFILE"
echo "Inject config : $PACKAGE_CONFIG"

echo "Injecting built-in extensions into $PUBLISH_DIR"
for builtin_dir in "${SELECTED_BUILTIN_DIRS[@]}"; do
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

  # CLIPS-933: sidecar metadata (s_cliTable leaves/verbs) written by ExtensionSidecar.targets
  # next to the extension's own DLL; must travel into the packaged bundled-extension folder
  # the same way *.deps.json does.
  cp "$builtin_dir"/*.commands.json "$dest/" 2>/dev/null || true

  echo "  BUNDLE $ext_assembly_name -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
done
