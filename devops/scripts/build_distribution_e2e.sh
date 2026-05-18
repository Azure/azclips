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
#
# Examples:
#   ./devops/scripts/build_distribution_e2e.sh
#   ./devops/scripts/build_distribution_e2e.sh --with-pwsh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DIST_DIR="$REPO_ROOT/devops/scripts/distribution"
OUT_ROOT="${OUT_ROOT:-$DIST_DIR/out}"
RUN_PWSH=0

for arg in "$@"; do
  case "$arg" in
    --with-pwsh)
      RUN_PWSH=1
      ;;
    *)
      echo "ERROR: Unknown argument: $arg"
      echo "Usage: $0 [--with-pwsh]"
      exit 1
      ;;
  esac
done

for required in build_common.sh build_linux_tarball.sh build_win_zip.sh test_bundled_plus_ext_linux.sh test_bundled_plus_ext_win.ps1 bundled_ext.config; do
  if [[ ! -f "$DIST_DIR/$required" ]]; then
    echo "ERROR: Missing required file: $DIST_DIR/$required"
    exit 1
  fi
done

cd "$REPO_ROOT"

echo "============================================"
echo " Distribution Build + E2E Validation"
echo "============================================"
echo "Repo root : $REPO_ROOT"
echo "Out root  : $OUT_ROOT"

echo ""
echo "[1/4] Build common distribution assets"
chmod +x "$DIST_DIR/build_common.sh"
OUT_ROOT="$OUT_ROOT" "$DIST_DIR/build_common.sh"

echo ""
echo "[2/4] Build Linux distribution artifact"
chmod +x "$DIST_DIR/build_linux_tarball.sh"
OUT_ROOT="$OUT_ROOT" "$DIST_DIR/build_linux_tarball.sh"

echo ""
echo "[3/4] Validate Linux bundled + optional extensions"
chmod +x "$DIST_DIR/test_bundled_plus_ext_linux.sh"
OUT_ROOT="$OUT_ROOT" "$DIST_DIR/test_bundled_plus_ext_linux.sh"

if [[ $RUN_PWSH -eq 1 ]]; then
  echo ""
  echo "[4/4] Build Windows distribution artifact + validate via PowerShell"
  if ! command -v pwsh >/dev/null 2>&1; then
    echo "ERROR: --with-pwsh requested, but pwsh is not installed."
    exit 1
  fi

  chmod +x "$DIST_DIR/build_win_zip.sh"
  OUT_ROOT="$OUT_ROOT" "$DIST_DIR/build_win_zip.sh"

  pwsh -NoLogo -NoProfile -File "$DIST_DIR/test_bundled_plus_ext_win.ps1" -OutRoot "$OUT_ROOT"
else
  echo ""
  echo "[4/4] Skipping PowerShell validation (use --with-pwsh to enable)"
fi

echo ""
echo "All selected distribution checks completed successfully."
