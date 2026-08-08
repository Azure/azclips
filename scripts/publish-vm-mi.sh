#!/usr/bin/env bash
#
# publish-vm-mi.sh — Build a self-contained, copy-and-run azclips bundle for a VM.
#
# Produces a folder that mirrors the runtime layout azclips expects:
#
#   <out>/
#     Core.Cli/az                                  <- self-contained host (bundles .NET)
#     Microsoft.Azure.Azclips.Extensions.Extension/   <- loose folder per BUILT-IN extension
#     Microsoft.Azure.Azclips.Extensions.Login/       <- (extension, login, config only)
#     Microsoft.Azure.Azclips.Extensions.Config/
#     packages/*.nupkg                               <- NON-built-in extensions (offline install)
#     Live.Tests/Live.Tests                          <- self-contained xUnit v3 live test runner
#     setup-vm-mi.sh
#     timing.sh                                      <- step timer sourced by setup-vm-mi.sh
#
# The CLI is published --self-contained so the target VM needs NO .NET runtime
# installed. Extensions are published framework-dependent (they load into the
# az process, which already provides Core.* and ExtensionRuntime).
#
# Only built-in (always-on) extensions get a loose <out>/<ExtName>/ folder, since the
# host seeds their verbs in-memory and BuiltinExtensionLoader loads them straight from the
# bundle. Non-built-in extensions are shipped only as nupkg under packages/ and are made
# routable on the VM via `az extension add --path` (a loose folder for them is unused).
#
# Usage:
#   scripts/publish-vm-mi.sh [--rid <rid>] [--config <Debug|Release>] [--out <dir>] [--tar]
#
# Examples:
#   scripts/publish-vm-mi.sh                              # linux-x64, Release
#   scripts/publish-vm-mi.sh --rid win-x64               # Windows x64
#   scripts/publish-vm-mi.sh --rid linux-arm64 --tar     # also produce a .tar.gz
#
set -euo pipefail

RID="linux-x64"
CONFIG="Release"
OUT=""
MAKE_TAR=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)    RID="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --out)    OUT="$2"; shift 2 ;;
    --tar)    MAKE_TAR=1; shift ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -n 30
      exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# shellcheck source=lib/timing.sh
source "$REPO_ROOT/scripts/lib/timing.sh"

[[ -z "$OUT" ]] && OUT="$REPO_ROOT/publish/vm/${CONFIG}-${RID}"

echo "==> Repo:   $REPO_ROOT"
echo "==> RID:    $RID"
echo "==> Config: $CONFIG"
echo "==> Out:    $OUT"
echo

rm -rf "$OUT"
mkdir -p "$OUT"

# 1) Self-contained CLI host -> <out>/Core.Cli. The apphost is named 'az'
#    (Core.Cli <AssemblyName>) and published self-contained so the target VM
#    needs no .NET runtime installed.
echo
timing_start "publish CLI (self-contained)"
dotnet publish src/CLI/Core.Cli.csproj \
  -c "$CONFIG" -r "$RID" --self-contained true \
  -o "$OUT/Core.Cli"
timing_end

# Built-in (always-on) extensions. Their verbs are seeded in-memory by the host
# (Core.Extensions.BuiltInExtensions) and loaded directly from their loose <out>/<ExtName>/
# folders by BuiltinExtensionLoader — so ONLY these need loose-folder output, and they MUST
# NOT be packed/registered as nupkg. Everything else is registered offline from a nupkg.
BUILTIN_IDS="Microsoft.Azure.Azclips.Extensions.Extension Microsoft.Azure.Azclips.Extensions.Login Microsoft.Azure.Azclips.Extensions.Config"

