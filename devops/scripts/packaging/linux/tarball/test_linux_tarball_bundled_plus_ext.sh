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

expect_install_failure() {
  local description="$1"
  local expected_message="$2"
  shift 2
  local install_root="${INSTALL_FAILURE_ROOT:-$SIM_ROOT/install}"
  local output=""

  if output=$(HOME="$SIM_ROOT/home" AZCLI_INSTALL_ROOT="$install_root" "$REPO_ROOT/install/azcli-install.sh" "$@" 2>&1); then
    echo "FAIL: $description unexpectedly succeeded"
    exit 1
  fi
  if ! grep -Fq -e "$expected_message" <<<"$output"; then
    echo "FAIL: $description did not report the expected error"
    printf '%s\n' "$output" | sed -n '1,20p'
    exit 1
  fi
  echo "  PASS: $description was rejected"
}

expect_uninstall_failure() {
  local description="$1"
  local expected_message="$2"
  local home_root="$3"
  local install_root="$4"
  shift 4
  local output=""

  if output=$(HOME="$home_root" AZCLI_INSTALL_ROOT="$install_root" "$REPO_ROOT/install/azcli-uninstall.sh" "$@" 2>&1); then
    echo "FAIL: $description unexpectedly succeeded"
    exit 1
  fi
  if ! grep -Fq -e "$expected_message" <<<"$output"; then
    echo "FAIL: $description did not report the expected error"
    printf '%s\n' "$output" | sed -n '1,20p'
    exit 1
  fi
  echo "  PASS: $description was rejected"
}

OUT_ROOT="$(absolute_path "${OUT_ROOT:-$REPO_ROOT/devops/out/linux}")"
COMMON_OUT_ROOT="$(absolute_path "${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}")"
SIM_ROOT="$(realpath "${SIM_ROOT:-$REPO_ROOT/devops/sim/linux-tarball-bundled-plus-ext}" 2>/dev/null || echo "${SIM_ROOT:-$REPO_ROOT/devops/sim/linux-tarball-bundled-plus-ext}")"
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
mkdir -p "$SIM_ROOT/home" "$SIM_ROOT/install" "$SIM_ROOT/invalid-archives" "$SIM_ROOT/classic-bin"
export AZCLIPS_HOME="$SIM_ROOT/home/.azclips"

archive_name="$(basename "$CLI_TAR_PATH")"
archive_version_and_rid="${archive_name#"$ARTIFACT_STEM"-}"
if [[ "$PACKAGE_PROFILE" == "lean" ]]; then
  wrong_profile_name="$CLI_ARCHIVE_STEM-$archive_version_and_rid"
else
  wrong_profile_name="$CLI_LEAN_ARCHIVE_STEM-$archive_version_and_rid"
fi
case "$ASSET_RID" in
  *-x64) wrong_rid_name="${archive_name/%-x64.tar.gz/-arm64.tar.gz}" ;;
  *-arm64) wrong_rid_name="${archive_name/%-arm64.tar.gz/-x64.tar.gz}" ;;
esac

ln -s "$CLI_TAR_PATH" "$SIM_ROOT/invalid-archives/$wrong_profile_name"
ln -s "$CLI_TAR_PATH" "$SIM_ROOT/invalid-archives/$wrong_rid_name"
printf 'not a tar archive\n' >"$SIM_ROOT/invalid-archives/$archive_name"

echo "[1/17] Reject invalid local archives"
expect_install_failure "empty archive path" "--archive requires a path" \
  --package "$PACKAGE_PROFILE" --archive ""
expect_install_failure "missing archive" "Archive was not found" \
  --package "$PACKAGE_PROFILE" --archive "$SIM_ROOT/invalid-archives/missing-$archive_name"
expect_install_failure "wrong package profile" "is not a $PACKAGE_PROFILE CLI package" \
  --package "$PACKAGE_PROFILE" --archive "$SIM_ROOT/invalid-archives/$wrong_profile_name"
