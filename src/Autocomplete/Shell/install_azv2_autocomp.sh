#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
HELPER_BINARY="${PROJECT_DIR}/bin/Release/net9/azv2-autocomplete"
PROJECT_FILE="${PROJECT_DIR}/Autocomplete.csproj"
UNINSTALL_SCRIPT="${SCRIPT_DIR}/uninstall_azv2_autocomp.sh"
BASHRC="${HOME}/.bashrc"

START_MARKER="# >>> azv2-autocomplete >>>"
END_MARKER="# <<< azv2-autocomplete <<<"

if [[ ! -f "$PROJECT_FILE" ]]; then
    echo "Missing project file: $PROJECT_FILE" >&2
    exit 1
fi

if [[ ! -f "$UNINSTALL_SCRIPT" ]]; then
    echo "Missing uninstall script: $UNINSTALL_SCRIPT" >&2
    exit 1
fi

touch "$BASHRC"

tmp_file="$(mktemp)"
awk -v start="$START_MARKER" -v end="$END_MARKER" '
$0 == start { skip = 1; next }
$0 == end { skip = 0; next }
skip != 1 { print }
' "$BASHRC" > "$tmp_file"

cat >> "$tmp_file" <<EOF

$START_MARKER
AZV2_AUTOCOMPLETE_BIN="$HELPER_BINARY"
AZV2_AUTOCOMPLETE_PROJECT_DIR="$PROJECT_DIR"
export AZV2_AUTOCOMPLETE_TIMEOUT_MS="\${AZV2_AUTOCOMPLETE_TIMEOUT_MS:-10000}"

_azv2_autocomplete_ensure_helper() {
    if [[ -x "\$AZV2_AUTOCOMPLETE_BIN" ]]; then
        return 0
    fi

    (cd "\$AZV2_AUTOCOMPLETE_PROJECT_DIR" && dotnet build -c Release >/dev/null 2>&1) || return 1
    [[ -x "\$AZV2_AUTOCOMPLETE_BIN" ]]
}

_azv2_autocomplete_complete() {
    _azv2_autocomplete_ensure_helper || {
        COMPREPLY=()
        return 0
    }

    if [[ "\${COMP_WORDS[0]:-}" != "azv2" ]]; then
        COMPREPLY=()
        return 0
    fi

    if [[ "\${COMP_POINT:-0}" -ne "\${#COMP_LINE}" ]]; then
        COMPREPLY=()
        return 0
    fi

    local current_word=""
    if [[ "\${COMP_CWORD:-0}" -gt 0 ]]; then
        current_word="\${COMP_WORDS[COMP_CWORD]}"
    fi

    local selected
    selected=\$("\$AZV2_AUTOCOMPLETE_BIN" \
        --line "\$COMP_LINE" \
        --cursor "\$COMP_POINT" \
        --prefix "\$current_word" 2>/dev/null)
    local exit_code=\$?

    if [[ \$exit_code -eq 0 && -n "\$selected" ]]; then
        if [[ "\${COMP_CWORD:-0}" -eq 0 ]]; then
            COMPREPLY=("azv2 \$selected")
            compopt -o nospace 2>/dev/null || true
        else
            COMPREPLY=("\$selected")
            compopt +o nospace 2>/dev/null || true
        fi
    else
        COMPREPLY=()
    fi
}

complete -o bashdefault -o default -F _azv2_autocomplete_complete azv2
$END_MARKER
EOF

mv "$tmp_file" "$BASHRC"

echo "azv2 autocomplete installed."
echo "Run: source ~/.bashrc"
echo "Uninstall (from this folder): ./uninstall_azv2_autocomp.sh"
