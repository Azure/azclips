#!/usr/bin/env bash
set -euo pipefail

# test_macos_tarball_bundled_plus_ext.sh
# macOS scenario: validate bundled macOS tarball layout, then run runtime checks on macOS hosts.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

read_artifact_config() {
  local key="$1"
  local default_value="$2"
  local config_path="${ARTIFACT_NAMING_CONFIG:-$PACKAGING_ROOT/config/artifact_naming.config}"
  local configured_value=""

  if [[ -f "$config_path" ]] && configured_value="$(awk -F= -v key="$key" '
      BEGIN { found = 0 }
      /^[[:space:]]*(#|$)/ { next }
      {
        k=$1
        sub(/^[[:space:]]+/, "", k)
        sub(/[[:space:]]+$/, "", k)
        if (k == key) {
          $1=""
          sub(/^=/, "")
          v=$0
          sub(/^[[:space:]]+/, "", v)
          sub(/[[:space:]]+$/, "", v)
          print v
          found = 1
          exit
        }
      }
      END { if (!found) exit 1 }
    ' "$config_path")"; then
    printf '%s\n' "$configured_value"
  else
    printf '%s\n' "$default_value"
  fi
}

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
ARTIFACT_VERSION_SUFFIX="${ARTIFACT_VERSION_SUFFIX-$(read_artifact_config artifact_version_suffix preview)}"
CLI_ARCHIVE_STEM="${CLI_ARCHIVE_STEM:-$(read_artifact_config cli_archive_stem azcli)}"
CLI_LEAN_ARCHIVE_STEM="${CLI_LEAN_ARCHIVE_STEM:-$(read_artifact_config cli_lean_archive_stem azcli-lean)}"
ASSET_RID="$RID"
if [[ "$RID" == "osx-arm64" ]]; then
  ASSET_RID="$(read_artifact_config macos_arm64_label macos-arm64)"
elif [[ "$RID" == "osx-x64" ]]; then
  ASSET_RID="$(read_artifact_config macos_x64_label macos-x86_64)"
fi

if [[ "$PACKAGE_PROFILE" == "full" ]]; then
  ARTIFACT_STEM="$CLI_ARCHIVE_STEM"
elif [[ "$PACKAGE_PROFILE" == "lean" ]]; then
  ARTIFACT_STEM="$CLI_LEAN_ARCHIVE_STEM"
else
  echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
  exit 1
fi

if [[ -n "$ARTIFACT_VERSION" ]]; then
  ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION"
  if [[ -n "$ARTIFACT_VERSION_SUFFIX" && "$ARTIFACT_VERSION_SEGMENT" != *"-$ARTIFACT_VERSION_SUFFIX" ]]; then
    ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION_SEGMENT-$ARTIFACT_VERSION_SUFFIX"
  fi
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_STEM-$ARTIFACT_VERSION_SEGMENT-$ASSET_RID.tar.gz}"
else
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_STEM-$ASSET_RID.tar.gz}"
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

AZ="$SIM_ROOT/install/az"
if [[ ! -f "$AZ" ]]; then
  echo "FAIL: az binary not found in extracted tarball"
  exit 1
fi

COPILOT="$SIM_ROOT/install/Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/$RID/native/copilot"
if [[ ! -x "$COPILOT" ]]; then
  echo "FAIL: Bundled Copilot CLI is not executable at the agent resolver path: $COPILOT"
  exit 1
fi

if [[ ! -f "$SIM_ROOT/install/NOTICE" ]]; then
  echo "FAIL: NOTICE file missing from extracted tarball"
  exit 1
fi

if [[ ! -f "$SIM_ROOT/install/LICENSE" ]]; then
  echo "FAIL: LICENSE file missing from extracted tarball"
  exit 1
fi

for completion_file in az.completions.ps1 az.completions.bash az.completions.zsh; do
  if [[ ! -f "$SIM_ROOT/install/completions/$completion_file" ]]; then
    echo "FAIL: completions/$completion_file missing from extracted tarball"
    exit 1
  fi
done

if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Azclips.Extensions.Extension" ]]; then
  echo "FAIL: Built-in Extension manager bundle missing"
  exit 1
fi
if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Azclips.Extensions.Accounts" ]]; then
  echo "FAIL: Built-in Accounts bundle missing"
  exit 1
fi
if [[ ! -d "$SIM_ROOT/install/Extensions/Microsoft.Azure.Azclips.Extensions.Config" ]]; then
  echo "FAIL: Built-in Config bundle missing"
  exit 1
fi

echo "PASS: Bundled extension folders are present"
echo "PASS: Bundled Copilot CLI is present at the agent resolver path"
echo "PASS: NOTICE, LICENSE, and completion scripts are present"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "NOTE: Non-macOS host detected; skipping macOS runtime execution checks."
  exit 0
fi

chmod +x "$AZ"

