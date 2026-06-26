#!/usr/bin/env bash
set -euo pipefail

# build_macos_tarball.sh
# Builds macOS self-contained CLI, injects built-ins, and packages tar.gz.

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

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/macos}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-osx-arm64}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"
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

CLI_SC_DIR="$OUT_ROOT/publish/cli/$RID"
if [[ -n "$ARTIFACT_VERSION" ]]; then
  ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION"
  if [[ -n "$ARTIFACT_VERSION_SUFFIX" && "$ARTIFACT_VERSION_SEGMENT" != *"-$ARTIFACT_VERSION_SUFFIX" ]]; then
    ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION_SEGMENT-$ARTIFACT_VERSION_SUFFIX"
  fi
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_STEM-$ARTIFACT_VERSION_SEGMENT-$ASSET_RID.tar.gz}"
else
  CLI_TAR_PATH="${CLI_TAR_PATH:-$OUT_ROOT/Release/tarball/$ARTIFACT_STEM-$ASSET_RID.tar.gz}"
fi

if [[ ! -f "$INJECTOR_SCRIPT" ]]; then
  echo "ERROR: Injector script not found at $INJECTOR_SCRIPT"
  exit 1
fi

echo "Publishing CLI (Release, $RID)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$CLI_SC_DIR"

chmod +x "$INJECTOR_SCRIPT"
OUT_ROOT="$OUT_ROOT" "$INJECTOR_SCRIPT" --publish-dir "$CLI_SC_DIR" --builtin-root "$COMMON_OUT_ROOT/builtin-publish"

mkdir -p "$(dirname "$CLI_TAR_PATH")"
rm -f "$CLI_TAR_PATH"
tar -czf "$CLI_TAR_PATH" -C "$CLI_SC_DIR" .
echo "Created $CLI_TAR_PATH"
