#!/usr/bin/env bash
set -euo pipefail

# Builds the canonical Linux CLI payload consumed by tarball and native package builders.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/linux}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-linux-x64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
PACKAGE_PROFILE="$(echo "$PACKAGE_PROFILE" | tr '[:upper:]' '[:lower:]')"
PAYLOAD_DIR="${PAYLOAD_DIR:-$OUT_ROOT/publish/cli/$RID}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"
STAGE_AGENT_SCRIPT="$PACKAGING_ROOT/common/stage_agent.sh"
MANIFEST_TOOL="$SCRIPT_DIR/create_payload_manifest.py"
PAYLOAD_VALIDATOR="$SCRIPT_DIR/validate_linux_payload.sh"
NOTICE_FILE="${NOTICE_FILE:-$REPO_ROOT/NOTICE}"
LICENSE_FILE="${LICENSE_FILE:-$REPO_ROOT/LICENSE}"
PAYLOAD_MANIFEST="${PAYLOAD_MANIFEST:-$OUT_ROOT/manifests/cli/$RID/$PACKAGE_PROFILE/payload-manifest.json}"

case "$PACKAGE_PROFILE" in
  lean|full) ;;
  *)
    echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
    exit 1
    ;;
esac

for required_file in "$INJECTOR_SCRIPT" "$STAGE_AGENT_SCRIPT" "$MANIFEST_TOOL" "$PAYLOAD_VALIDATOR" "$NOTICE_FILE" "$LICENSE_FILE"; do
  if [[ ! -f "$required_file" ]]; then
    echo "ERROR: Required file was not found: $required_file"
    exit 1
  fi
done

[[ -d "$PAYLOAD_DIR" ]] && rm -rf "$PAYLOAD_DIR"

echo "Publishing CLI payload (Release, $RID, $PACKAGE_PROFILE)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$PAYLOAD_DIR"

echo "Publishing Agent exe (Release, $RID) into scratch"
AGENT_SCRATCH="$COMMON_OUT_ROOT/agent-publish/$RID"
[[ -d "$AGENT_SCRATCH" ]] && rm -rf "$AGENT_SCRATCH"
dotnet publish "$REPO_ROOT/Agent/Core.Agent.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$AGENT_SCRATCH" \
  -p:IsPackagedBuild=true

echo "Publishing Agent.Commands extension (Release)"
AGENT_EXT_DIR="$COMMON_OUT_ROOT/builtin-publish/Microsoft.Azure.Azclips.Extensions.Agent"
dotnet publish "$REPO_ROOT/Agent/Commands/Agent.Commands.csproj" \
  -c Release \
  -o "$AGENT_EXT_DIR"

OUT_ROOT="$OUT_ROOT" PACKAGE_PROFILE="$PACKAGE_PROFILE" \
  bash "$INJECTOR_SCRIPT" --publish-dir "$PAYLOAD_DIR" --builtin-root "$COMMON_OUT_ROOT/builtin-publish"

AGENT_EXT_IN_PAYLOAD="$PAYLOAD_DIR/Extensions/Microsoft.Azure.Azclips.Extensions.Agent"
if [[ ! -d "$AGENT_EXT_IN_PAYLOAD" ]]; then
  echo "ERROR: Agent extension dir not present after inject: $AGENT_EXT_IN_PAYLOAD" >&2
  exit 1
fi

bash "$STAGE_AGENT_SCRIPT" \
  --agent-publish-dir "$AGENT_SCRATCH" \
  --cli-publish-dir "$PAYLOAD_DIR" \
  --agent-extension-dir "$AGENT_EXT_IN_PAYLOAD"

cp "$NOTICE_FILE" "$PAYLOAD_DIR/NOTICE"
cp "$LICENSE_FILE" "$PAYLOAD_DIR/LICENSE"

python3 "$MANIFEST_TOOL" generate \
  --payload-dir "$PAYLOAD_DIR" \
  --manifest "$PAYLOAD_MANIFEST" \
  --rid "$RID" \
  --profile "$PACKAGE_PROFILE"

bash "$PAYLOAD_VALIDATOR" \
  --payload-dir "$PAYLOAD_DIR" \
  --manifest "$PAYLOAD_MANIFEST"

echo "Created canonical Linux payload at $PAYLOAD_DIR"