#!/usr/bin/env bash
#
# azcli installer (Linux / macOS)
#
# Downloads the latest signed release of the az CLI from the azcli GitHub repo
# using your authenticated GitHub CLI (gh) session, extracts it, and installs
# the `az` command in isolation (PATH is NOT modified, so it coexists with the
# classic azure-cli).
#
# Private-preview users only need read access to the repo and a logged-in `gh`.
#
# Usage:
#   ./azcli-install.sh [--package lean|full]
#
# Environment overrides: AZCLI_PACKAGE
#
set -euo pipefail

# REPO and INSTALL_ROOT are fixed. Their AZCLI_REPO / AZCLI_INSTALL_ROOT env
# overrides exist for internal release testing only and are undocumented.
REPO="${AZCLI_REPO:-Azure/azclips}"
PACKAGE="${AZCLI_PACKAGE:-full}"          # full | lean
INSTALL_ROOT="${AZCLI_INSTALL_ROOT:-$HOME/.local/share/azcli}"

die() { echo "ERROR: $*" >&2; exit 1; }

usage() {
  sed -n '3,15p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package)
      [[ $# -ge 2 ]] || die "--package requires a value: lean or full."
      PACKAGE="$2"; shift 2;;
    -h|--help) usage;;
    *) die "Unknown argument: $1 (use --help)";;
  esac
done

case "$PACKAGE" in
  lean|full) ;;
  *) die "--package must be 'lean' or 'full'. Got: $PACKAGE";;
esac

# Refuse obviously unsafe install roots (guards against a mis-set AZCLI_INSTALL_ROOT).
case "${INSTALL_ROOT%/}" in
  ""|"/"|"$HOME"|/root|/home|/Users|/usr|/etc|/var|/bin) die "Refusing to use unsafe install root: '$INSTALL_ROOT'." ;;
esac

# 1. Require an authenticated GitHub CLI.
command -v gh >/dev/null 2>&1 || die "GitHub CLI 'gh' is required. Install it from https://cli.github.com/ and re-run."
if ! gh auth status >/dev/null 2>&1; then
  echo "You are not signed in to GitHub. Launching 'gh auth login'..."
  gh auth login
fi

# 2. Detect platform.
os="$(uname -s)"
machine="$(uname -m)"
case "$os" in
  Linux)  os_label="linux"; ext="tar.gz";;
  Darwin) os_label="macos"; ext="tar.gz";;
  *) die "Unsupported OS: $os. This installer supports Linux and macOS; use azcli-install.ps1 on Windows.";;
esac
case "$machine" in
  x86_64|amd64) if [[ "$os_label" == "macos" ]]; then arch_label="x86_64"; else arch_label="x64"; fi;;
  arm64|aarch64) arch_label="arm64";;
  *) die "Unsupported CPU architecture: $machine";;
esac

if [[ "$os_label" == "macos" && "$arch_label" == "x86_64" ]]; then
  die "macOS Intel (x86_64) is not supported. azcli requires an Apple Silicon (arm64) Mac."
fi

suffix="-${os_label}-${arch_label}.${ext}"
if [[ "$PACKAGE" == "full" ]]; then stem="azcli"; else stem="azcli-lean"; fi

echo "Repository : $REPO"
echo "Package    : $PACKAGE ($stem)"
echo "Platform   : $os_label-$arch_label"

# 3. Resolve the latest release tag.
tag="$(gh release view --repo "$REPO" --json tagName --jq '.tagName' 2>/dev/null || true)"
[[ -n "$tag" ]] || die "Could not resolve a release for $REPO. Confirm you have read access and that a release exists."
echo "Release    : $tag"

# 4. Choose the matching asset for this platform + package.
# (Portable read loop; macOS ships bash 3.2 which lacks `mapfile`.)
asset_names=()
while IFS= read -r name; do
  [[ -n "$name" ]] && asset_names+=("$name")
done < <(gh release view "$tag" --repo "$REPO" --json assets --jq '.assets[].name')
[[ ${#asset_names[@]} -gt 0 ]] || die "Release $tag has no downloadable assets."

chosen=""
for name in "${asset_names[@]}"; do
  [[ "$name" == *"$suffix" ]] || continue
  if [[ "$PACKAGE" == "lean" ]]; then
    [[ "$name" == azcli-lean-* ]] && { chosen="$name"; break; }
  else
    [[ "$name" == azcli-* && "$name" != azcli-lean-* ]] && { chosen="$name"; break; }
  fi
done

if [[ -z "$chosen" ]]; then
  echo "Available assets in $tag:" >&2
  printf '  %s\n' "${asset_names[@]}" >&2
  die "No $PACKAGE CLI asset found for $os_label-$arch_label in release $tag."
fi
echo "Asset      : $chosen"

# 5. Download via the authenticated gh session (handles private-repo access).
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
gh release download "$tag" --repo "$REPO" --pattern "$chosen" --dir "$tmp" --clobber
archive="$tmp/$chosen"
[[ -f "$archive" ]] || die "Download did not produce the expected file: $archive"

# 6. Install (clean replace of the install root).
if [[ -e "$INSTALL_ROOT" && ! -e "$INSTALL_ROOT/az" && -n "$(ls -A "$INSTALL_ROOT" 2>/dev/null)" ]]; then
  die "Install root '$INSTALL_ROOT' is not empty and does not look like an azcli install. Refusing to overwrite. Remove it and re-run."
fi
rm -rf "$INSTALL_ROOT"
mkdir -p "$INSTALL_ROOT"
tar -xzf "$archive" -C "$INSTALL_ROOT"
chmod +x "$INSTALL_ROOT/az" 2>/dev/null || true
[[ -f "$INSTALL_ROOT/az" ]] || die "Extraction did not yield an 'az' binary under $INSTALL_ROOT."

# 7. Verify.
az_bin="$INSTALL_ROOT/az"

echo
echo "Installed to: $INSTALL_ROOT"
if "$az_bin" --help >/dev/null 2>&1; then
  echo "Verified: '$az_bin --help' ran successfully."
else
  echo "Warning: '$az_bin --help' did not exit cleanly."
fi

echo
echo "Done. Installed in isolation — PATH was not modified (coexists with classic 'az')."
echo
echo "  Run it directly:                $INSTALL_ROOT/az --help"
echo "  Or add to PATH (this session):  export PATH=\"$INSTALL_ROOT:\$PATH\""