# 2) Built-in extensions only (framework-dependent) -> <out>/<ExtName>
#    Folder name MUST equal the extension's <ExtName> so BuiltinExtensionLoader finds it.
#    Non-built-in extensions are intentionally skipped here: the dispatcher will not route
#    to them until they are registered from their nupkg (step 3), after which they load from
#    the versioned install dir — so a loose folder for them would be dead weight.
echo
timing_start "publish built-in extensions"
while IFS= read -r csproj; do
  ext_name="$(grep -oPm1 '(?<=<ExtName>).*?(?=</ExtName>)' "$csproj" || true)"
  if [[ -z "$ext_name" ]]; then
    echo "    !! Skipping $csproj (no <ExtName>)"
    continue
  fi
  case " $BUILTIN_IDS " in
    *" $ext_name "*) ;;
    *) echo "    - skip $ext_name (non-built-in; installed from nupkg)"; continue ;;
  esac
  echo "    - $ext_name"
  dotnet publish "$csproj" \
    -c "$CONFIG" -r "$RID" --no-self-contained \
    -o "$OUT/$ext_name" >/dev/null
done < <(find src/Extensions -maxdepth 2 -name 'Extensions.*.csproj' -not -path '*/obj/*')
timing_end

# 3) Pack the NON-built-in extensions as .nupkg into <out>/packages so they can be
#    registered offline on the VM (az extension add --path). 'extension', 'login'
#    and 'config' are built-in (always-on) and MUST NOT be registered this way.
echo
timing_start "pack extensions for offline install"
mkdir -p "$OUT/packages"
while IFS= read -r csproj; do
  ext_name="$(grep -oPm1 '(?<=<ExtName>).*?(?=</ExtName>)' "$csproj" || true)"
  [[ -z "$ext_name" ]] && continue
  case " $BUILTIN_IDS " in
    *" $ext_name "*) echo "    - skip $ext_name (built-in)"; continue ;;
  esac
  echo "    - pack $ext_name"
  # Build first so the runtime config/deps files exist, then pack with
  # --no-build. A bare `dotnet pack` from a clean tree fails with NU5026
  # (missing *.runtimeconfig.json) because pack does not emit it on its own.
  dotnet build "$csproj" -c "$CONFIG" >/dev/null
  dotnet pack "$csproj" -c "$CONFIG" --no-build -o "$OUT/packages" >/dev/null
done < <(find src/Extensions -maxdepth 2 -name 'Extensions.*.csproj' -not -path '*/obj/*')
timing_end

# 4) Live test suite -> <out>/Live.Tests. Published SELF-CONTAINED because the VM
#    has no .NET SDK and so cannot run `dotnet test`; xUnit v3 builds the project
#    to a standalone executable, which we invoke directly on the VM.
echo
timing_start "publish live tests (self-contained)"
dotnet publish tests/Live.Tests/Live.Tests.csproj \
  -c "$CONFIG" -r "$RID" --self-contained true \
  -o "$OUT/Live.Tests" >/dev/null
timing_end

# 5) Include the VM setup helper (and the timing helper it sources) so they
#    travel with the bundle (Option D).
echo
timing_start "add setup-vm-mi.sh to bundle"
cp "$(dirname "${BASH_SOURCE[0]}")/setup-vm-mi.sh" "$OUT/"
cp "$(dirname "${BASH_SOURCE[0]}")/lib/timing.sh" "$OUT/"
chmod +x "$OUT/setup-vm-mi.sh"
timing_end

echo
echo "==> Bundle ready: $OUT"
echo "    Run on the VM:  $OUT/Core.Cli/az --help"

if [[ "$MAKE_TAR" == "1" ]]; then
  TARBALL="$REPO_ROOT/publish/vm/azclips-${CONFIG}-${RID}.tar.gz"
  echo
  timing_start "create tarball ($(basename "$TARBALL"))"
  tar -czf "$TARBALL" -C "$(dirname "$OUT")" "$(basename "$OUT")"
  timing_end
  echo "    $(du -h "$TARBALL" | cut -f1) -> $TARBALL"
  echo
  echo "    The bundle travels to the VM via Azure Storage + 'az vm run-command'"
  echo "    (no SSH). Run the full flow with: scripts/mi-live-test.sh"
fi

timing_summary "Bundle build (publish-vm-mi.sh)"
