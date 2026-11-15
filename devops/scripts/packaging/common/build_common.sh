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
SOLUTION_FILE="${SOLUTION_FILE:-azclips.slnx}"
CLI_PROJECT="src/CLI/Core.Cli.csproj"

EXT_PACK_DIR="$OUT_ROOT/extensions-pack"
BUILTIN_PUBLISH_ROOT="$OUT_ROOT/builtin-publish"
CLI_FD_DIR="$OUT_ROOT/publish/cli/framework-dep"

mapfile -t DISCOVERED_EXTENSIONS < <(find src/Extensions -mindepth 2 -maxdepth 2 -name "Extensions.*.csproj" | sort)
if [[ ${#DISCOVERED_EXTENSIONS[@]} -eq 0 ]]; then
  echo "ERROR: No extension projects found under src/Extensions"
  exit 1
fi

echo "Package profile: all (profile selection happens during inject)"

echo "[common 1/5] Clean shared output directories"
rm -rf "$EXT_PACK_DIR" "$BUILTIN_PUBLISH_ROOT" "$CLI_FD_DIR"
mkdir -p "$EXT_PACK_DIR" "$BUILTIN_PUBLISH_ROOT"

echo "[common 2/5] Restore solution"
dotnet restore "$SOLUTION_FILE"

echo "[common 3/5] Publish CLI (framework-dependent)"
dotnet publish "$CLI_PROJECT" -c Release --no-restore -o "$CLI_FD_DIR"

echo "[common 4/5] Build + pack extensions"
for ext in "${DISCOVERED_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  echo "  BUILD $ext_name"
  dotnet build "$ext" -c Release --no-restore
  echo "  PACK  $ext_name"
  dotnet pack "$ext" -c Release -o "$EXT_PACK_DIR" --no-build --no-restore
done
bash "$SCRIPT_DIR/rename_extension_packages.sh" "$EXT_PACK_DIR"

echo "[common 5/5] Publish extension assemblies for platform inject step"
for ext in "${DISCOVERED_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  ext_assembly_name=$(sed -n 's:.*<AssemblyName>\(.*\)</AssemblyName>.*:\1:p' "$ext" | sed -n '1p')
  ext_assembly_name="${ext_assembly_name:-$ext_name}"
  out_dir="$BUILTIN_PUBLISH_ROOT/$ext_assembly_name"

  dotnet publish "$ext" -c Release --self-contained false -o "$out_dir" --no-build --no-restore -v quiet
  echo "  PUBLISH $ext_name -> $out_dir"
done

echo "Shared outputs ready:"
echo "  Extension packages: $EXT_PACK_DIR"
echo "  Built-in publish root: $BUILTIN_PUBLISH_ROOT"
echo "  Framework-dependent CLI: $CLI_FD_DIR"
