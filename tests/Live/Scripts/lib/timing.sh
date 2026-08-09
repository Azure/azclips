#!/usr/bin/env bash
#
# timing.sh — minimal step timer shared by the live-test scripts.
#
# The whole managed-identity live test runs as a SINGLE GitHub Actions step, so
# the run UI only ever shows one duration for the lot. These helpers attribute
# that duration to the phase actually spending it.
#
# Sourced, never executed:
#
#   source "$REPO_ROOT/tests/Live/Scripts/lib/timing.sh"
#
#   timing_start "Build bundle"      # ... work ...      ; timing_end
#   timed "Upload blob" az storage blob upload ...
#   timing_record "smoke: SomeTest" 8210               # pre-measured, in ms
#   timing_summary "Stage 2 (orchestrator)"
#
# timing_start/timing_end do not nest — a nested phase belongs in the child
# script, which prints its own summary. When $GITHUB_STEP_SUMMARY is set the
# summary is also appended there as a markdown table.
#
# POSIX-ish bash 4+; runs both on the dev box and on the test VM.

[[ -n "${_TIMING_SH_LOADED:-}" ]] && return 0
_TIMING_SH_LOADED=1

_TIMING_LABELS=()
_TIMING_MS=()
_TIMING_DETAIL=()
_TIMING_CUR_LABEL=""
_TIMING_CUR_T0=0
_TIMING_T0="$(date +%s%N)"

_timing_ms_since() { echo $(( ( $(date +%s%N) - $1 ) / 1000000 )); }

# 91234 -> "1m31.2s", 8210 -> "8.2s"
timing_fmt() {
  local ms=$1 sign="" s
  (( ms < 0 )) && { sign="-"; ms=$(( -ms )); }
  s=$(( ms / 1000 ))
  if (( s >= 60 )); then
    printf '%s%dm%d.%01ds' "$sign" $(( s / 60 )) $(( s % 60 )) $(( (ms % 1000) / 100 ))
  else
    printf '%s%d.%01ds' "$sign" "$s" $(( (ms % 1000) / 100 ))
  fi
}

# Append a row measured elsewhere (e.g. parsed out of the xUnit XML report).
#
# With TIMING_EMIT_MARKERS=1 each row is also echoed as a machine-readable
# marker. That is how VM-side timings survive the trip home: `az vm run-command`
# gives the orchestrator nothing but the guest's stdout, so it re-parses these
# markers to rebuild the VM's table locally.
timing_record() {
  _TIMING_LABELS+=("$1")
  _TIMING_MS+=("$2")
  _TIMING_DETAIL+=("${3:-0}")
  [[ -n "${TIMING_EMIT_MARKERS:-}" ]] && printf 'MI_LIVE_TIMING:%s|%s|%s\n' "$1" "$2" "${3:-0}"
  return 0
}

# A breakdown of a phase already recorded above it (e.g. the individual tests
# inside "run live tests"). Shown indented and left out of the accounted total,
# which would otherwise count the same seconds twice.
timing_record_detail() { timing_record "$1" "$2" 1; }

# Drop recorded rows and restart the wall clock, so one script can print more
# than one independent table.
timing_reset() {
  _TIMING_LABELS=()
  _TIMING_MS=()
  _TIMING_DETAIL=()
  _TIMING_T0="$(date +%s%N)"
}

timing_start() {
  _TIMING_CUR_LABEL="$1"
  _TIMING_CUR_T0="$(date +%s%N)"
  printf '==> %s ...\n' "$1"
}

timing_end() {
  local ms
  ms="$(_timing_ms_since "$_TIMING_CUR_T0")"
  timing_record "$_TIMING_CUR_LABEL" "$ms"
  printf '    [%s] %s\n' "$(timing_fmt "$ms")" "$_TIMING_CUR_LABEL"
}

# timed LABEL cmd [args...] — time a single command, preserving its exit code.
# The `|| rc=$?` keeps `set -e` from killing us before the row is recorded; the
# non-zero return then trips `set -e` in the caller as usual.
timed() {
  local label="$1" rc=0
  shift
  timing_start "$label"
  "$@" || rc=$?
  timing_end
  return $rc
}

# timing_summary [TITLE] [TOTAL_MS] — print the table. TOTAL_MS overrides the
# wall clock, for tables rebuilt from markers where our own clock is meaningless.
# Also emits a markdown version to $GITHUB_STEP_SUMMARY so the numbers land on
# the workflow run's summary page.
#
# Suppressed while emitting markers: that means someone downstream will render
# this table from them, and `az vm run-command` only returns the LAST ~4KB of
# stdout — a duplicate table here would push the earlier steps out of the reply.
timing_summary() {
  [[ -n "${TIMING_EMIT_MARKERS:-}" ]] && return 0

  local title="${1:-Timing}" total i w=0 label ms pct accounted=0
  total="${2:-$(_timing_ms_since "$_TIMING_T0")}"

  local n=${#_TIMING_LABELS[@]}
  if (( n == 0 )); then
    return 0
  fi

  for (( i = 0; i < n; i++ )); do
    label="${_TIMING_LABELS[$i]}"
    (( ${#label} + ( _TIMING_DETAIL[i] ? 4 : 0 ) > w )) && w=$(( ${#label} + ( _TIMING_DETAIL[i] ? 4 : 0 ) ))
  done
  w=$(( w + 2 ))
  (( w < 22 )) && w=22

  echo
  printf '===== %s =====\n' "$title"
  for (( i = 0; i < n; i++ )); do
    label="${_TIMING_LABELS[$i]}"
    ms="${_TIMING_MS[$i]}"
    if [[ "${_TIMING_DETAIL[$i]}" == "1" ]]; then
      printf '    %-*s  %8s\n' $(( w - 4 )) "$label" "$(timing_fmt "$ms")"
      continue
    fi
    accounted=$(( accounted + ms ))
    pct=0
    (( total > 0 )) && pct=$(( ms * 100 / total ))
    printf '  %-*s  %8s  %3d%%\n' "$w" "$label" "$(timing_fmt "$ms")" "$pct"
  done
  printf '  %-*s  %8s\n' "$w" "$(printf '%.0s-' $(seq 1 $w))" "--------"
  printf '  %-*s  %8s\n' "$w" "measured" "$(timing_fmt "$accounted")"
  printf '  %-*s  %8s\n' "$w" "other (untimed)" "$(timing_fmt $(( total - accounted )))"
  printf '  %-*s  %8s\n' "$w" "TOTAL" "$(timing_fmt "$total")"
  echo

  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
      printf '### %s\n\n' "$title"
      printf '| Step | Duration | Share |\n|---|--:|--:|\n'
      for (( i = 0; i < n; i++ )); do
        ms="${_TIMING_MS[$i]}"
        if [[ "${_TIMING_DETAIL[$i]}" == "1" ]]; then
          printf '| &nbsp;&nbsp;&nbsp;&nbsp;%s | %s | |\n' "${_TIMING_LABELS[$i]}" "$(timing_fmt "$ms")"
          continue
        fi
        pct=0
        (( total > 0 )) && pct=$(( ms * 100 / total ))
        printf '| **%s** | %s | %d%% |\n' "${_TIMING_LABELS[$i]}" "$(timing_fmt "$ms")" "$pct"
      done
      printf '| **other (untimed)** | %s | |\n' "$(timing_fmt $(( total - accounted )))"
      printf '| **TOTAL** | **%s** | |\n\n' "$(timing_fmt "$total")"
    } >> "$GITHUB_STEP_SUMMARY"
  fi
}
