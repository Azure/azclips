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
#   --test-mi       After install, verify managed-identity auth on this VM by
#                   running `login --identity` + a `vm show` smoke test. Always
#                   tests the system-assigned identity, and tests the user-assigned
#                   identity via any of the selectors below that are supplied.
#   --vm-name NAME  VM name for the --test-mi smoke test (default: test-vm-mi)
#   --resource-group RG
#                   Resource group for the --test-mi smoke test (default: ManagedIdentityLiveTest)
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
VM_NAME="test-vm-mi"
RESOURCE_GROUP="ManagedIdentityLiveTest"
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

  echo "==> --test-mi: verifying managed-identity auth on this VM"

  # Verify one MI login style end-to-end: clear context, `login --identity` with
  # the given selector args, then `vm show` must succeed AND return the expected
  # VM. Any failure returns non-zero so the caller (set -e) aborts loudly.
  mi_check() {
    local label="$1"; shift
    echo "    [$label] login --identity ${*:-(system-assigned)}"
    "$AZ" login logout >/dev/null 2>&1 || true
    if ! "$AZ" login --identity --subscription-id "$SUBSCRIPTION_ID" "$@" >/dev/null 2>&1; then
      echo "    !! [$label] 'login --identity $*' failed" >&2
      return 1
    fi
    local attempt out
    for attempt in {1..12}; do
      if out="$("$AZ" vm show --name "${VM_NAME}" --resource-group "${RESOURCE_GROUP}" 2>&1)"; then
        break
      fi
      if [[ "$attempt" -eq 12 ]]; then
        echo "    !! [$label] vm show failed:" >&2
        echo "$out" | sed 's/^/       /' >&2
        return 1
      fi
      sleep 10
    done
    if ! grep -q "\"name\": \"${VM_NAME}\"" <<<"$out"; then
      echo "    !! [$label] vm show did not return '${VM_NAME}':" >&2
      echo "$out" | sed 's/^/       /' >&2
      return 1
    fi
    echo "    [$label] OK — vm show returned '${VM_NAME}'"
  }

  # System-assigned identity (plain `login --identity`).
  mi_check "system-assigned"

  # User-assigned identity, once per selector supplied (sourced from terraform
  # output by the orchestrator, mi-e2e-test.sh).
  if [[ -n "$UAMI_CLIENT_ID" ]]; then
    mi_check "user-assigned/client-id" --client-id "$UAMI_CLIENT_ID"
  fi
  if [[ -n "$UAMI_RESOURCE_ID" ]]; then
    mi_check "user-assigned/resource-id" --resource-id "$UAMI_RESOURCE_ID"
  fi
  if [[ -n "$UAMI_OBJECT_ID" ]]; then
    mi_check "user-assigned/object-id" --object-id "$UAMI_OBJECT_ID"
  fi

  echo "    All MI smoke tests passed."
  echo
fi

echo "==> Done."
