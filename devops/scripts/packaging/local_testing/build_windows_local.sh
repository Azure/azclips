#!/usr/bin/env bash
set -euo pipefail

# build_windows_local.sh
# Local Windows-zip flow (from non-Windows hosts via PowerShell script validation):
#   1) Build common distribution assets
#   2) Build Windows zip
#   3) Validate Windows bundled + optional extension flow

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
PACKAGING_DIR="$REPO_ROOT/devops/scripts/packaging"

OUT_ROOT_BASE="${OUT_ROOT_BASE:-$REPO_ROOT/devops/out}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$OUT_ROOT_BASE/common}"
WINDOWS_OUT_ROOT="${WINDOWS_OUT_ROOT:-$OUT_ROOT_BASE/windows}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
PACKAGE_PROFILE="$(echo "$PACKAGE_PROFILE" | tr '[:upper:]' '[:lower:]')"

if [[ "$PACKAGE_PROFILE" == "full" ]]; then
  ARTIFACT_PREFIX="azv2"
elif [[ "$PACKAGE_PROFILE" == "lean" ]]; then
  ARTIFACT_PREFIX="azv2-lean"
else
  echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
  exit 1
fi

WINDOWS_ZIP_PATH="${WINDOWS_ZIP_PATH:-$WINDOWS_OUT_ROOT/Release/zip/$ARTIFACT_PREFIX-win-x64.zip}"

for required in common/build_common.sh windows/zip/build_win_zip.ps1 windows/zip/test_windows_zip_bundled_plus_ext.ps1 config/lean_package.config config/full_package.config; do
  if [[ ! -f "$PACKAGING_DIR/$required" ]]; then
    echo "ERROR: Missing required file: $PACKAGING_DIR/$required"
    exit 1
  fi
done

if ! command -v pwsh >/dev/null 2>&1; then
  echo "ERROR: pwsh is required to run Windows local validation script."
  exit 1
fi

cd "$REPO_ROOT"

echo "============================================"
echo " Local Windows Zip Distribution Validation"
echo "============================================"
echo "Repo root : $REPO_ROOT"
echo "Common out: $COMMON_OUT_ROOT"
echo "Windows out: $WINDOWS_OUT_ROOT"
echo "Windows zip path : $WINDOWS_ZIP_PATH"

echo ""
echo "[1/3] Build common distribution assets"
chmod +x "$PACKAGING_DIR/common/build_common.sh"
OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" "$PACKAGING_DIR/common/build_common.sh"

echo ""
echo "[2/3] Build Windows zip"
PACKAGE_PROFILE="$PACKAGE_PROFILE" pwsh -NoLogo -NoProfile -File "$PACKAGING_DIR/windows/zip/build_win_zip.ps1" -OutRoot "$WINDOWS_OUT_ROOT" -CommonOutRoot "$COMMON_OUT_ROOT" -CliWinZipPath "$WINDOWS_ZIP_PATH"

echo ""
echo "[3/3] Validate Windows bundled + optional extensions"
PACKAGE_PROFILE="$PACKAGE_PROFILE" pwsh -NoLogo -NoProfile -File "$PACKAGING_DIR/windows/zip/test_windows_zip_bundled_plus_ext.ps1" -OutRoot "$WINDOWS_OUT_ROOT" -CommonOutRoot "$COMMON_OUT_ROOT" -CliZipPath "$WINDOWS_ZIP_PATH"

echo ""
echo "Windows local distribution checks completed successfully."
