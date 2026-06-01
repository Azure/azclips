#!/usr/bin/env bash
set -euo pipefail

# build_macos_local.sh
# Local macOS-tarball flow:
#   1) Build common distribution assets
#   2) Build macOS tarball
#   3) Validate macOS bundled + optional extension flow
# Note: Runtime checks execute only on Darwin hosts; on other hosts package/layout checks run.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
PACKAGING_DIR="$REPO_ROOT/devops/scripts/packaging"

OUT_ROOT_BASE="${OUT_ROOT_BASE:-$REPO_ROOT/devops/out}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$OUT_ROOT_BASE/common}"
MACOS_OUT_ROOT="${MACOS_OUT_ROOT:-$OUT_ROOT_BASE/macos}"
MACOS_RID="${MACOS_RID:-osx-arm64}"
MACOS_TARBALL_PATH="${MACOS_TARBALL_PATH:-$MACOS_OUT_ROOT/Release/tarball/azv2-$MACOS_RID.tar.gz}"

for required in common/build_common.sh macos/tarball/build_macos_tarball.sh macos/tarball/test_macos_tarball_bundled_plus_ext.sh config/bundled_ext.config; do
  if [[ ! -f "$PACKAGING_DIR/$required" ]]; then
    echo "ERROR: Missing required file: $PACKAGING_DIR/$required"
    exit 1
  fi
done

cd "$REPO_ROOT"

echo "============================================"
echo " Local macOS Tarball Distribution Validation"
echo "============================================"
echo "Repo root : $REPO_ROOT"
echo "Common out: $COMMON_OUT_ROOT"
echo "macOS out : $MACOS_OUT_ROOT"
echo "macOS rid : $MACOS_RID"
echo "macOS tarball path: $MACOS_TARBALL_PATH"

echo ""
echo "[1/3] Build common distribution assets"
chmod +x "$PACKAGING_DIR/common/build_common.sh"
OUT_ROOT="$COMMON_OUT_ROOT" "$PACKAGING_DIR/common/build_common.sh"

echo ""
echo "[2/3] Build macOS tarball"
chmod +x "$PACKAGING_DIR/macos/tarball/build_macos_tarball.sh"
OUT_ROOT="$MACOS_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" RID="$MACOS_RID" CLI_TAR_PATH="$MACOS_TARBALL_PATH" "$PACKAGING_DIR/macos/tarball/build_macos_tarball.sh"

echo ""
echo "[3/3] Validate macOS bundled + optional extensions"
chmod +x "$PACKAGING_DIR/macos/tarball/test_macos_tarball_bundled_plus_ext.sh"
OUT_ROOT="$MACOS_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" RID="$MACOS_RID" CLI_TAR_PATH="$MACOS_TARBALL_PATH" "$PACKAGING_DIR/macos/tarball/test_macos_tarball_bundled_plus_ext.sh"

echo ""
echo "macOS local distribution checks completed successfully."
