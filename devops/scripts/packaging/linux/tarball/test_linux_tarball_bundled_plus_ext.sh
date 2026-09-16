#!/usr/bin/env bash
set -euo pipefail

# test_linux_tarball_bundled_plus_ext.sh
# Linux scenario: install bundled Linux tarball, verify built-ins, then add optional extensions.

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

OUT_ROOT="$(absolute_path "${OUT_ROOT:-$REPO_ROOT/devops/out/linux}")"
COMMON_OUT_ROOT="$(absolute_path "${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}")"
SIM_ROOT="$(realpath "${SIM_ROOT:-$REPO_ROOT/devops/sim/linux-tarball-bundled-plus-ext}" 2>/dev/null || echo "${SIM_ROOT:-$REPO_ROOT/devops/sim/linux-tarball-bundled-plus-ext}")"
SKIP_RUNTIME="${SKIP_RUNTIME:-false}"
RID="${RID:-linux-x64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
PACKAGE_PROFILE="$(echo "$PACKAGE_PROFILE" | tr '[:upper:]' '[:lower:]')"
ARTIFACT_VERSION="${ARTIFACT_VERSION:-}"
ARTIFACT_VERSION_SUFFIX="${ARTIFACT_VERSION_SUFFIX-$(read_artifact_config artifact_version_suffix preview)}"
CLI_ARCHIVE_STEM="${CLI_ARCHIVE_STEM:-$(read_artifact_config cli_archive_stem azcli)}"
CLI_LEAN_ARCHIVE_STEM="${CLI_LEAN_ARCHIVE_STEM:-$(read_artifact_config cli_lean_archive_stem azcli-lean)}"
ASSET_RID="$RID"
if [[ "$RID" == "linux-x64" ]]; then
  ASSET_RID="$(read_artifact_config linux_x64_label linux-x64)"
elif [[ "$RID" == "linux-arm64" ]]; then
  ASSET_RID="$(read_artifact_config linux_arm64_label linux-arm64)"
elif [[ "$RID" == "linux-musl-x64" ]]; then
  ASSET_RID="$(read_artifact_config linux_musl_x64_label linux-musl-x64)"
elif [[ "$RID" == "linux-musl-arm64" ]]; then
  ASSET_RID="$(read_artifact_config linux_musl_arm64_label linux-musl-arm64)"
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
  echo "Then: ./devops/scripts/packaging/linux/tarball/build_linux_tarball.sh"
  exit 1
fi

if [[ ! -d "$EXT_PACK_DIR" ]]; then
  echo "ERROR: Extension package directory not found at $EXT_PACK_DIR"
  echo "Run: ./devops/scripts/packaging/common/build_common.sh"
  exit 1
fi

echo "============================================"
echo " Linux: Bundled CLI + Add Optional Extensions"
echo "============================================"

rm -rf "$SIM_ROOT"
mkdir -p "$SIM_ROOT/home" "$SIM_ROOT/install"

if [[ "$SKIP_RUNTIME" == "true" ]]; then
  tar -xzf "$CLI_TAR_PATH" -C "$SIM_ROOT/install"
  chmod +x "$SIM_ROOT/install/az"
else
  AZCLI_INSTALL_ROOT="$SIM_ROOT/install" "$REPO_ROOT/install/azcli-install.sh" \
    --package "$PACKAGE_PROFILE" \
    --archive "$CLI_TAR_PATH"
fi

AZ="$SIM_ROOT/install/az"

if [[ ! -x "$AZ" ]]; then
  echo "FAIL: az executable not found in installed payload"
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

COPILOT="$SIM_ROOT/install/Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/$RID/native/copilot"
if [[ ! -x "$COPILOT" ]]; then
  echo "FAIL: Bundled Copilot CLI is not executable at the agent resolver path: $COPILOT"
  exit 1
fi

echo "PASS: Bundled extension folders are present"
echo "PASS: Bundled Copilot CLI is present at the agent resolver path"
echo "PASS: NOTICE, LICENSE, and completion scripts are present"

