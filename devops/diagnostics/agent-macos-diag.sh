#!/usr/bin/env bash
# azclips agent — macOS "Failed to create CoreCLR (0x80070008)" diagnostics.
#
# WHAT: reproduces the crash with .NET host tracing on and collects the
#       environment facts needed to explain WHY the agent subprocess can't start
#       its runtime. Writes everything to one file you can paste back.
#
# HOW:  cd into your azclips install dir (where ./az lives) and run this script
#       from there, OR pass the install dir as the first argument:
#         bash agent-macos-diag.sh
#         bash agent-macos-diag.sh ~/.local/share/azcli
#
# It does NOT need an Azure login — the crash happens before any login step.

set -uo pipefail

# Install dir: default to where install/azcli-install.sh puts it. Override by
# passing a path as the first arg, or by running this script from inside it.
if [ -n "${1:-}" ]; then
  INSTALL_DIR="$1"
elif [ -x "$HOME/.local/share/azcli/az" ]; then
  INSTALL_DIR="$HOME/.local/share/azcli"     # the standard install location
elif [ -x "$PWD/az" ]; then
  INSTALL_DIR="$PWD"                          # running from inside the install dir
else
  INSTALL_DIR="$HOME/.local/share/azcli"      # best guess; steps below will report if empty
fi
OUT="${TMPDIR:-/tmp}/azclips-agent-diag-$(date +%Y%m%d-%H%M%S).txt"

log() { echo "$@" | tee -a "$OUT"; }
run() { echo "\$ $*" | tee -a "$OUT"; eval "$@" 2>&1 | tee -a "$OUT"; echo | tee -a "$OUT"; }

: > "$OUT"
log "===== azclips agent macOS diagnostics ====="
log "install dir : $INSTALL_DIR"
log "output file : $OUT"
log ""

# 1) Environment facts -------------------------------------------------------
log "----- 1. environment -----"
run "sw_vers"                                  # macOS product + build version
run "uname -m"                                 # arm64 vs x86_64
run "sysctl -n hw.memsize 2>/dev/null | awk '{print \$1/1024/1024/1024 \" GB RAM\"}'"
run "vm_stat 2>/dev/null | head -5"            # 0x80070008 == NOT_ENOUGH_MEMORY; capture memory pressure
run "echo \"DOTNET_ROOT=\${DOTNET_ROOT:-<unset>}\""
run "which -a dotnet 2>/dev/null || echo '<no dotnet on PATH>'"
run "dotnet --info 2>/dev/null | head -20 || echo '<dotnet --info failed / not installed>'"

# 2) Package integrity -------------------------------------------------------
log "----- 2. install layout -----"
run "ls -la '$INSTALL_DIR' | head -40"
run "ls -la '$INSTALL_DIR' | grep -i azclips-agent || echo '<no azclips-agent next to az>'"
run "file '$INSTALL_DIR/az' 2>/dev/null || echo '<no az>'"
run "file '$INSTALL_DIR/azclips-agent' 2>/dev/null || echo '<no azclips-agent>'"
# The agent's runtime config points at the probing paths CoreCLR resolves at startup.
run "cat '$INSTALL_DIR/azclips-agent.runtimeconfig.json' 2>/dev/null || echo '<no runtimeconfig>'"
# Is the shared runtime actually present next to the launcher?
run "ls '$INSTALL_DIR' | grep -iE 'coreclr|libhostfxr|System.Private.CoreLib' || echo '<no runtime dlls beside launcher — expected if they live under Extensions/.../deps>'"
# Gatekeeper / quarantine: a signed dylib blocked by quarantine can fail CLR load.
run "xattr -l '$INSTALL_DIR/azclips-agent' 2>/dev/null || echo '<no xattrs>'"
run "codesign -dv '$INSTALL_DIR/azclips-agent' 2>&1 | head -8 || echo '<codesign check failed>'"

# Permissions on the pieces CoreCLR needs. 0x80070008 can be a dylib the runtime
# can't read/execute. Show the launcher + every runtime dylib + the deps tree.
log "----- 2b. permissions on launcher + runtime dylibs -----"
run "ls -l '$INSTALL_DIR/az' '$INSTALL_DIR/azclips-agent' 2>/dev/null"
run "find '$INSTALL_DIR' -name '*.dylib' -exec ls -l {} \; 2>/dev/null | head -40"
run "find '$INSTALL_DIR' -name 'libcoreclr.dylib' -o -name 'libhostfxr.dylib' -o -name 'libhostpolicy.dylib' 2>/dev/null"
# Any runtime dylib that is NOT readable would break CLR startup — flag them.
run "find '$INSTALL_DIR' -name '*.dylib' ! -perm -u+r 2>/dev/null | head || echo '<all dylibs are readable>'"
# The agent's own deps live under Extensions/.../deps — check that tree's perms.
run "find '$INSTALL_DIR/Extensions' -maxdepth 4 -name deps -type d 2>/dev/null -exec ls -ld {} \;"

# 3) Reproduce with .NET host tracing ON -------------------------------------
# COREHOST_TRACE=1 makes hostfxr/hostpolicy print the full probing + resolution
# log to stderr right up to the CoreCLR-init failure — this is what reveals the
# real cause behind the generic 0x80070008.
log "----- 3. reproduce with COREHOST_TRACE=1 -----"
cd "$INSTALL_DIR" || { log "cannot cd into install dir"; exit 1; }
run "./az config set --key agent.enabled --value true"
run "./az config set --key agent.sessionStyle --value oneshot"
log "--- triggering the agent via a failing command (host trace on) ---"
run "COREHOST_TRACE=1 COREHOST_TRACEFILE='$OUT.corehost.log' ./az vm show --name test --subscription-id 00000000-0000-0000-0000-000000000000 --resource-group test"
log "--- tail of the CoreCLR host trace (the lines just before the failure matter most) ---"
run "tail -60 '$OUT.corehost.log' 2>/dev/null || echo '<no corehost trace file — the launcher may have died before hostfxr started>'"

# 4) Run the agent launcher directly ----------------------------------------
# Bypass the az wrapper to see the launcher's own raw error, again with tracing.
log "----- 4. run azclips-agent directly (bypass az) -----"
run "COREHOST_TRACE=1 ./azclips-agent --agent-mode --failed-command 'vm show' --error 'diagnostic run' 2>&1 | head -40 || true"

log ""
log "===== done. Please send this whole file back: $OUT ====="
log "(and, if it exists, the host trace: $OUT.corehost.log)"
echo
echo "Diagnostics written to: $OUT"
