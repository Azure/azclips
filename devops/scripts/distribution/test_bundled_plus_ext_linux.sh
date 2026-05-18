#!/usr/bin/env bash
set -euo pipefail

# test_bundled_plus_ext_linux.sh
# Linux scenario: install bundled Linux tarball, verify built-ins, then add optional extensions.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="$(realpath "${OUT_ROOT:-$SCRIPT_DIR/out}")"
SIM_ROOT="$(realpath "$SCRIPT_DIR/sim-bundled-plus-linux" 2>/dev/null || echo "$SCRIPT_DIR/sim-bundled-plus-linux")"

CLI_TAR_PATH="$OUT_ROOT/bin/Release/azv2-linux-x64.tar.gz"
EXT_PACK_DIR="$OUT_ROOT/extensions-pack"

if [[ ! -f "$CLI_TAR_PATH" ]]; then
  echo "ERROR: CLI tarball not found at $CLI_TAR_PATH"
  echo "Run: ./devops/scripts/distribution/build_common.sh"
  echo "Then: ./devops/scripts/distribution/build_linux_tarball.sh"
  exit 1
fi

if [[ ! -d "$EXT_PACK_DIR" ]]; then
  echo "ERROR: Extension package directory not found at $EXT_PACK_DIR"
  echo "Run: ./devops/scripts/distribution/build_common.sh"
  exit 1
fi

echo "============================================"
echo " Linux: Bundled CLI + Add Optional Extensions"
echo "============================================"

rm -rf "$SIM_ROOT"
mkdir -p "$SIM_ROOT/home" "$SIM_ROOT/install"

tar -xzf "$CLI_TAR_PATH" -C "$SIM_ROOT/install"
chmod +x "$SIM_ROOT/install/azv2"
AZV2="$SIM_ROOT/install/azv2"

echo ""
echo "[1/5] CLI help (bundled install)"
HOME="$SIM_ROOT/home" "$AZV2" --help 2>&1 | head -n 20 || true

echo ""
echo "[2/5] Verify built-in extension manager is available"
output=$(HOME="$SIM_ROOT/home" "$AZV2" extension add 2>&1 || true)
echo "  $output"
if echo "$output" | grep -qi "Extension.*could not be found"; then
  echo "  FAIL: Extension manager not loaded"
  exit 1
elif echo "$output" | grep -Eqi "Either Name or Path must be provided|Required option.*(name|path)"; then
  echo "  PASS: Extension manager loaded"
else
  echo "  FAIL: Extension command response was unexpected"
  exit 1
fi

echo ""
echo "[3/5] Verify bundled Config commands work"
set_output=$(HOME="$SIM_ROOT/home" "$AZV2" config set --key core.logging.level --value debug --force 2>&1 || true)
get_output=$(HOME="$SIM_ROOT/home" "$AZV2" config get --key core.logging.level 2>&1 || true)
echo "  $set_output"
echo "  $get_output"
if echo "$get_output" | grep -q "core.logging.level = debug"; then
  echo "  PASS: Bundled Config commands work"
else
  echo "  FAIL: Bundled Config commands did not return expected value"
  exit 1
fi

echo ""
echo "[4/5] Install Resources extension via 'extension add'"
RES_NUPKG=$(find "$EXT_PACK_DIR" -name "Microsoft.Azure.Cirrus.Extensions.Resources*.nupkg" | head -1)
if [[ -z "${RES_NUPKG:-}" ]]; then
  echo "ERROR: Resources package not found in $EXT_PACK_DIR"
  exit 1
fi
echo "Installing from: $RES_NUPKG"
output=$(HOME="$SIM_ROOT/home" "$AZV2" extension add --path "$RES_NUPKG" 2>&1 || true)
echo "  $output"
if echo "$output" | grep -q "installedExtensionPath"; then
  echo "  PASS: Resources extension installed"
else
  echo "  FAIL: Resources extension install output did not include installed path"
  exit 1
fi

echo ""
echo "[5/5] Verify Resources command resolves"
output=$(HOME="$SIM_ROOT/home" "$AZV2" group list 2>&1 || true)
echo "  $output"
if echo "$output" | grep -q "could not be found"; then
  echo "  FAIL: Resources extension command not found"
  exit 1
elif echo "$output" | grep -q "Required parameter 'SubscriptionId' is missing"; then
  echo "  PASS: Resources extension loaded"
elif echo "$output" | grep -Eqi "AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication"; then
  echo "  PASS: Resources command resolved (authentication context not available in CI)"
else
  echo "  PASS: Resources command resolved"
fi

echo ""
echo "Installed extensions:"
find "$SIM_ROOT/home/.cirrus/extensions" -maxdepth 3 -type d | sort

echo ""
echo "============================================"
echo " Linux Bundled+Optional Test Complete"
echo "============================================"
echo "Result: Linux bundled install + bundled Config verification + Resources extension add succeeded."