expect_install_failure "wrong runtime identifier" "does not match platform $ASSET_RID" \
  --package "$PACKAGE_PROFILE" --archive "$SIM_ROOT/invalid-archives/$wrong_rid_name"
printf 'home sentinel\n' >"$SIM_ROOT/home/sentinel"
INSTALL_FAILURE_ROOT="$SIM_ROOT/home" expect_install_failure "unsafe install root" "Refusing to use unsafe install root" \
  --package "$PACKAGE_PROFILE" --archive "$CLI_TAR_PATH"
if [[ "$(cat "$SIM_ROOT/home/sentinel")" != "home sentinel" ]]; then
  echo "FAIL: rejected unsafe install root was modified"
  exit 1
fi
echo "  PASS: rejected unsafe install root was preserved"
unknown_install_root="$SIM_ROOT/unknown-install"
mkdir -p "$unknown_install_root"
printf 'unknown install sentinel\n' >"$unknown_install_root/sentinel"
INSTALL_FAILURE_ROOT="$unknown_install_root" expect_install_failure "unknown nonempty install root" "does not look like an azcli install" \
  --package "$PACKAGE_PROFILE" --archive "$CLI_TAR_PATH"
if [[ "$(cat "$unknown_install_root/sentinel")" != "unknown install sentinel" ]]; then
  echo "FAIL: rejected unknown install root was modified"
  exit 1
fi
echo "  PASS: rejected unknown install root was preserved"
printf 'existing install sentinel\n' >"$SIM_ROOT/install/az"
expect_install_failure "corrupt archive" "tar:" \
  --package "$PACKAGE_PROFILE" --archive "$SIM_ROOT/invalid-archives/$archive_name"
if [[ "$(cat "$SIM_ROOT/install/az")" != "existing install sentinel" ]]; then
  echo "FAIL: corrupt archive modified the existing installation"
  exit 1
fi
echo "  PASS: corrupt archive preserved the existing installation"
rm -rf "$SIM_ROOT/invalid-archives"

echo "[Preflight] Verify the default install path handles spaces and non-ASCII characters"
default_home="$SIM_ROOT/Test User José"
default_install_root="$default_home/.local/share/azcli"
default_home_sentinel="$default_home/sentinel"
mkdir -p "$default_home"
printf 'default home sentinel\n' >"$default_home_sentinel"
(
  unset AZCLI_INSTALL_ROOT AZCLIPS_HOME
  HOME="$default_home" "$REPO_ROOT/install/azcli-install.sh" --package "$PACKAGE_PROFILE" --archive "$CLI_TAR_PATH"
)
default_az="$default_install_root/az"
HOME="$default_home" AZCLIPS_HOME="$default_home/.azclips" "$default_az" --version >/dev/null
HOME="$default_home" AZCLIPS_HOME="$default_home/.azclips" "$default_az" --help >/dev/null
printf 'stale install sentinel\n' >"$default_install_root/stale"
(
  unset AZCLI_INSTALL_ROOT AZCLIPS_HOME
  HOME="$default_home" "$REPO_ROOT/install/azcli-install.sh" --package "$PACKAGE_PROFILE" --archive "$CLI_TAR_PATH"
)
if [[ -e "$default_install_root/stale" ]]; then
  echo "FAIL: reinstall left a stale file under the default install path"
  exit 1
fi
(
  unset AZCLI_INSTALL_ROOT
  HOME="$default_home" "$REPO_ROOT/install/azcli-uninstall.sh"
)
if [[ -e "$default_install_root" || "$(cat "$default_home_sentinel")" != "default home sentinel" ]]; then
  echo "FAIL: default-path uninstall removed data outside the install root"
  exit 1
fi
echo "  PASS: default-path install, runtime, reinstall, and uninstall succeeded"

