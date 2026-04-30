#!/usr/bin/env bash
set -euo pipefail

BASHRC="${HOME}/.bashrc"
START_MARKER="# >>> azv2-autocomplete >>>"
END_MARKER="# <<< azv2-autocomplete <<<"

if [[ -f "$BASHRC" ]]; then
    tmp_file="$(mktemp)"
    awk -v start="$START_MARKER" -v end="$END_MARKER" '
    $0 == start { skip = 1; next }
    $0 == end { skip = 0; next }
    skip != 1 { print }
    ' "$BASHRC" > "$tmp_file"
    mv "$tmp_file" "$BASHRC"
fi

complete -r azv2 2>/dev/null || true
unset -f _azv2_autocomplete_complete 2>/dev/null || true
unset -f _azv2_autocomplete_ensure_helper 2>/dev/null || true
unset AZV2_AUTOCOMPLETE_BIN 2>/dev/null || true
unset AZV2_AUTOCOMPLETE_PROJECT_DIR 2>/dev/null || true

echo "azv2 autocomplete uninstalled."
echo "Run: source ~/.bashrc"