# Hosted macOS pools are often x86_64. When validating an osx-arm64 build there,
# keep layout checks but skip runtime execution to avoid false failures.
HOST_ARCH="$(uname -m)"
BIN_INFO="$(file "$AZ" 2>/dev/null || true)"
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
echo "[1/6] CLI version (bundled install)"
if ! version_output="$(HOME="$SIM_ROOT/home" "$AZ" --version 2>&1)"; then
  echo "FAIL: az --version exited with a non-zero status"
  printf '%s\n' "$version_output" | sed -n '1,20p'
  exit 1
fi
if [[ -z "${version_output//[[:space:]]/}" ]]; then
  echo "FAIL: az --version returned no output"
  exit 1
fi
printf '%s\n' "$version_output" | sed -n '1,20p'

echo ""
echo "[2/6] CLI help (bundled install)"
if ! help_output="$(HOME="$SIM_ROOT/home" "$AZ" --help 2>&1)"; then
  echo "FAIL: az --help exited with a non-zero status"
  printf '%s\n' "$help_output" | sed -n '1,20p'
  exit 1
fi
if [[ -z "${help_output//[[:space:]]/}" ]]; then
  echo "FAIL: az --help returned no output"
  exit 1
fi
printf '%s\n' "$help_output" | sed -n '1,20p'

echo ""
echo "[3/6] Verify bundled Copilot CLI daemon starts"
copilot_stdout="$SIM_ROOT/copilot.stdout"
copilot_stderr="$SIM_ROOT/copilot.stderr"
copilot_timeout="$SIM_ROOT/copilot.timeout"
HOME="$SIM_ROOT/home" "$COPILOT" --server --stdio </dev/null >"$copilot_stdout" 2>"$copilot_stderr" &
copilot_pid=$!
(
  sleep 15
  if kill -0 "$copilot_pid" 2>/dev/null; then
    touch "$copilot_timeout"
    kill "$copilot_pid" 2>/dev/null || true
  fi
) &
copilot_watchdog_pid=$!
if wait "$copilot_pid"; then
  copilot_exit_code=0
else
  copilot_exit_code=$?
fi
kill "$copilot_watchdog_pid" 2>/dev/null || true
wait "$copilot_watchdog_pid" 2>/dev/null || true
if [[ -f "$copilot_timeout" ]]; then
  echo "FAIL: Bundled Copilot CLI daemon did not stop within 15 seconds"
  sed -n '1,20p' "$copilot_stdout" "$copilot_stderr"
  exit 1
fi
if [[ "$copilot_exit_code" -ne 0 ]]; then
  echo "FAIL: Bundled Copilot CLI daemon exited with code $copilot_exit_code"
  sed -n '1,20p' "$copilot_stdout" "$copilot_stderr"
  exit 1
fi
echo "  PASS: Bundled Copilot CLI daemon started and stopped cleanly"

echo ""
echo "[4/6] Verify built-in extension manager is available"
output=$(HOME="$SIM_ROOT/home" "$AZ" extension add 2>&1 || true)
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
echo "[5/6] Verify bundled Config commands work"
set_output=$(HOME="$SIM_ROOT/home" "$AZ" config set --key core.logging.level --value debug --force 2>&1 || true)
get_output=$(HOME="$SIM_ROOT/home" "$AZ" config get --key core.logging.level 2>&1 || true)
echo "  $set_output"
echo "  $get_output"
if echo "$get_output" | grep -q "core.logging.level = debug"; then
  echo "  PASS: Bundled Config commands work"
else
  echo "  FAIL: Bundled Config commands did not return expected value"
  exit 1
fi

echo ""
echo "[6/6] Install Resources extension and verify command resolves"
RES_NUPKG=$(find "$EXT_PACK_DIR" -name "azcli.extensions.resources*.nupkg" | head -1)
if [[ -z "${RES_NUPKG:-}" ]]; then
  echo "ERROR: Resources package not found in $EXT_PACK_DIR"
  exit 1
fi

output=$(HOME="$SIM_ROOT/home" "$AZ" extension add --path "$RES_NUPKG" 2>&1 || true)
echo "  $output"
if ! echo "$output" | grep -q "installedExtensionPath"; then
  echo "  FAIL: Resources extension install output did not include installed path"
  exit 1
fi

output=$(HOME="$SIM_ROOT/home" "$AZ" group list 2>&1 || true)
echo "  $output"
if echo "$output" | grep -q "could not be found"; then
  echo "  FAIL: Resources extension command not found"
  exit 1
elif echo "$output" | grep -Eqi "Required parameter 'SubscriptionId' is missing|SubscriptionId must be provided|SubscriptionId cannot be resolved"; then
  echo "  PASS: Resources extension loaded"
elif echo "$output" | grep -Eqi "AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication"; then
  echo "  PASS: Resources command resolved (authentication context not available)"
else
  echo "  PASS: Resources command resolved"
fi

echo ""
echo "macOS distribution checks completed successfully."
