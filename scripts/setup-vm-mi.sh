#!/usr/bin/env bash
#
# setup-vm-mi.sh — Install a self-contained azclips bundle onto an Azure VM (Linux).
#
# A self-contained az bundle needs no prerequisites (no .NET runtime, no
# Azure CLI). This script extracts the bundle (if given a .tar.gz), adds az
# to your PATH, and verifies it runs.
#
# Usage (run ON the VM):
#   ./setup-vm-mi.sh --archive <file.tar.gz> [--dest <dir>]
#   ./setup-vm-mi.sh --bundle  <dir>
#
#   --archive FILE  A azclips .tar.gz bundle; it is extracted, then wired up
#   --dest DIR      Where to extract the archive (default: current directory)
#   --bundle DIR    An already-extracted bundle; adds az to PATH via symlink
#   --test-mi       After install, run the bundled live test suite (tests/Live.Tests,
#                   xUnit v3, published self-contained) to verify managed-identity
#                   auth on this VM by
#                   running `login --identity` + a `vm show` smoke test. Always
#                   tests the system-assigned identity, and tests the user-assigned
#                   identity via any of the selectors below that are supplied.
#   --vm-name NAME  VM name for the --test-mi smoke test (required with --test-mi)
#   --resource-group RG
#                   Resource group for the --test-mi smoke test (required with
#                   --test-mi). Names are per-developer, so there is no default;
#                   the orchestrator passes the Terraform outputs.
#   --subscription-id ID
#                   Subscription containing the VM under test
#   --uami-client-id ID
#                   Verify `login --identity --client-id ID` + vm show (user-assigned)
#   --uami-resource-id ID
#                   Verify `login --identity --resource-id ID` + vm show (user-assigned)
#   --uami-object-id ID
#                   Verify `login --identity --object-id ID` + vm show (user-assigned)
#
# Examples:
#   ./setup-vm-mi.sh --archive ~/azclips-Release-linux-x64.tar.gz
#   ./setup-vm-mi.sh --archive ~/azclips-Release-linux-x64.tar.gz --dest ~/apps
#   ./setup-vm-mi.sh --bundle  ~/Release-linux-x64
#   ./setup-vm-mi.sh --bundle  ~/Release-linux-x64 --test-mi
#
set -euo pipefail

ARCHIVE=""
DEST=""
BUNDLE=""
RUN_TEST=0
VM_NAME=""
RESOURCE_GROUP=""
SUBSCRIPTION_ID=""
UAMI_CLIENT_ID=""
UAMI_RESOURCE_ID=""
UAMI_OBJECT_ID=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --archive) ARCHIVE="$2"; shift 2 ;;
    --dest)    DEST="$2"; shift 2 ;;
    --bundle)  BUNDLE="$2"; shift 2 ;;
    --test-mi) RUN_TEST=1; shift ;;
    --vm-name) VM_NAME="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --subscription-id) SUBSCRIPTION_ID="$2"; shift 2 ;;
    --uami-client-id)   UAMI_CLIENT_ID="$2"; shift 2 ;;
    --uami-resource-id) UAMI_RESOURCE_ID="$2"; shift 2 ;;
    --uami-object-id)   UAMI_OBJECT_ID="$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -n 34
      exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

. /etc/os-release 2>/dev/null || true
echo "==> Detected: ${PRETTY_NAME:-unknown}"

if [[ -n "$ARCHIVE" && -n "$BUNDLE" ]]; then
  echo "!! Use either --archive or --bundle, not both. See --help." >&2
  exit 1
fi

# Extract a .tar.gz archive and resolve the bundle directory it contains.
if [[ -n "$ARCHIVE" ]]; then
  if [[ ! -f "$ARCHIVE" ]]; then
    echo "!! Archive not found: $ARCHIVE" >&2
    exit 1
  fi
  [[ -z "$DEST" ]] && DEST="$PWD"
  mkdir -p "$DEST"

  # Top-level folder inside the archive (e.g. "Release-linux-x64").
  # head closes the pipe early, so disable pipefail locally to avoid SIGPIPE.
  TOP="$(set +o pipefail; tar -tzf "$ARCHIVE" | head -n1 | cut -d/ -f1)"
  if [[ -z "$TOP" ]]; then
    echo "!! Could not read archive contents: $ARCHIVE" >&2
    exit 1
  fi

  echo "==> Extracting $ARCHIVE -> $DEST"
  tar -xzf "$ARCHIVE" -C "$DEST"
  BUNDLE="$DEST/$TOP"
