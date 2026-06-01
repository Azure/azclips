#!/usr/bin/env bash
set -euo pipefail

# build_linux_local.sh
# Local Linux flow:
#   1) Build common distribution assets
#   2) Build Linux tarball
#   3) Validate Linux bundled + optional extension flow

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
PACKAGING_DIR="$REPO_ROOT/devops/scripts/packaging"

OUT_ROOT_BASE="${OUT_ROOT_BASE:-$REPO_ROOT/devops/out}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$OUT_ROOT_BASE/common}"
LINUX_OUT_ROOT="${LINUX_OUT_ROOT:-$OUT_ROOT_BASE/linux}"
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

for required in common/build_common.sh linux/tarball/build_linux_tarball.sh linux/tarball/test_linux_tarball_bundled_plus_ext.sh config/lean_package.config config/full_package.config; do
  if [[ ! -f "$PACKAGING_DIR/$required" ]]; then
    echo "ERROR: Missing required file: $PACKAGING_DIR/$required"
    exit 1
  fi
done

cd "$REPO_ROOT"

echo "============================================"
echo " Local Linux Distribution Validation"
echo "============================================"
echo "Repo root : $REPO_ROOT"
echo "Common out: $COMMON_OUT_ROOT"
echo "Linux out : $LINUX_OUT_ROOT"
echo "Linux tarball path: $LINUX_TARBALL_PATH"

echo ""
echo "[1/3] Build common distribution assets"
chmod +x "$PACKAGING_DIR/common/build_common.sh"
OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" "$PACKAGING_DIR/common/build_common.sh"

echo ""
echo "[2/3] Build Linux tarball"
chmod +x "$PACKAGING_DIR/linux/tarball/build_linux_tarball.sh"
OUT_ROOT="$LINUX_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" CLI_TAR_PATH="$LINUX_TARBALL_PATH" "$PACKAGING_DIR/linux/tarball/build_linux_tarball.sh"

echo ""
echo "[3/3] Validate Linux bundled + optional extensions"
chmod +x "$PACKAGING_DIR/linux/tarball/test_linux_tarball_bundled_plus_ext.sh"
OUT_ROOT="$LINUX_OUT_ROOT" COMMON_OUT_ROOT="$COMMON_OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" CLI_TAR_PATH="$LINUX_TARBALL_PATH" "$PACKAGING_DIR/linux/tarball/test_linux_tarball_bundled_plus_ext.sh"

echo ""
echo "Linux local distribution checks completed successfully."
