#!/usr/bin/env bash
set -euo pipefail

# build_distribution_e2e.sh
# CI entrypoint that runs distribution build and validation end-to-end.
#
# Default flow:
#   1) Build common distribution assets
#   2) Build Linux distribution artifact
#   3) Run Linux bundled + optional extension validation
#
# Optional flow:
#   4) Build Windows distribution artifact and run PowerShell validation script
#   5) Build macOS distribution artifact and run validation script
#
# Examples:
#   ./devops/scripts/packaging/local_testing/build_distribution_e2e.sh
#   ./devops/scripts/packaging/local_testing/build_distribution_e2e.sh --with-pwsh
#   ./devops/scripts/packaging/local_testing/build_distribution_e2e.sh --with-macos

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
PACKAGING_DIR="$REPO_ROOT/devops/scripts/packaging"
OUT_ROOT_BASE="${OUT_ROOT_BASE:-$REPO_ROOT/devops/out}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$OUT_ROOT_BASE/common}"
LINUX_OUT_ROOT="${LINUX_OUT_ROOT:-$OUT_ROOT_BASE/linux}"
WINDOWS_OUT_ROOT="${WINDOWS_OUT_ROOT:-$OUT_ROOT_BASE/windows}"
MACOS_OUT_ROOT="${MACOS_OUT_ROOT:-$OUT_ROOT_BASE/macos}"
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

LINUX_TARBALL_PATH="${LINUX_TARBALL_PATH:-$LINUX_OUT_ROOT/Release/tarball/$ARTIFACT_PREFIX-linux-x64.tar.gz}"
WINDOWS_ZIP_PATH="${WINDOWS_ZIP_PATH:-$WINDOWS_OUT_ROOT/Release/zip/$ARTIFACT_PREFIX-win-x64.zip}"
MACOS_RID="${MACOS_RID:-osx-arm64}"
MACOS_TARBALL_PATH="${MACOS_TARBALL_PATH:-$MACOS_OUT_ROOT/Release/tarball/$ARTIFACT_PREFIX-$MACOS_RID.tar.gz}"
RUN_PWSH=0
RUN_MACOS=0

for arg in "$@"; do
  case "$arg" in
    --with-pwsh)
      RUN_PWSH=1
      ;;
    --with-macos)
      RUN_MACOS=1
      ;;
    *)
      echo "ERROR: Unknown argument: $arg"
      echo "Usage: $0 [--with-pwsh] [--with-macos]"
      exit 1
      ;;
  esac
done

for required in common/build_common.sh linux/tarball/build_linux_tarball.sh windows/zip/build_win_zip.sh macos/tarball/build_macos_tarball.sh linux/tarball/test_linux_tarball_bundled_plus_ext.sh windows/zip/test_windows_zip_bundled_plus_ext.ps1 macos/tarball/test_macos_tarball_bundled_plus_ext.sh config/lean_package.config config/full_package.config; do
  if [[ ! -f "$PACKAGING_DIR/$required" ]]; then
    echo "ERROR: Missing required file: $PACKAGING_DIR/$required"
    exit 1
  fi
done

cd "$REPO_ROOT"

echo "============================================"
echo " Distribution Build + E2E Validation"
echo "============================================"
echo "Repo root : $REPO_ROOT"
echo "Common out: $COMMON_OUT_ROOT"
echo "Linux out : $LINUX_OUT_ROOT"
echo "Windows out: $WINDOWS_OUT_ROOT"
echo "macOS out : $MACOS_OUT_ROOT"
echo "Linux tarball path: $LINUX_TARBALL_PATH"
echo "Windows zip path : $WINDOWS_ZIP_PATH"
echo "macOS tarball path: $MACOS_TARBALL_PATH"

echo ""
echo "[1/4] Build common distribution assets"
chmod +x "$PACKAGING_DIR/common/build_common.sh"
OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" "$PACKAGING_DIR/common/build_common.sh"

echo ""
echo "[2/4] Build Linux distribution artifact"
chmod +x "$PACKAGING_DIR/linux/tarball/build_linux_tarball.sh"
OUT_ROOT="$LINUX_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" CLI_TAR_PATH="$LINUX_TARBALL_PATH" "$PACKAGING_DIR/linux/tarball/build_linux_tarball.sh"

echo ""
echo "[3/4] Validate Linux bundled + optional extensions"
chmod +x "$PACKAGING_DIR/linux/tarball/test_linux_tarball_bundled_plus_ext.sh"
OUT_ROOT="$LINUX_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" CLI_TAR_PATH="$LINUX_TARBALL_PATH" "$PACKAGING_DIR/linux/tarball/test_linux_tarball_bundled_plus_ext.sh"

if [[ $RUN_PWSH -eq 1 ]]; then
  echo ""
  echo "[4/4] Build Windows distribution artifact + validate via PowerShell"
  if ! command -v pwsh >/dev/null 2>&1; then
    echo "ERROR: --with-pwsh requested, but pwsh is not installed."
    exit 1
  fi

  chmod +x "$PACKAGING_DIR/windows/zip/build_win_zip.sh"
  OUT_ROOT="$WINDOWS_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" CLI_WIN_ZIP_PATH="$WINDOWS_ZIP_PATH" "$PACKAGING_DIR/windows/zip/build_win_zip.sh"

  PACKAGE_PROFILE="$PACKAGE_PROFILE" pwsh -NoLogo -NoProfile -File "$PACKAGING_DIR/windows/zip/test_windows_zip_bundled_plus_ext.ps1" -OutRoot "$WINDOWS_OUT_ROOT" -CommonOutRoot "$COMMON_OUT_ROOT" -CliZipPath "$WINDOWS_ZIP_PATH"
else
  echo ""
  echo "[4/4] Skipping PowerShell validation (use --with-pwsh to enable)"
fi

if [[ $RUN_MACOS -eq 1 ]]; then
  echo ""
  echo "[5/5] Build macOS distribution artifact + validate"

  chmod +x "$PACKAGING_DIR/macos/tarball/build_macos_tarball.sh"
  OUT_ROOT="$MACOS_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" RID="$MACOS_RID" CLI_TAR_PATH="$MACOS_TARBALL_PATH" "$PACKAGING_DIR/macos/tarball/build_macos_tarball.sh"

  chmod +x "$PACKAGING_DIR/macos/tarball/test_macos_tarball_bundled_plus_ext.sh"
  OUT_ROOT="$MACOS_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" RID="$MACOS_RID" CLI_TAR_PATH="$MACOS_TARBALL_PATH" "$PACKAGING_DIR/macos/tarball/test_macos_tarball_bundled_plus_ext.sh"
else
  echo ""
  echo "[5/5] Skipping macOS validation (use --with-macos to enable)"
fi

echo ""
echo "All selected distribution checks completed successfully."
