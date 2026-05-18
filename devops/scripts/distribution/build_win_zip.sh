#!/usr/bin/env bash
set -euo pipefail

# build_win_zip.sh
# Builds Windows self-contained CLI, injects built-ins, and packages zip.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$SCRIPT_DIR/out}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-win-x64}"

CLI_WIN_SC_DIR="$OUT_ROOT/publish/cli/$RID"
CLI_TAR_DIR="$OUT_ROOT/bin/Release"
CLI_WIN_ZIP_PATH="$CLI_TAR_DIR/azv2-$RID.zip"
BUILTIN_PUBLISH_ROOT="$OUT_ROOT/builtin-publish"

if [[ ! -d "$BUILTIN_PUBLISH_ROOT" ]]; then
  echo "ERROR: Built-in publish root not found at $BUILTIN_PUBLISH_ROOT"
  echo "Run ./devops/scripts/distribution/build_common.sh first."
  exit 1
fi

echo "Publishing CLI (Release, $RID)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$CLI_WIN_SC_DIR"

echo "Injecting built-in extensions into Windows publish output"
for builtin_dir in "$BUILTIN_PUBLISH_ROOT"/*; do
  [[ -d "$builtin_dir" ]] || continue

  ext_assembly_name=$(basename "$builtin_dir")
  dest="$CLI_WIN_SC_DIR/Extensions/$ext_assembly_name"
  mkdir -p "$dest"

  copied=0
  skipped=0
  for dll in "$builtin_dir"/*.dll; do
    [[ -f "$dll" ]] || continue
    dll_name=$(basename "$dll")
    host_dll="$CLI_WIN_SC_DIR/$dll_name"
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

if ! command -v zip >/dev/null 2>&1; then
  echo "ERROR: 'zip' command not found. Install zip to produce $CLI_WIN_ZIP_PATH"
  exit 1
fi

mkdir -p "$CLI_TAR_DIR"
rm -f "$CLI_WIN_ZIP_PATH"
(cd "$CLI_WIN_SC_DIR" && zip -rq "$CLI_WIN_ZIP_PATH" .)
echo "Created $CLI_WIN_ZIP_PATH"
