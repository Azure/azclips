#!/usr/bin/env bash
set -euo pipefail

# build_all.sh
# Master build script: produces all distribution artifacts.
#
# Outputs:
#   out/publish/cli/linux-x64/          CLI self-contained publish
#   out/publish/cli/win-x64/            CLI self-contained publish (Windows)
#   out/publish/cli/framework-dep/      CLI framework-dependent publish
#   out/bin/Release/azv2-linux-x64.tar.gz   CLI tarball (self-contained)
#   out/bin/Release/azv2-win-x64.zip    CLI zip (self-contained, Windows)
#   out/extensions-pack/                Extension .nupkg files
#   out/bundled/                        CLI + built-in extensions (same content as tarball)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$SCRIPT_DIR/out}"
BUNDLED_EXT_CONFIG="$SCRIPT_DIR/bundled_ext.config"

BUILTIN_EXTENSIONS=()
if [[ ! -f "$BUNDLED_EXT_CONFIG" ]]; then
  echo "ERROR: bundled extension config not found at $BUNDLED_EXT_CONFIG"
  exit 1
fi

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

CLI_PROJECT="src/CLI/Core.Cli.csproj"

CLI_SC_DIR="$OUT_ROOT/publish/cli/linux-x64"
CLI_WIN_SC_DIR="$OUT_ROOT/publish/cli/win-x64"
CLI_FD_DIR="$OUT_ROOT/publish/cli/framework-dep"
CLI_TAR_DIR="$OUT_ROOT/bin/Release"
CLI_TAR_PATH="$CLI_TAR_DIR/azv2-linux-x64.tar.gz"
CLI_WIN_ZIP_PATH="$CLI_TAR_DIR/azv2-win-x64.zip"
EXT_PACK_DIR="$OUT_ROOT/extensions-pack"
BUNDLED_DIR="$OUT_ROOT/bundled"

echo "============================================"
echo " Build All Distribution Artifacts"
echo "============================================"

echo "Bundled extensions from config:"
for ext in "${BUILTIN_EXTENSIONS[@]}"; do
  echo "  - $ext"
done

echo "[1/10] Cleaning output directories"
rm -rf "$CLI_SC_DIR" "$CLI_WIN_SC_DIR" "$CLI_FD_DIR" "$EXT_PACK_DIR" "$BUNDLED_DIR"
mkdir -p "$CLI_TAR_DIR" "$EXT_PACK_DIR" "$BUNDLED_DIR"

echo "[2/10] Restoring solution"
dotnet restore cirrus.slnx

echo "[3/10] Publishing CLI (self-contained, linux-x64)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$CLI_SC_DIR"

echo "[4/10] Publishing CLI (self-contained, win-x64)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o "$CLI_WIN_SC_DIR"

echo "[5/10] Publishing CLI (framework-dependent)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -o "$CLI_FD_DIR"

echo "[6/10] Building extensions (Release)"
for ext in "${ALL_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  echo "  BUILD $ext_name"
  dotnet build "$ext" -c Release --no-restore
done

echo "[7/10] Packing extensions as NuGet packages"
for ext in "${ALL_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  echo "  PACK $ext_name"
  dotnet pack "$ext" -c Release -o "$EXT_PACK_DIR" --no-build --no-restore
done

echo "[8/10] Injecting built-in extensions into self-contained CLI outputs"
for ext in "${BUILTIN_EXTENSIONS[@]}"; do
  ext_name=$(basename "$(dirname "$ext")")
  ext_assembly_name=$(grep -oP '(?<=<AssemblyName>).*(?=</AssemblyName>)' "$ext" 2>/dev/null || echo "$ext_name")

  tmp_publish="$OUT_ROOT/tmp-publish/$ext_name"
  dotnet publish "$ext" -c Release --self-contained false -o "$tmp_publish" --no-restore -v quiet

  for target_dir in "$CLI_SC_DIR" "$CLI_WIN_SC_DIR"; do
    dest="$target_dir/Extensions/$ext_assembly_name"
    mkdir -p "$dest"

    copied=0
    skipped=0
    for dll in "$tmp_publish"/*.dll; do
      dll_name=$(basename "$dll")
      host_dll="$target_dir/$dll_name"
      if [[ -f "$host_dll" ]] && cmp -s "$dll" "$host_dll"; then
        skipped=$((skipped + 1))
      else
        cp "$dll" "$dest/"
        copied=$((copied + 1))
      fi
    done

    cp "$tmp_publish"/*.deps.json "$dest/" 2>/dev/null || true

    echo "  BUNDLE $ext_name -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
  done
done

rm -rf "$OUT_ROOT/tmp-publish"

echo "[9/10] Creating CLI tar.gz (linux-x64)"
rm -f "$CLI_TAR_PATH"
tar -czf "$CLI_TAR_PATH" -C "$CLI_SC_DIR" .

echo "[10/10] Creating CLI zip (win-x64) and bundled copy"
if ! command -v zip >/dev/null 2>&1; then
  echo "ERROR: 'zip' command not found. Install zip to produce $CLI_WIN_ZIP_PATH"
  exit 1
fi
rm -f "$CLI_WIN_ZIP_PATH"
(cd "$CLI_WIN_SC_DIR" && zip -rq "$CLI_WIN_ZIP_PATH" .)

rm -rf "$BUNDLED_DIR"
mkdir -p "$BUNDLED_DIR"
cp -a "$CLI_SC_DIR/." "$BUNDLED_DIR/"

echo ""
echo "============================================"
echo " Build Complete"
echo "============================================"
echo ""
echo "Artifacts:"
echo "  CLI (self-contained):     $CLI_SC_DIR"
echo "  CLI (self-contained win): $CLI_WIN_SC_DIR"
echo "  CLI (framework-dep):      $CLI_FD_DIR"
echo "  CLI tarball:              $CLI_TAR_PATH"
du -sh "$CLI_TAR_PATH" 2>/dev/null || true
echo "  CLI zip (win-x64):        $CLI_WIN_ZIP_PATH"
du -sh "$CLI_WIN_ZIP_PATH" 2>/dev/null || true
echo "  Extension .nupkg files:   $EXT_PACK_DIR"
ls -1 "$EXT_PACK_DIR"/*.nupkg 2>/dev/null || echo "  (none)"
echo "  Bundled (CLI + built-in): $BUNDLED_DIR"
echo ""
echo "Next steps:"
echo "  Test Linux bundled + add optional: ./devops/scripts/distribution/test_bundled_plus_ext_linux.sh"
echo "  Test Win via PowerShell: pwsh ./devops/scripts/distribution/test_bundled_plus_ext_win.ps1"
