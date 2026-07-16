#!/usr/bin/env bash
# azclips agent — macOS "Failed to create CoreCLR (0x80070008)" diagnostics.
#
# Runs everything in ONE pass and writes ONE report file to the current dir.
# Just run it — no arguments, no Azure login needed (the crash happens before
# any login). At the end it prints the file path; send that one file back.
#   bash agent-macos-diag.sh
#
# What it collects, in order:
#   1. environment (OS/arch/memory/dotnet)
#   2. install layout + integrity (files, arch, codesign, quarantine)
#   3. permissions on launcher + every runtime dylib + deps tree
#   4. bisect: which of az's spawn conditions triggers the crash — EACH case
#      run with COREHOST_TRACE on, so a crashing case captures the full hostfxr
#      trace (not just the one-line error), plus its exit code and full stderr
#   5. the real thing: let `az` itself spawn the agent (only this path carries
#      the .NET-parent env injection that hand-running can't reproduce)

set -uo pipefail

# Locate the install dir (where ./az and ./azclips-agent live).
if [ -n "${1:-}" ]; then INSTALL_DIR="$1"
elif [ -x "$HOME/.local/share/azcli/az" ]; then INSTALL_DIR="$HOME/.local/share/azcli"
elif [ -x "$PWD/az" ]; then INSTALL_DIR="$PWD"
else INSTALL_DIR="$HOME/.local/share/azcli"; fi

OUT="$PWD/azclips-agent-diag-$(date +%Y%m%d-%H%M%S).txt"
: > "$OUT"
AGENT="$INSTALL_DIR/azclips-agent"
AGENT_ARGS=(--agent-mode --failed-command 'vm show' --error 'diagnostic run')

log() { echo "$@" | tee -a "$OUT"; }
run() { echo "\$ $*" | tee -a "$OUT"; eval "$@" 2>&1 | tee -a "$OUT"; echo | tee -a "$OUT"; }

# Run the agent once under COREHOST_TRACE with a given cwd + env, fold the full
# host trace + exit code + stderr into the report, and say if it crashed.
# usage: trace_case "<label>" "<cwd>" ENV1=v1 ENV2=v2 ...
trace_case() {
  local label="$1" cwd="$2"; shift 2
  local tf; tf="${TMPDIR:-/tmp}/azclips-corehost-case-$$.log"
  rm -f "$tf"
  log ""
  log "########## $label ##########"
  log "(cwd=$cwd; extra env: $*)"
  local out rc
  out="$(cd "$cwd" && env COREHOST_TRACE=1 COREHOST_TRACEFILE="$tf" "$@" "$AGENT" "${AGENT_ARGS[@]}" 2>&1)"
  rc=$?
  log "----- combined stdout+stderr (full) -----"
  echo "$out" | tee -a "$OUT"
  log "----- exit code: $rc -----"
  if grep -qi "Failed to create CoreCLR" <<<"$out"; then
    log ">>> RESULT: CRASHED (Failed to create CoreCLR)"
  else
    log ">>> RESULT: ok (no CoreCLR crash)"
  fi
  log "----- full CoreCLR host trace for this case -----"
  if [ -f "$tf" ]; then cat "$tf" >> "$OUT"; rm -f "$tf"; else log "<no host trace file produced>"; fi
}

log "===== azclips agent macOS diagnostics ====="
log "install dir : $INSTALL_DIR"
log "output file : $OUT"

# 1) environment
log ""; log "----- 1. environment -----"
run "sw_vers"
run "uname -m"
run "sysctl -n hw.memsize 2>/dev/null | awk '{print \$1/1024/1024/1024 \" GB RAM\"}'"
run "vm_stat 2>/dev/null | head -5"
run "echo \"DOTNET_ROOT=\${DOTNET_ROOT:-<unset>}\""
run "which -a dotnet 2>/dev/null || echo '<no dotnet on PATH>'"
run "dotnet --info 2>/dev/null | head -20 || echo '<dotnet --info failed / not installed>'"

