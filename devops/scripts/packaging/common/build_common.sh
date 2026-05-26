#!/usr/bin/env bash
set -euo pipefail

# build_common.sh
# Builds common distribution assets shared by all platform-specific build scripts.
#
# Outputs:
#   out/extensions-pack/                 Extension .nupkg files
#   out/builtin-publish/<AssemblyName>/  Built-in extension publish outputs
#   out/publish/cli/framework-dep/       Framework-dependent CLI publish

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/common}"
BUNDLED_EXT_CONFIG="$PACKAGING_ROOT/config/bundled_ext.config"
CLI_PROJECT="src/CLI/Core.Cli.csproj"

EXT_PACK_DIR="$OUT_ROOT/extensions-pack"
BUILTIN_PUBLISH_ROOT="$OUT_ROOT/builtin-publish"
CLI_FD_DIR="$OUT_ROOT/publish/cli/framework-dep"

if [[ ! -f "$BUNDLED_EXT_CONFIG" ]]; then
  echo "ERROR: bundled extension config not found at $BUNDLED_EXT_CONFIG"
  exit 1
fi

BUILTIN_EXTENSIONS=()
while IFS= read -r line; do
  line="${line%%#*}"
  line="$(echo "$line" | xargs)"
  [[ -z "$line" ]] && continue

  ext_project="src/Extensions/$line/Extensions.$line.csproj"
  if [[ ! -f "$ext_project" ]]; then
    echo "ERROR: Built-in extension project not found for '$line' at $ext_project"
    exit 1
  fi

  BUILTIN_EXTENSIONS+=("$ext_project")
done < "$BUNDLED_EXT_CONFIG"

mapfile -t DISCOVERED_EXTENSIONS < <(find src/Extensions -mindepth 2 -maxdepth 2 -name "Extensions.*.csproj" | sort)
if [[ ${#DISCOVERED_EXTENSIONS[@]} -eq 0 ]]; then
  echo "ERROR: No extension projects found under src/Extensions"
  exit 1
fi

OPTIONAL_EXTENSIONS=()
for ext in "${DISCOVERED_EXTENSIONS[@]}"; do
  is_builtin=0
  for builtin in "${BUILTIN_EXTENSIONS[@]}"; do
    if [[ "$ext" == "$builtin" ]]; then
      is_builtin=1
      break
    fi
  done
  if [[ $is_builtin -eq 0 ]]; then
    OPTIONAL_EXTENSIONS+=("$ext")
  fi
done

ALL_EXTENSIONS=("${BUILTIN_EXTENSIONS[@]}" "${OPTIONAL_EXTENSIONS[@]}")

echo "[common 1/5] Clean shared output directories"
rm -rf "$EXT_PACK_DIR" "$BUILTIN_PUBLISH_ROOT" "$CLI_FD_DIR"
mkdir -p "$EXT_PACK_DIR" "$BUILTIN_PUBLISH_ROOT"

echo "[common 2/5] Restore solution"
dotnet restore cirrus.slnx

echo "[common 3/5] Publish CLI (framework-dependent)"
dotnet publish "$CLI_PROJECT" -c Release --no-restore -o "$CLI_FD_DIR"

echo "[common 4/5] Build + pack extensions"
for ext in "${ALL_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  echo "  BUILD $ext_name"
  dotnet build "$ext" -c Release --no-restore
  echo "  PACK  $ext_name"
  dotnet pack "$ext" -c Release -o "$EXT_PACK_DIR" --no-build --no-restore
done

echo "[common 5/5] Publish built-in extensions for platform inject step"
for ext in "${BUILTIN_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  ext_assembly_name=$(grep -oP '(?<=<AssemblyName>).*(?=</AssemblyName>)' "$ext" 2>/dev/null || echo "$ext_name")
  out_dir="$BUILTIN_PUBLISH_ROOT/$ext_assembly_name"

  dotnet publish "$ext" -c Release --self-contained false -o "$out_dir" --no-restore -v quiet
  echo "  BUILTIN $ext_name -> $out_dir"
done

echo "Shared outputs ready:"
echo "  Extension packages: $EXT_PACK_DIR"
echo "  Built-in publish root: $BUILTIN_PUBLISH_ROOT"
echo "  Framework-dependent CLI: $CLI_FD_DIR"