echo "[Preflight] Verify uninstaller safety"
uninstall_home="$SIM_ROOT/uninstall-safety-home"
absent_install_root="$uninstall_home/already-absent"
unknown_uninstall_root="$uninstall_home/unknown-install"
forced_uninstall_root="$uninstall_home/forced-install"
outside_sentinel="$uninstall_home/outside-sentinel"
mkdir -p "$uninstall_home"
printf 'home sentinel\n' >"$uninstall_home/sentinel"
HOME="$uninstall_home" AZCLI_INSTALL_ROOT="$absent_install_root" "$REPO_ROOT/install/azcli-uninstall.sh" >/dev/null
if [[ -e "$absent_install_root" ]]; then
  echo "FAIL: idempotent uninstall created the absent install root"
  exit 1
fi
expect_uninstall_failure "unsafe uninstall root" "Refusing to operate on unsafe install root" \
  "$uninstall_home" "$uninstall_home"
mkdir -p "$unknown_uninstall_root"
printf 'unknown install sentinel\n' >"$unknown_uninstall_root/sentinel"
expect_uninstall_failure "unknown nonempty uninstall root" "does not look like an azcli install" \
  "$uninstall_home" "$unknown_uninstall_root"
if [[ "$(cat "$unknown_uninstall_root/sentinel")" != "unknown install sentinel" ]]; then
  echo "FAIL: rejected unknown uninstall root was modified"
  exit 1
fi
mkdir -p "$forced_uninstall_root"
printf 'forced install sentinel\n' >"$forced_uninstall_root/sentinel"
printf 'outside sentinel\n' >"$outside_sentinel"
HOME="$uninstall_home" AZCLI_INSTALL_ROOT="$forced_uninstall_root" "$REPO_ROOT/install/azcli-uninstall.sh" --force >/dev/null
if [[ -e "$forced_uninstall_root" || "$(cat "$outside_sentinel")" != "outside sentinel" ]]; then
  echo "FAIL: forced uninstall was not limited to the selected root"
  exit 1
fi
echo "  PASS: idempotent, guarded, and forced uninstall behavior succeeded"

echo "classic az sentinel" >"$SIM_ROOT/classic-bin/az"
chmod +x "$SIM_ROOT/classic-bin/az"

HOME="$SIM_ROOT/home" AZCLI_INSTALL_ROOT="$SIM_ROOT/install" "$REPO_ROOT/install/azcli-install.sh" \
  --package "$PACKAGE_PROFILE" \
  --archive "$CLI_TAR_PATH"

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