# 2) install layout + integrity
log ""; log "----- 2. install layout + integrity -----"
run "ls -la '$INSTALL_DIR' | head -40"
run "file '$INSTALL_DIR/az' 2>/dev/null || echo '<no az>'"
run "file '$INSTALL_DIR/azclips-agent' 2>/dev/null || echo '<no azclips-agent>'"
run "cat '$INSTALL_DIR/azclips-agent.runtimeconfig.json' 2>/dev/null || echo '<no runtimeconfig>'"
run "ls '$INSTALL_DIR' | grep -iE 'coreclr|libhostfxr|libhostpolicy|System.Private.CoreLib' || echo '<no runtime dylibs beside launcher>'"
# codesign + quarantine on BOTH launchers (a blocked/unsigned dylib fails CLR load)
run "codesign -dv '$INSTALL_DIR/az' 2>&1 | head -6 || echo '<az codesign check failed>'"
run "codesign -dv '$INSTALL_DIR/azclips-agent' 2>&1 | head -6 || echo '<agent codesign check failed>'"
run "xattr -l '$INSTALL_DIR/az' 2>/dev/null || echo '<no xattrs on az>'"
run "xattr -l '$INSTALL_DIR/azclips-agent' 2>/dev/null || echo '<no xattrs on agent>'"

# 3) permissions on launcher + every runtime dylib + deps tree
log ""; log "----- 3. permissions -----"
run "ls -l '$INSTALL_DIR/az' '$INSTALL_DIR/azclips-agent' 2>/dev/null"
run "find '$INSTALL_DIR' -name '*.dylib' -exec ls -l {} \; 2>/dev/null | head -60"
run "find '$INSTALL_DIR' -name '*.dylib' ! -perm -u+r 2>/dev/null | head || echo '<all dylibs readable>'"
run "find '$INSTALL_DIR/Extensions' -maxdepth 4 -name deps -type d 2>/dev/null -exec ls -ld {} \;"

# enable the agent so `az` will actually spawn it in step 5
log ""; log "----- enabling agent for the spawn test -----"
run "cd '$INSTALL_DIR' && ./az config set --key agent.enabled --value true"
run "cd '$INSTALL_DIR' && ./az config set --key agent.sessionStyle --value oneshot"

# 4) bisect — each case under COREHOST_TRACE so a crash captures the full trace.
#    Hand-running the agent doesn't crash; az spawning it does. Reproduce az's
#    spawn-specific conditions one at a time to find which one breaks it.
log ""; log "===== 4. bisect az's spawn conditions (each with host trace) ====="
trace_case "4.0 baseline: from HOME, no extra env"            "$HOME"        env
trace_case "4.A cwd = install dir"                            "$INSTALL_DIR" env
trace_case "4.B AZCLIPS_IN_AGENT_MODE=1 (from HOME)"          "$HOME"        env AZCLIPS_IN_AGENT_MODE=1
trace_case "4.C AZCLIPS_CALLER_CWD=HOME (from HOME)"          "$HOME"        env "AZCLIPS_CALLER_CWD=$HOME"
trace_case "4.ALL cwd=install + both env vars"                "$INSTALL_DIR" env AZCLIPS_IN_AGENT_MODE=1 "AZCLIPS_CALLER_CWD=$HOME"

# 5) the real path — let `az` itself spawn the agent. Only this carries the
#    .NET-parent environment injection that a hand-launched agent can't. Trace
#    is exported so the spawned child inherits it and writes its own hostfxr log.
log ""; log "===== 5. real az-spawned agent (with inherited host trace) ====="
REAL_TF="${TMPDIR:-/tmp}/azclips-corehost-real-$$.log"; rm -f "$REAL_TF"
log "\$ COREHOST_TRACE=1 COREHOST_TRACEFILE=$REAL_TF ./az vm show --name test --subscription-id 000... --resource-group test"
real_out="$(cd "$INSTALL_DIR" && env COREHOST_TRACE=1 COREHOST_TRACEFILE="$REAL_TF" ./az vm show --name test --subscription-id 00000000-0000-0000-0000-000000000000 --resource-group test 2>&1)"; real_rc=$?
log "----- combined stdout+stderr (full) -----"; echo "$real_out" | tee -a "$OUT"
log "----- exit code: $real_rc -----"
grep -qi "Failed to create CoreCLR" <<<"$real_out" && log ">>> RESULT: CRASHED" || log ">>> RESULT: ok"
log "----- host trace from the az-spawned run (may include the child's) -----"
if [ -f "$REAL_TF" ]; then cat "$REAL_TF" >> "$OUT"; rm -f "$REAL_TF"; else log "<no host trace file produced>"; fi

log ""; log "===== done ====="
echo
echo "Diagnostics written to a single file — just send this back:"
echo "  $OUT"
