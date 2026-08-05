#!/usr/bin/env bash
# Generic benchmark change-detection.
#
# Derives the full extension "universe" from the [BenchmarkExtension("<Name>")]
# attribute (the single source of truth) and computes which extensions to run
# from path conventions + the attribute->file map.
# Inputs (environment):
#   EVENT          github.event_name (e.g. pull_request, push, workflow_dispatch)
#   SHARED         "true" if a shared/cross-cutting path changed (forces all)
#   STARTUP        "true" if a startup-benchmark path changed
#   CHANGED_FILES  JSON array of changed files (from dorny/paths-filter list-files)
#
# Outputs (GITHUB_OUTPUT):
#   matrix         {"extension":[...]}                    -- run-cli / run-ps matrix
#   publish_matrix {"extension":[...],"product":[...]}    -- publish matrix
#   has_ext_work   "true"/"false"                         -- any extension in scope
#   run_startup    "true"/"false"                         -- run the startup family
set -euo pipefail

: "${EVENT:?EVENT required}"
SHARED="${SHARED:-false}"
STARTUP="${STARTUP:-false}"
CHANGED_FILES="${CHANGED_FILES:-[]}"
OUT="${GITHUB_OUTPUT:-/dev/stdout}"

declare -A FILE_EXT
declare -A IS_EXT
while IFS= read -r line; do
  line="${line%$'\r'}"                 # tolerate CRLF checkouts
  [ -z "$line" ] && continue
  file="${line%%:*}"
  name="${line#*:}"
  ext="${name,,}"
  FILE_EXT["$file"]="$ext"
  IS_EXT["$ext"]=1
done < <(grep -rHoP '^\s*\[BenchmarkExtension\("\K[^"]+' tests/Benchmark --include='*.cs' 2>/dev/null || true)

mapfile -t UNIVERSE < <(printf '%s\n' "${!IS_EXT[@]}" | sort)

json_array() {
  local out="[" first=1 x
  for x in "$@"; do
    [ $first -eq 1 ] && first=0 || out+=","
    out+="\"$x\""
  done
  out+="]"
  printf '%s' "$out"
}

# --- Decide which extensions to run ---
if [ "$EVENT" = "workflow_dispatch" ] || [ "$SHARED" = "true" ]; then
  RUN=("${UNIVERSE[@]}")
else
  declare -A HIT
  while IFS= read -r cf; do
    cf="${cf%$'\r'}"                   # tolerate CRLF
    [ -z "$cf" ] && continue
    # (a) a benchmark class file changed (its filename may differ from the ext name)
    if [ -n "${FILE_EXT[$cf]:-}" ]; then HIT["${FILE_EXT[$cf]}"]=1; fi
    # (b) product source-path convention: src/Extensions/<Ext>/** or src/PS/<Ext>/**
    if [[ "$cf" =~ ^src/(Extensions|PS)/([^/]+)/ ]]; then
      e="${BASH_REMATCH[2],,}"
      [ -n "${IS_EXT[$e]:-}" ] && HIT["$e"]=1
    fi
  done < <(printf '%s' "$CHANGED_FILES" | jq -r '.[]?' 2>/dev/null || true)

  RUN=()
  for u in "${UNIVERSE[@]}"; do [ -n "${HIT[$u]:-}" ] && RUN+=("$u"); done
fi

EXT_LIST="$(json_array "${RUN[@]}")"
{
  echo "matrix={\"extension\":$EXT_LIST}"
  echo "publish_matrix={\"extension\":$EXT_LIST,\"product\":[\"CLI\",\"PS\"]}"
  if [ "$EXT_LIST" = "[]" ]; then echo "has_ext_work=false"; else echo "has_ext_work=true"; fi
  # Startup is cross-cutting (whole-process startup): run it on shared changes,
  # startup-specific changes, or manual dispatch -- not on extension-only changes.
  if [ "$EVENT" = "workflow_dispatch" ] || [ "$SHARED" = "true" ] || [ "$STARTUP" = "true" ]; then
    echo "run_startup=true"
  else
    echo "run_startup=false"
  fi
} >> "$OUT"

echo "Universe: ${UNIVERSE[*]:-<none>}" >&2
echo "Run: ${RUN[*]:-<none>}" >&2
