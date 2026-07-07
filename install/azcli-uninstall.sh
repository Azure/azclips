#!/usr/bin/env bash
#
# azcli uninstaller (Linux / macOS)
#
# Removes an azcli install created by azcli-install.sh (deletes the install folder).
#
# Usage:
#   ./azcli-uninstall.sh [--force]
#
set -euo pipefail

# INSTALL_ROOT is the fixed default; the AZCLI_INSTALL_ROOT env override is internal.
INSTALL_ROOT="${AZCLI_INSTALL_ROOT:-$HOME/.local/share/azcli}"
CLEAN_PATH=1
FORCE=0

die() { echo "ERROR: $*" >&2; exit 1; }

usage() {
  sed -n '3,8p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --keep-path) CLEAN_PATH=0; shift;;
    --force) FORCE=1; shift;;
    -h|--help) usage;;
    *) die "Unknown argument: $1 (use --help)";;
  esac
done

echo "Install root: $INSTALL_ROOT"

# 1. Remove the install directory.
if [[ -e "$INSTALL_ROOT" ]]; then
  if [[ -f "$INSTALL_ROOT/az" || "$FORCE" -eq 1 ]]; then
    rm -rf "$INSTALL_ROOT"
    echo "Removed $INSTALL_ROOT"
  else
    die "'$INSTALL_ROOT' does not look like an azcli install (no 'az' binary). Re-run with --force to remove it anyway."
  fi
else
  echo "Nothing to remove at $INSTALL_ROOT (already absent)."
fi

# 2. Remove the symlink from ~/.local/bin.
BIN_DIR="${AZCLI_BIN_DIR:-$HOME/.local/bin}"
link="$BIN_DIR/az"
if [[ -L "$link" ]]; then
  target="$(readlink "$link" 2>/dev/null || true)"
  if [[ "$target" == "$INSTALL_ROOT/az" || "$FORCE" -eq 1 ]]; then
    rm -f "$link"
    echo "Removed symlink $link"
  fi
fi

# 3. Remove PATH entries the installer added to shell rc files.
if [[ "$CLEAN_PATH" -eq 1 ]]; then
  for rc in "$HOME/.zshrc" "$HOME/.bashrc" "$HOME/.profile"; do
    [[ -f "$rc" ]] || continue
    tmp="$(mktemp)"
    grep -vx '# azcli' "$rc" \
      | grep -vx '# azclips CLI' \
      | grep -vxF "export PATH=\"$BIN_DIR:\$PATH\"" \
      | grep -vxF "export PATH=\"$INSTALL_ROOT:\$PATH\"" > "$tmp" || true
    if ! cmp -s "$tmp" "$rc"; then
      cp "$rc" "$rc.azcli.bak"
      mv "$tmp" "$rc"
      echo "Cleaned azcli PATH entry from $rc (backup: $rc.azcli.bak)"
    else
      rm -f "$tmp"
    fi
  done
fi

echo
echo "Done."
