#!/usr/bin/env bash
#
# azcli installer (Linux / macOS)
#
# Downloads the latest signed release of the az CLI from the azcli GitHub repo,
# then extracts and installs the `az` command in isolation (PATH is NOT
# modified, so it coexists with the classic azure-cli).
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
# Internal only: --archive installs an exact local artifact for CI and
# release-candidate validation before GitHub publication.
ARCHIVE_PATH=""
tmp=""
stage_root=""

die() { echo "ERROR: $*" >&2; exit 1; }

cleanup() {
  [[ -z "$stage_root" ]] || rm -rf "$stage_root"
  [[ -z "$tmp" ]] || rm -rf "$tmp"
}
trap cleanup EXIT

usage() {
  sed -n '3,15p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package)
      [[ $# -ge 2 ]] || die "--package requires a value: lean or full."
      PACKAGE="$2"; shift 2;;
    --archive)
      [[ $# -ge 2 ]] || die "--archive requires a path."
      [[ -n "$2" ]] || die "--archive requires a path."
      ARCHIVE_PATH="$2"; shift 2;;
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

# 1. Detect platform.
os="$(uname -s)"
machine="$(uname -m)"
case "$os" in
  Linux)
    platform="linux"
    libc_info=""
    if command -v ldd >/dev/null 2>&1; then
      libc_info="$(ldd --version 2>&1 || true)"
    fi
    if [[ -f /etc/alpine-release ]] || compgen -G '/lib/ld-musl-*.so.1' >/dev/null || grep -qi musl <<<"$libc_info"; then
      platform="linux-musl"
    fi
    ext="tar.gz"
    ;;
  Darwin) platform="macos"; ext="tar.gz";;
  *) die "Unsupported OS: $os. This installer supports Linux and macOS; use azcli-install.ps1 on Windows.";;
esac
case "$machine" in
  x86_64|amd64) architecture="x64";;
  arm64|aarch64) architecture="arm64";;
  *) die "Unsupported CPU architecture: $machine";;
esac

case "$platform-$architecture" in
  linux-x64) asset_label="linux-x64";;
  linux-arm64) asset_label="linux-arm64";;
  linux-musl-x64) asset_label="linux-musl-x64";;
  linux-musl-arm64) asset_label="linux-musl-arm64";;
  macos-x64) asset_label="macos-x86_64";;
  macos-arm64) asset_label="macos-arm64";;
  *) die "Unsupported platform: $platform-$architecture";;
esac

suffix="-${asset_label}.${ext}"
if [[ "$PACKAGE" == "full" ]]; then stem="azcli"; else stem="azcli-lean"; fi

echo "Repository : $REPO"
echo "Package    : $PACKAGE ($stem)"
echo "Platform   : $asset_label"

if [[ -n "$ARCHIVE_PATH" ]]; then
  [[ -f "$ARCHIVE_PATH" ]] || die "Archive was not found: $ARCHIVE_PATH"
  archive="$(cd "$(dirname "$ARCHIVE_PATH")" && pwd)/$(basename "$ARCHIVE_PATH")"
  archive_name="$(basename "$archive")"
  [[ "$archive_name" == *"$suffix" ]] || die "Archive '$archive_name' does not match platform $asset_label."
  if [[ "$PACKAGE" == "lean" ]]; then
    [[ "$archive_name" == "$stem"-* ]] || die "Archive '$archive_name' is not a lean CLI package."
  else
    [[ "$archive_name" == "$stem"-* && "$archive_name" != azcli-lean-* ]] || die "Archive '$archive_name' is not a full CLI package."
  fi
  echo "Archive    : $archive"
else
  # 2. Require an authenticated GitHub CLI.
  command -v gh >/dev/null 2>&1 || die "GitHub CLI 'gh' is required. Install it from https://cli.github.com/ and re-run."
  if ! gh auth status >/dev/null 2>&1; then
    echo "You are not signed in to GitHub. Launching 'gh auth login'..."
    gh auth login
  fi

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
    die "No $PACKAGE CLI asset found for $asset_label in release $tag."
  fi
  echo "Asset      : $chosen"

  # 5. Download via the authenticated gh session (handles private-repo access).
  tmp="$(mktemp -d)"
  gh release download "$tag" --repo "$REPO" --pattern "$chosen" --dir "$tmp" --clobber
  archive="$tmp/$chosen"
  [[ -f "$archive" ]] || die "Download did not produce the expected file: $archive"
fi

# 6. Install (clean replace of the install root).
if [[ -e "$INSTALL_ROOT" && ! -e "$INSTALL_ROOT/az" && -n "$(ls -A "$INSTALL_ROOT" 2>/dev/null)" ]]; then
  die "Install root '$INSTALL_ROOT' is not empty and does not look like an azcli install. Refusing to overwrite. Remove it and re-run."
fi
install_parent="$(dirname "$INSTALL_ROOT")"
mkdir -p "$install_parent"
stage_root="$(mktemp -d "$install_parent/.azcli-stage.XXXXXX")"
tar -xzf "$archive" -C "$stage_root"
chmod +x "$stage_root/az" 2>/dev/null || true
[[ -f "$stage_root/az" ]] || die "Extraction did not yield an 'az' binary."
"$stage_root/az" --help >/dev/null 2>&1 || die "Extracted 'az --help' did not exit cleanly."

rm -rf "$INSTALL_ROOT"
mv "$stage_root" "$INSTALL_ROOT"
stage_root=""

# 7. Verify.
az_bin="$INSTALL_ROOT/az"

echo
echo "Installed to: $INSTALL_ROOT"
echo "Verified: '$az_bin --help' ran successfully."

echo
echo "Done. Installed in isolation — PATH was not modified (coexists with classic 'az')."
echo
echo "  Run it directly:                $INSTALL_ROOT/az --help"
echo "  Or add to PATH (this session):  export PATH=\"$INSTALL_ROOT:\$PATH\""
