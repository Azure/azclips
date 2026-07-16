#!/usr/bin/env bash
# Pin down WHICH of `az`'s subprocess-spawn conditions triggers the macOS
# "Failed to create CoreCLR (0x80070008)" crash.
#
# Fact so far: running ./azclips-agent by hand does NOT crash, but az spawning it
# DOES. So the trigger is one of the three things az does differently when it
# spawns the agent:
#   (A) sets the child's working directory to the install dir
#   (B) sets env AZCLIPS_IN_AGENT_MODE=1
#   (C) sets env AZCLIPS_CALLER_CWD=<the dir you ran az from>
# This script reproduces each, one at a time, so we see which one breaks it.
#
# Run it from your azclips install dir (where ./azclips-agent lives), or pass
# that dir as the first arg. No Azure login needed.

set -uo pipefail
INSTALL_DIR="${1:-$PWD}"
[ -x "$INSTALL_DIR/azclips-agent" ] || INSTALL_DIR="$HOME/.local/share/azcli"
OUT="$PWD/agent-spawn-bisect-$(date +%Y%m%d-%H%M%S).txt"
: > "$OUT"

AGENT="$INSTALL_DIR/azclips-agent"
ARGS=(--agent-mode --failed-command 'vm show' --error 'bisect run')

section() { echo; echo "########## $* ##########" | tee -a "$OUT"; }
check()   { grep -qi "Failed to create CoreCLR" <<<"$1" && echo ">>> RESULT: CRASHED (Failed to create CoreCLR)" | tee -a "$OUT" || echo ">>> RESULT: ok (no CoreCLR crash)" | tee -a "$OUT"; }

echo "install dir: $INSTALL_DIR" | tee -a "$OUT"
echo "output:      $OUT"        | tee -a "$OUT"

# Baseline — plain run from a NON-install dir (like the shell does). Should be ok.
section "0. baseline: run from HOME, no extra env"
O="$(cd "$HOME" && "$AGENT" "${ARGS[@]}" 2>&1 | head -20)"; echo "$O" | tee -a "$OUT"; check "$O"

# (A) working directory = install dir (what az sets)
section "A. cwd = install dir"
O="$(cd "$INSTALL_DIR" && "$AGENT" "${ARGS[@]}" 2>&1 | head -20)"; echo "$O" | tee -a "$OUT"; check "$O"

# (B) env AZCLIPS_IN_AGENT_MODE=1
section "B. AZCLIPS_IN_AGENT_MODE=1 (from HOME)"
O="$(cd "$HOME" && AZCLIPS_IN_AGENT_MODE=1 "$AGENT" "${ARGS[@]}" 2>&1 | head -20)"; echo "$O" | tee -a "$OUT"; check "$O"

# (C) env AZCLIPS_CALLER_CWD set
section "C. AZCLIPS_CALLER_CWD=\$HOME (from HOME)"
O="$(cd "$HOME" && AZCLIPS_CALLER_CWD="$HOME" "$AGENT" "${ARGS[@]}" 2>&1 | head -20)"; echo "$O" | tee -a "$OUT"; check "$O"

# (ALL) all three together — the closest reproduction of what az actually does
section "ALL. cwd=install dir + both env vars (closest to az's real spawn)"
O="$(cd "$INSTALL_DIR" && AZCLIPS_IN_AGENT_MODE=1 AZCLIPS_CALLER_CWD="$HOME" "$AGENT" "${ARGS[@]}" 2>&1 | head -20)"; echo "$O" | tee -a "$OUT"; check "$O"

echo | tee -a "$OUT"
echo "===== done. Send back: $OUT =====" | tee -a "$OUT"
echo
echo "Wrote: $OUT"