fi

if [[ -z "$BUNDLE" ]]; then
  echo "!! Provide --archive <file.tar.gz> or --bundle <dir>. See --help." >&2
  exit 1
fi

AZ="$BUNDLE/Core.Cli/az"
echo "==> Wiring bundle: $BUNDLE"
if [[ ! -x "$AZ" ]]; then
  echo "    !! az not found/executable at $AZ" >&2
  exit 1
fi
chmod +x "$AZ"
mkdir -p "$HOME/.local/bin"
ln -sf "$AZ" "$HOME/.local/bin/az"
echo "    Linked az -> ~/.local/bin/az"
case ":$PATH:" in
  *":$HOME/.local/bin:"*) ;;
  *) echo "    Note: add ~/.local/bin to PATH (echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.bashrc)";;
esac
echo
"$AZ" --help >/dev/null 2>&1 && echo "    az runs OK." || echo "    !! az failed to run; check dependencies." >&2

# Register the bundled non-core extensions (offline) so commands like
# 'az group' / 'az vm' work without a NuGet feed. Built-in verbs
# (extension/login/config) need nothing here.
PKG_DIR="$BUNDLE/packages"
shopt -s nullglob
pkgs=("$PKG_DIR"/*.nupkg)
shopt -u nullglob
if [[ ${#pkgs[@]} -gt 0 ]]; then
  echo
  echo "==> Registering bundled extensions ..."
  for pkg in "${pkgs[@]}"; do
    name="$(basename "$pkg")"
    if "$AZ" extension add --path "$pkg" >/dev/null 2>&1; then
      echo "    + $name"
    else
      echo "    !! failed to register $name (continuing)" >&2
    fi
  done
fi

echo
if [[ "$RUN_TEST" -eq 1 ]]; then
  if [[ -z "$SUBSCRIPTION_ID" ]]; then
    echo "!! --subscription-id is required with --test-mi." >&2
    exit 1
  fi

  # No defaults: resource names are per-developer (alias-derived), so a guessed
  # name would target somebody else's VM or nothing at all.
  if [[ -z "$VM_NAME" || -z "$RESOURCE_GROUP" ]]; then
    echo "!! --vm-name and --resource-group are required with --test-mi." >&2
    exit 1
  fi

  echo "==> --test-mi: verifying managed-identity auth on this VM"

  # The assertions live in tests/Live.Tests (xUnit v3), published self-contained
  # into the bundle. Keeping them in C# rather than here gives real assertion
  # messages, structured JSON parsing and per-case pass/fail reporting; this
  # script only supplies the environment and propagates the exit code.
  RUNNER="$BUNDLE/Live.Tests/Live.Tests"
  if [[ ! -x "$RUNNER" ]]; then
    echo "!! Live test runner not found at $RUNNER" >&2
    echo "   Rebuild the bundle with scripts/publish-vm-mi.sh." >&2
    exit 1
  fi

  export AZCLIPS_LIVE_TEST=1
  # Absolute: the runner is started from its own directory, so a relative path
  # (the common case, since --bundle is usually relative) would not resolve.
  export AZCLIPS_LIVE_AZ
  AZCLIPS_LIVE_AZ="$(readlink -f "$AZ")"
  export AZCLIPS_LIVE_SUBSCRIPTION_ID="$SUBSCRIPTION_ID"
  export AZCLIPS_LIVE_VM_NAME="$VM_NAME"
  export AZCLIPS_LIVE_RESOURCE_GROUP="$RESOURCE_GROUP"
  export AZCLIPS_LIVE_UAMI_CLIENT_ID="$UAMI_CLIENT_ID"
  export AZCLIPS_LIVE_UAMI_RESOURCE_ID="$UAMI_RESOURCE_ID"
  export AZCLIPS_LIVE_UAMI_OBJECT_ID="$UAMI_OBJECT_ID"

  "$RUNNER"

  echo "    All MI live tests passed."
  echo
fi

echo "==> Done."
