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
#   --test-mi       After install, run the bundled live test suite (tests/Live/Live.Tests,
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

# Step timer. Lives beside this script both in the repo (scripts/lib) and in the
# bundle (publish-vm-mi.sh copies it next to us), so try both before giving up —
# timing is diagnostics, never a reason to fail the run.
_SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -f "$_SELF_DIR/timing.sh" ]]; then
  # shellcheck source=lib/timing.sh
  source "$_SELF_DIR/timing.sh"
elif [[ -f "$_SELF_DIR/lib/timing.sh" ]]; then
  # shellcheck source=lib/timing.sh
  source "$_SELF_DIR/lib/timing.sh"
else
  timing_start() { echo "==> $1 ..."; }
  timing_end() { :; }
  timing_record() { :; }
  timing_record_detail() { :; }
  timing_summary() { :; }
fi

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
  timing_start "extract archive"
  tar -xzf "$ARCHIVE" -C "$DEST"
  timing_end
  BUNDLE="$DEST/$TOP"
fi

if [[ -z "$BUNDLE" ]]; then
  echo "!! Provide --archive <file.tar.gz> or --bundle <dir>. See --help." >&2
  exit 1
fi

AZ="$BUNDLE/Core.Cli/az"
timing_start "install az (link + PATH)"
echo "    Wiring bundle: $BUNDLE"
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
"$AZ" --help >/dev/null 2>&1 && echo "    az runs OK." || echo "    !! az failed to run; check dependencies." >&2
timing_end

# Register the bundled non-core extensions (offline) so commands like
# 'az group' / 'az vm' work without a NuGet feed. Built-in verbs
# (extension/login/config) need nothing here.
PKG_DIR="$BUNDLE/packages"
shopt -s nullglob
pkgs=("$PKG_DIR"/*.nupkg)
shopt -u nullglob
if [[ ${#pkgs[@]} -gt 0 ]]; then
  echo
  timing_start "register bundled extensions"
  for pkg in "${pkgs[@]}"; do
    name="$(basename "$pkg")"
    if "$AZ" extension add --path "$pkg" >/dev/null 2>&1; then
      echo "    + $name"
    else
      echo "    !! failed to register $name (continuing)" >&2
    fi
  done
  timing_end
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

  # The assertions live in tests/Live/Live.Tests (xUnit v3), published self-contained
  # into the bundle. Keeping them in C# rather than here gives real assertion
  # messages, structured JSON parsing and per-case pass/fail reporting; this
  # script only supplies the environment and propagates the exit code.
  RUNNER="$BUNDLE/Live.Tests/Live.Tests"
  if [[ ! -x "$RUNNER" ]]; then
    echo "!! Live test runner not found at $RUNNER" >&2
    echo "   Rebuild the bundle with tests/Live/Scripts/publish-vm-mi.sh." >&2
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

  # -xml gives per-test durations, which is the only way to see which live test
  # is actually slow — the console reporter prints just a total.
  XML_REPORT="$(mktemp -t live-tests-XXXXXX.xml)"
  trap 'rm -f "$XML_REPORT"' EXIT

  rc=0
  timing_start "run live tests"
  "$RUNNER" -xml "$XML_REPORT" || rc=$?
  timing_end

  # xUnit writes the report as one long line, so split on '<' before matching.
  # Diagnostics only: a malformed/absent report must never change the verdict.
  if [[ -s "$XML_REPORT" ]]; then
    while IFS= read -r el; do
      t="$(printf '%s' "$el" | grep -o ' time="[^"]*"' | head -n1 | cut -d'"' -f2)"
      m="$(printf '%s' "$el" | grep -o ' method="[^"]*"' | head -n1 | cut -d'"' -f2)"
      c="$(printf '%s' "$el" | grep -o ' type="[^"]*"' | head -n1 | cut -d'"' -f2)"
      [[ -z "$t" || -z "$m" ]] && continue
      timing_record_detail "test: ${c##*.}.${m}" "$(awk -v s="$t" 'BEGIN { printf "%d", s * 1000 }')"
    done < <(tr '<' '\n' < "$XML_REPORT" | grep '^test ' || true)
  fi

  if [[ $rc -ne 0 ]]; then
    timing_summary "VM-side (setup-vm-mi.sh)"
    exit $rc
  fi

  echo "    All MI live tests passed."
  echo
fi

timing_summary "VM-side (setup-vm-mi.sh)"
echo "==> Done."