if [[ "$SKIP_RUNTIME" == "true" ]]; then
  echo "NOTE: SKIP_RUNTIME=true, skipping Linux runtime execution checks."
  echo "Result: Linux package layout validation succeeded."
  exit 0
fi

echo ""
echo "[1/7] CLI version (bundled install)"
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
echo "[2/7] CLI help (bundled install)"
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
echo "[3/7] Verify bundled Copilot CLI daemon starts"
if ! copilot_output="$(HOME="$SIM_ROOT/home" timeout 15s "$COPILOT" --server --stdio </dev/null 2>&1)"; then
  echo "FAIL: Bundled Copilot CLI failed to start"
  printf '%s\n' "$copilot_output" | sed -n '1,20p'
  exit 1
fi
echo "  PASS: Bundled Copilot CLI daemon started and stopped cleanly"

echo ""
echo "[4/7] Verify built-in extension manager is available"
output=$(HOME="$SIM_ROOT/home" "$AZ" extension add 2>&1 || true)
echo "  $output"
if grep -Eqi "Extension.*could not be found" <<<"$output"; then
  echo "  FAIL: Extension manager not loaded"
  exit 1
elif grep -Eqi "Either Name or Path must be provided|Required option.*(name|path)" <<<"$output"; then
  echo "  PASS: Extension manager loaded"
else
  echo "  FAIL: Extension command response was unexpected"
  exit 1
fi

echo ""
echo "[5/7] Verify bundled Config commands work"
set_output=$(HOME="$SIM_ROOT/home" "$AZ" config set --key core.logging.level --value debug --force 2>&1 || true)
get_output=$(HOME="$SIM_ROOT/home" "$AZ" config get --key core.logging.level 2>&1 || true)
echo "  $set_output"
echo "  $get_output"
if grep -Fq "core.logging.level = debug" <<<"$get_output"; then
  echo "  PASS: Bundled Config commands work"
else
  echo "  FAIL: Bundled Config commands did not return expected value"
  exit 1
fi

echo ""
echo "[6/7] Install Resources extension via 'extension add'"
RES_NUPKG=$(find "$EXT_PACK_DIR" -name "azcli.extensions.resources*.nupkg" | head -1)
if [[ -z "${RES_NUPKG:-}" ]]; then
  echo "ERROR: Resources package not found in $EXT_PACK_DIR"
  exit 1
fi
echo "Installing from: $RES_NUPKG"
output=$(HOME="$SIM_ROOT/home" "$AZ" extension add --path "$RES_NUPKG" 2>&1 || true)
echo "  $output"
if grep -Fq "installedExtensionPath" <<<"$output"; then
  echo "  PASS: Resources extension installed"
else
  echo "  FAIL: Resources extension install output did not include installed path"
  exit 1
fi

echo ""
echo "[7/7] Verify Resources command resolves"
output=$(HOME="$SIM_ROOT/home" "$AZ" group list 2>&1 || true)
echo "  $output"
if grep -Fq "could not be found" <<<"$output"; then
  echo "  FAIL: Resources extension command not found"
  exit 1
elif grep -Eqi "Required parameter 'SubscriptionId' is missing|SubscriptionId must be provided|SubscriptionId cannot be resolved" <<<"$output"; then
  echo "  PASS: Resources extension loaded"
elif grep -Eqi "AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication" <<<"$output"; then
  echo "  PASS: Resources command resolved (authentication context not available in CI)"
else
  echo "  PASS: Resources command resolved"
fi

echo ""
echo "Installed extensions:"
if [[ -d "$SIM_ROOT/home/.azclips/extensions" ]]; then
  find "$SIM_ROOT/home/.azclips/extensions" -maxdepth 3 -type d | sort
else
  echo "  No installed extensions directory found."
fi

echo ""
echo "============================================"
echo " Linux Bundled+Optional Test Complete"
echo "============================================"
echo "Result: Linux bundled install + bundled Config verification + Resources extension add succeeded."