echo ""
echo "[2/17] Verify bundled extension sidecars are present"
sidecar_count=0
for ext_dir in "$SIM_ROOT/install/Extensions"/*/; do
  ext_name="$(basename "$ext_dir")"
  sidecar="$ext_dir$ext_name.commands.json"
  if [[ ! -f "$sidecar" ]]; then
    echo "  FAIL: sidecar missing for bundled extension $ext_name ($sidecar)"
    exit 1
  fi
  # Validate the sidecar schema here; contract contents are compared with ExtensionIndex in Cli.Tests.
  if ! python3 - "$sidecar" "$ext_name" <<'PY'
import json
import sys

sidecar, expected_package_id = sys.argv[1:]

def fail(message):
    print(f"  FAIL: {message}: {sidecar}", file=sys.stderr)
    sys.exit(1)

try:
    with open(sidecar, encoding="utf-8") as sidecar_file:
        payload = json.load(sidecar_file)
except (OSError, json.JSONDecodeError) as error:
    fail(f"sidecar is not valid JSON ({error})")

if not isinstance(payload, dict):
    fail("sidecar root must be an object")
if payload.get("schemaVersion") != 1:
    fail("sidecar schemaVersion must be 1")
if payload.get("packageId") != expected_package_id:
    fail(f"sidecar packageId must be {expected_package_id}")

verbs = payload.get("verbs")
leaves = payload.get("leaves")
if not isinstance(verbs, list) or not all(isinstance(verb, str) for verb in verbs):
    fail("sidecar verbs must be an array of strings")
if not isinstance(leaves, list) or not all(isinstance(leaf, str) for leaf in leaves):
    fail("sidecar leaves must be an array of strings")

PY
  then
    exit 1
  fi
  sidecar_count=$((sidecar_count + 1))
done
if [[ "$sidecar_count" -eq 0 ]]; then
  echo "  FAIL: no bundled extensions found under $SIM_ROOT/install/Extensions"
  exit 1
fi
echo "  PASS: $sidecar_count bundled extension sidecar(s) present and well-formed"
echo "  PASS: bundled extension sidecars match the required schema and field types"

echo ""
echo "[3/17] CLI version (bundled install)"
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
echo "[4/17] CLI help (bundled install)"
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
echo "[5/17] Verify bundled Copilot CLI daemon starts"
if ! copilot_output="$(HOME="$SIM_ROOT/home" timeout 15s "$COPILOT" --server --stdio </dev/null 2>&1)"; then
  echo "FAIL: Bundled Copilot CLI failed to start"
  printf '%s\n' "$copilot_output" | sed -n '1,20p'
  exit 1
fi
echo "  PASS: Bundled Copilot CLI daemon started and stopped cleanly"

echo ""
echo "[6/17] Verify built-in extension manager is available"
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
echo "[7/17] Verify bundled Config commands work"
set_output=$(HOME="$SIM_ROOT/home" "$AZ" config set --key core.logging.level --value debug --force 2>&1 || true)
get_output=$(HOME="$SIM_ROOT/home" "$AZ" config get --key core.logging.level --query "value" || true)
echo "  $set_output"
echo "  $get_output"
if [[ "$get_output" == "debug" ]]; then
  echo "  PASS: Bundled Config commands work"
else
  echo "  FAIL: Bundled Config commands did not return expected value"
  exit 1
fi

echo ""
echo "[8/17] Verify reinstall cleanly replaces the install root"
touch "$SIM_ROOT/install/reinstall-sentinel"
HOME="$SIM_ROOT/home" AZCLI_INSTALL_ROOT="$SIM_ROOT/install" "$REPO_ROOT/install/azcli-install.sh" \
  --package "$PACKAGE_PROFILE" \
  --archive "$CLI_TAR_PATH"
if [[ -e "$SIM_ROOT/install/reinstall-sentinel" ]]; then
  echo "  FAIL: Reinstall did not cleanly replace the install root"
  exit 1
fi
echo "  PASS: Reinstall removed stale files and restored the CLI"

echo ""
echo "[9/17] Install Resources extension via 'extension add'"
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
echo "[10/17] Verify Resources sidecar installed via 'extension add'"
res_sidecar=$(find "$AZCLIPS_HOME/extensions/Microsoft.Azure.Azclips.Extensions.Resources" -maxdepth 2 -name "Microsoft.Azure.Azclips.Extensions.Resources.commands.json" -print -quit 2>/dev/null || true)
if [[ -z "$res_sidecar" ]]; then
  echo "  FAIL: Resources sidecar not found after extension add (expected under \$AZCLIPS_HOME/extensions/Microsoft.Azure.Azclips.Extensions.Resources/<version>/)"
  exit 1
fi
if ! grep -Fq '"schemaVersion"' "$res_sidecar"; then
  echo "  FAIL: installed Resources sidecar is malformed: $res_sidecar"
  exit 1
fi
echo "  PASS: Resources sidecar present after nupkg install: $res_sidecar"

echo ""
echo "[11/17] Verify Resources appears in extension list JSON"
if ! list_output=$(HOME="$SIM_ROOT/home" "$AZ" extension list --output json 2>&1); then
  echo "  FAIL: extension list failed"
  printf '%s\n' "$list_output" | sed -n '1,20p'
  exit 1
fi
if ! grep -Eq '"packageId"[[:space:]]*:[[:space:]]*"Microsoft\.Azure\.Azclips\.Extensions\.Resources"' <<<"$list_output" ||
  ! grep -Eq '"selectedVersion"[[:space:]]*:' <<<"$list_output"; then
  echo "  FAIL: extension list JSON did not contain the installed Resources package and version"
  printf '%s\n' "$list_output" | sed -n '1,40p'
  exit 1
fi
echo "  PASS: extension list reports Resources and its selected version"

echo ""
echo "[12/17] Verify Resources appears in extension show JSON"
if ! show_output=$(HOME="$SIM_ROOT/home" "$AZ" extension show --name Resources --output json 2>&1); then
  echo "  FAIL: extension show failed"
  printf '%s\n' "$show_output" | sed -n '1,20p'
  exit 1
fi
if ! grep -Eq '"packageId"[[:space:]]*:[[:space:]]*"Microsoft\.Azure\.Azclips\.Extensions\.Resources"' <<<"$show_output" ||
  ! grep -Eq '"location"[[:space:]]*:' <<<"$show_output"; then
  echo "  FAIL: extension show JSON did not contain the installed Resources package and location"
  printf '%s\n' "$show_output" | sed -n '1,40p'
  exit 1
fi
echo "  PASS: extension show reports Resources and its location"

echo ""
echo "[13/17] Verify Resources command resolves"
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
echo "[14/17] Remove Resources extension"
if ! remove_output=$(HOME="$SIM_ROOT/home" "$AZ" extension remove --name Resources 2>&1); then
  echo "  FAIL: extension remove failed"
  printf '%s\n' "$remove_output" | sed -n '1,20p'
  exit 1
fi
printf '%s\n' "$remove_output" | sed -n '1,20p'
echo "  PASS: Resources extension remove completed"

echo ""
echo "[15/17] Verify Resources files and registry entry are gone"
if find "$SIM_ROOT/home/.azclips/extensions" -mindepth 1 -iname '*resources*' -print -quit 2>/dev/null | grep -q .; then
  echo "  FAIL: Resources files remain under the installed extensions root"
  exit 1
fi
if [[ -f "$SIM_ROOT/home/.azclips/extensions/registry.json" ]] &&
   grep -Fqi 'Microsoft.Azure.Azclips.Extensions.Resources' "$SIM_ROOT/home/.azclips/extensions/registry.json"; then
  echo "  FAIL: Resources remains in the extension registry"
  exit 1
fi
echo "  PASS: Resources files and registry entry are gone"

echo ""
echo "[16/17] Verify Resources command no longer resolves"
removed_command_output=$(HOME="$SIM_ROOT/home" "$AZ" group list 2>&1 || true)
printf '%s\n' "$removed_command_output" | sed -n '1,20p'
if [[ "$PACKAGE_PROFILE" == "lean" ]] &&
   grep -Fq '[ExtensionNotFound]' <<<"$removed_command_output"; then
  echo "  PASS: Removed Resources command reports the extension installation path"
elif [[ "$PACKAGE_PROFILE" == "full" ]] &&
     grep -Fq "[BundledExtensionLoader] Loaded 'Resources'" <<<"$removed_command_output" &&
     ! grep -Fq '[ExtensionNotFound]' <<<"$removed_command_output"; then
  echo "  PASS: Resources command fell back to the bundled full-package extension"
else
  echo "  FAIL: az group list did not match the expected $PACKAGE_PROFILE profile behavior after removal"
  exit 1
fi

echo ""
echo "[17/17] Uninstall isolated azcli and preserve classic az"
AZCLI_INSTALL_ROOT="$SIM_ROOT/install" "$REPO_ROOT/install/azcli-uninstall.sh"
if [[ -e "$SIM_ROOT/install" ]]; then
  echo "  FAIL: azcli install root remains after uninstall"
  exit 1
fi
if [[ ! -x "$SIM_ROOT/classic-bin/az" ]] ||
   [[ "$(cat "$SIM_ROOT/classic-bin/az")" != "classic az sentinel" ]]; then
  echo "  FAIL: Uninstall modified the classic az sentinel"
  exit 1
fi
echo "  PASS: Isolated azcli was removed and classic az was preserved"

echo ""
echo "============================================"
echo " Linux Bundled+Optional Test Complete"
echo "============================================"
echo "Result: Linux installer, runtime, reinstall, extension lifecycle, failure paths, and uninstall succeeded."
