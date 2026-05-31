#!/usr/bin/env bash
set -euo pipefail

# build_linux_tarball.sh
# Builds Linux self-contained CLI, injects built-ins, and packages tar.gz.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/linux}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-linux-x64}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"

CLI_SC_DIR="$OUT_ROOT/publish/cli/$RID"
CLI_TAR_DIR="$OUT_ROOT/bin/Release"
CLI_TAR_PATH="${CLI_TAR_PATH:-$CLI_TAR_DIR/azv2-$RID.tar.gz}"

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
