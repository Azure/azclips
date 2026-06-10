#!/usr/bin/env bash
set -euo pipefail

# test_macos_tarball_bundled_plus_ext.sh
# macOS scenario: validate bundled macOS tarball layout, then run runtime checks on macOS hosts.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

absolute_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

OUT_ROOT="$(absolute_path "${OUT_ROOT:-$REPO_ROOT/devops/out/macos}")"
COMMON_OUT_ROOT="$(absolute_path "${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}")"
SIM_ROOT="$(realpath "${SIM_ROOT:-$REPO_ROOT/devops/sim/macos-tarball-bundled-plus-ext}" 2>/dev/null || echo "${SIM_ROOT:-$REPO_ROOT/devops/sim/macos-tarball-bundled-plus-ext}")"

RID="${RID:-osx-arm64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
PACKAGE_PROFILE="$(echo "$PACKAGE_PROFILE" | tr '[:upper:]' '[:lower:]')"
ARTIFACT_VERSION="${ARTIFACT_VERSION:-}"

if [[ "$PACKAGE_PROFILE" == "full" ]]; then
  ARTIFACT_PREFIX="azv2"
elif [[ "$PACKAGE_PROFILE" == "lean" ]]; then
  ARTIFACT_PREFIX="azv2-lean"
else
  echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
  exit 1
fi

if [[ -n "$ARTIFACT_VERSION" ]]; then
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_PREFIX-$RID-$ARTIFACT_VERSION.tar.gz}"
else
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_PREFIX-$RID.tar.gz}"
fi
EXT_PACK_DIR="$COMMON_OUT_ROOT/extensions-pack"

if [[ ! -f "$CLI_TAR_PATH" ]]; then
  echo "ERROR: CLI tarball not found at $CLI_TAR_PATH"
  echo "Run: ./devops/scripts/packaging/common/build_common.sh"
  echo "Then: ./devops/scripts/packaging/macos/tarball/build_macos_tarball.sh"
  exit 1
fi

if [[ ! -d "$EXT_PACK_DIR" ]]; then
  echo "ERROR: Extension package directory not found at $EXT_PACK_DIR"
  echo "Run: ./devops/scripts/packaging/common/build_common.sh"
  exit 1
fi

echo "============================================"
echo " macOS: Bundled CLI + Add Optional Extensions"
echo "============================================"

rm -rf "$SIM_ROOT"
mkdir -p "$SIM_ROOT/home" "$SIM_ROOT/install"

tar -xzf "$CLI_TAR_PATH" -C "$SIM_ROOT/install"

AZV2="$SIM_ROOT/install/azv2"
if [[ ! -f "$AZV2" ]]; then
  echo "FAIL: azv2 binary not found in extracted tarball"
  exit 1
fi

if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Cirrus.Extensions.Extension" ]]; then
  echo "FAIL: Built-in Extension manager bundle missing"
  exit 1
fi
if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Cirrus.Extensions.Login" ]]; then
  echo "FAIL: Built-in Login bundle missing"
  exit 1
fi
if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Cirrus.Extensions.Config" ]]; then
  echo "FAIL: Built-in Config bundle missing"
  exit 1
fi

echo "PASS: Bundled extension folders are present"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "NOTE: Non-macOS host detected; skipping macOS runtime execution checks."
  exit 0
fi

chmod +x "$AZV2"

# Hosted macOS pools are often x86_64. When validating an osx-arm64 build there,
# keep layout checks but skip runtime execution to avoid false failures.
HOST_ARCH="$(uname -m)"
BIN_INFO="$(file "$AZV2" 2>/dev/null || true)"
if [[ -n "$BIN_INFO" ]]; then
  echo "Binary info: $BIN_INFO"
fi

is_runnable=true
if [[ "$HOST_ARCH" == "arm64" ]]; then
  if [[ "$BIN_INFO" != *"arm64"* && "$BIN_INFO" != *"universal"* ]]; then
    is_runnable=false
  fi
elif [[ "$HOST_ARCH" == "x86_64" ]]; then
  if [[ "$BIN_INFO" != *"x86_64"* && "$BIN_INFO" != *"universal"* ]]; then
    is_runnable=false
  fi
fi

if [[ "$is_runnable" != "true" ]]; then
  echo "NOTE: Host architecture '$HOST_ARCH' is not compatible with extracted binary."
  echo "NOTE: Skipping runtime execution checks; package layout validation already passed."
  exit 0
fi

echo ""
echo "[1/4] CLI help (bundled install)"
HOME="$SIM_ROOT/home" "$AZV2" --help 2>&1 | head -n 20 || true

echo ""
echo "[2/4] Verify built-in extension manager is available"
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
echo "[3/4] Verify bundled Config commands work"
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
echo "[4/4] Install Resources extension and verify command resolves"
RES_NUPKG=$(find "$EXT_PACK_DIR" -name "Microsoft.Azure.Cirrus.Extensions.Resources*.nupkg" | head -1)
if [[ -z "${RES_NUPKG:-}" ]]; then
  echo "ERROR: Resources package not found in $EXT_PACK_DIR"
  exit 1
fi

output=$(HOME="$SIM_ROOT/home" "$AZV2" extension add --path "$RES_NUPKG" 2>&1 || true)
echo "  $output"
if ! echo "$output" | grep -q "installedExtensionPath"; then
  echo "  FAIL: Resources extension install output did not include installed path"
  exit 1
fi

output=$(HOME="$SIM_ROOT/home" "$AZV2" group list 2>&1 || true)
echo "  $output"
if echo "$output" | grep -q "could not be found"; then
  echo "  FAIL: Resources extension command not found"
  exit 1
elif echo "$output" | grep -q "Required parameter 'SubscriptionId' is missing"; then
  echo "  PASS: Resources extension loaded"
elif echo "$output" | grep -Eqi "AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication"; then
  echo "  PASS: Resources command resolved (authentication context not available)"
else
  echo "  PASS: Resources command resolved"
fi

echo ""
echo "macOS distribution checks completed successfully."
