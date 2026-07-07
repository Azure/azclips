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
FORCE=0

die() { echo "ERROR: $*" >&2; exit 1; }

usage() {
  sed -n '3,8p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force) FORCE=1; shift;;
    -h|--help) usage;;
    *) die "Unknown argument: $1 (use --help)";;
  esac
done

echo "Install root: $INSTALL_ROOT"

# Remove the install directory.
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

echo
echo "Done."
