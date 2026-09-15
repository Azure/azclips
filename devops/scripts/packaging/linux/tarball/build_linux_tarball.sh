#!/usr/bin/env bash
set -euo pipefail

# build_linux_tarball.sh
# Builds Linux self-contained CLI, injects built-ins, and packages tar.gz.

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

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/linux}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-linux-x64}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"
NOTICE_FILE="${NOTICE_FILE:-$REPO_ROOT/NOTICE}"
LICENSE_FILE="${LICENSE_FILE:-$REPO_ROOT/LICENSE}"
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

if [[ ! -f "$NOTICE_FILE" ]]; then
  echo "ERROR: NOTICE file was not found: $NOTICE_FILE"
  exit 1
fi

if [[ ! -f "$LICENSE_FILE" ]]; then
  echo "ERROR: LICENSE file was not found: $LICENSE_FILE"
  exit 1
fi

echo "Publishing CLI (Release, $RID)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$CLI_SC_DIR"

# Publish the Agent daemon into a scratch dir; stage_agent then splits the
# output: launcher files move to the CLI publish root (sibling to az so
# they share the runtime pack), and agent-only NuGet dependencies + prompts
# + copilot native binary move under
# Extensions/Microsoft.Azure.Azclips.Extensions.Agent/. Runtime-pack
# duplicates that are byte-identical to the CLI copy are deleted.
echo "Publishing Agent exe (Release, $RID) into scratch"
AGENT_SCRATCH="$COMMON_OUT_ROOT/agent-publish/$RID"
[[ -d "$AGENT_SCRATCH" ]] && rm -rf "$AGENT_SCRATCH"
AGENT_PUBLISH_PROPERTIES=(-p:IsPackagedBuild=true)
if [[ "$RID" == linux-musl-* ]]; then
  COPILOT_ARCH="${RID##*-}"
  dotnet restore "$REPO_ROOT/Agent/Core.Agent.csproj" --nologo
  COPILOT_CLI_VERSION="$(dotnet msbuild "$REPO_ROOT/Agent/Core.Agent.csproj" -getProperty:CopilotCliVersion -nologo)"
  if [[ -z "$COPILOT_CLI_VERSION" ]]; then
    echo "ERROR: Could not resolve CopilotCliVersion from GitHub.Copilot.SDK."
    exit 1
  fi
  COPILOT_CACHE_DIR="$COMMON_OUT_ROOT/copilot-cli/$COPILOT_CLI_VERSION/linuxmusl-$COPILOT_ARCH"
  COPILOT_BINARY="$COPILOT_CACHE_DIR/copilot"

  if [[ ! -f "$COPILOT_BINARY" ]]; then
    COPILOT_PACKAGE="copilot-linuxmusl-$COPILOT_ARCH"
    COPILOT_ARCHIVE="$COPILOT_CACHE_DIR/$COPILOT_PACKAGE-$COPILOT_CLI_VERSION.tgz"
    COPILOT_METADATA_URL="https://registry.npmjs.org/@github%2F$COPILOT_PACKAGE/$COPILOT_CLI_VERSION"
    COPILOT_URL="https://registry.npmjs.org/@github/$COPILOT_PACKAGE/-/$COPILOT_PACKAGE-$COPILOT_CLI_VERSION.tgz"
    mkdir -p "$COPILOT_CACHE_DIR"
    echo "Downloading Copilot CLI $COPILOT_CLI_VERSION for linuxmusl-$COPILOT_ARCH"
    curl --fail --location --retry 3 --output "$COPILOT_ARCHIVE" "$COPILOT_URL"
    COPILOT_ARCHIVE_SHASUM="$(curl --fail --location --retry 3 "$COPILOT_METADATA_URL" \
      | jq --exit-status --raw-output '.dist.shasum | select(type == "string" and test("^[0-9a-fA-F]{40}$"))')"
    echo "$COPILOT_ARCHIVE_SHASUM  $COPILOT_ARCHIVE" | sha1sum --check --strict
    tar -xzf "$COPILOT_ARCHIVE" --strip-components=1 -C "$COPILOT_CACHE_DIR" package/copilot
  fi

  AGENT_PUBLISH_PROPERTIES+=("-p:CopilotCliBinaryPath=$COPILOT_BINARY")
fi
dotnet publish "$REPO_ROOT/Agent/Core.Agent.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$AGENT_SCRATCH" \
  "${AGENT_PUBLISH_PROPERTIES[@]}"

# Publish the Agent.Commands extension into builtin-publish/ so the inject
# script picks it up in the same pass as Accounts/Config.
echo "Publishing Agent.Commands extension (Release)"
AGENT_EXT_DIR="$COMMON_OUT_ROOT/builtin-publish/Microsoft.Azure.Azclips.Extensions.Agent"
dotnet publish "$REPO_ROOT/Agent/Commands/Agent.Commands.csproj" \
  -c Release \
  -o "$AGENT_EXT_DIR"

chmod +x "$INJECTOR_SCRIPT"
OUT_ROOT="$OUT_ROOT" "$INJECTOR_SCRIPT" --publish-dir "$CLI_SC_DIR" --builtin-root "$COMMON_OUT_ROOT/builtin-publish"

# Stage agent daemon into the hybrid layout: launcher files land next to az,
# everything else lands under the freshly-injected agent extension directory
# (prompts, deps/ NuGet layout, native copilot).
STAGE_AGENT_SCRIPT="$PACKAGING_ROOT/common/stage_agent.sh"
AGENT_EXT_IN_PUBLISH="$CLI_SC_DIR/Extensions/Microsoft.Azure.Azclips.Extensions.Agent"
if [[ ! -d "$AGENT_EXT_IN_PUBLISH" ]]; then
    echo "Agent extension dir not present in publish after inject: $AGENT_EXT_IN_PUBLISH" >&2
    exit 1
fi
chmod +x "$STAGE_AGENT_SCRIPT"
"$STAGE_AGENT_SCRIPT" \
    --agent-publish-dir "$AGENT_SCRATCH" \
    --cli-publish-dir "$CLI_SC_DIR" \
    --agent-extension-dir "$AGENT_EXT_IN_PUBLISH"
cp "$NOTICE_FILE" "$CLI_SC_DIR/NOTICE"
cp "$LICENSE_FILE" "$CLI_SC_DIR/LICENSE"

mkdir -p "$(dirname "$CLI_TAR_PATH")"
rm -f "$CLI_TAR_PATH"

tar -czf "$CLI_TAR_PATH" -C "$CLI_SC_DIR" .
echo "Created $CLI_TAR_PATH"
