#!/usr/bin/env bash
set -euo pipefail

# build_win_zip.sh
# Builds Windows self-contained CLI, injects built-ins, and packages zip.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/windows}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-win-x64}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"

CLI_WIN_SC_DIR="$OUT_ROOT/publish/cli/$RID"
CLI_ZIP_DIR="$OUT_ROOT/bin/Release"
CLI_WIN_ZIP_PATH="${CLI_WIN_ZIP_PATH:-$CLI_ZIP_DIR/azv2-$RID.zip}"

if [[ ! -f "$INJECTOR_SCRIPT" ]]; then
  echo "ERROR: Injector script not found at $INJECTOR_SCRIPT"
  exit 1
fi

echo "Publishing CLI (Release, $RID)"
dotnet publish "$CLI_PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$CLI_WIN_SC_DIR"

chmod +x "$INJECTOR_SCRIPT"
OUT_ROOT="$OUT_ROOT" "$INJECTOR_SCRIPT" --publish-dir "$CLI_WIN_SC_DIR" --builtin-root "$COMMON_OUT_ROOT/builtin-publish"

if ! command -v zip >/dev/null 2>&1; then
  echo "ERROR: 'zip' command not found. Install zip to produce $CLI_WIN_ZIP_PATH"
  exit 1
fi

mkdir -p "$(dirname "$CLI_WIN_ZIP_PATH")"
rm -f "$CLI_WIN_ZIP_PATH"
(cd "$CLI_WIN_SC_DIR" && zip -rq "$CLI_WIN_ZIP_PATH" .)
echo "Created $CLI_WIN_ZIP_PATH"
