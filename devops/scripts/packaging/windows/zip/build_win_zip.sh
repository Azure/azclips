#!/usr/bin/env bash
set -euo pipefail

# build_win_zip.sh
# Builds Windows self-contained CLI, injects built-ins, and packages zip.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/windows}"
COMMON_OUT_ROOT="${COMMON_OUT_ROOT:-$REPO_ROOT/devops/out/common}"
CLI_PROJECT="${CLI_PROJECT:-src/CLI/Core.Cli.csproj}"
RID="${RID:-win-x64}"
INJECTOR_SCRIPT="$PACKAGING_ROOT/common/inject_builtin_extensions.sh"

CLI_WIN_SC_DIR="$OUT_ROOT/publish/cli/$RID"
CLI_WIN_ZIP_PATH="${CLI_WIN_ZIP_PATH:-$OUT_ROOT/Release/zip/azv2-$RID.zip}"

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

mkdir -p "$(dirname "$CLI_WIN_ZIP_PATH")"
rm -f "$CLI_WIN_ZIP_PATH"

if command -v zip >/dev/null 2>&1; then
  (cd "$CLI_WIN_SC_DIR" && zip -rq "$CLI_WIN_ZIP_PATH" .)
elif command -v pwsh >/dev/null 2>&1 || command -v powershell.exe >/dev/null 2>&1; then
  # On hosted Windows agents, zip may be missing; fall back to PowerShell's Compress-Archive.
  if command -v pwsh >/dev/null 2>&1; then
    PS_BIN="pwsh"
  else
    PS_BIN="powershell.exe"
  fi

  to_ps_path() {
    if command -v cygpath >/dev/null 2>&1; then
      cygpath -w "$1"
    else
      echo "$1"
    fi
  }

  CLI_WIN_SC_DIR_PS="$(to_ps_path "$CLI_WIN_SC_DIR")"
  CLI_WIN_ZIP_PATH_PS="$(to_ps_path "$CLI_WIN_ZIP_PATH")"

  "$PS_BIN" -NoLogo -NoProfile -Command "Compress-Archive -Path '${CLI_WIN_SC_DIR_PS}\\*' -DestinationPath '${CLI_WIN_ZIP_PATH_PS}' -Force"
else
  echo "ERROR: Neither 'zip' nor PowerShell is available to produce $CLI_WIN_ZIP_PATH"
  exit 1
fi

echo "Created $CLI_WIN_ZIP_PATH"
