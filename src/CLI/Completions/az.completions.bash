# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
# Requires: jq
#
# Completions are resolved dynamically by invoking `az --completions [group]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.

_az_resolve_exe() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    # 1. Same directory as this script
    if [[ -x "$script_dir/az" ]]; then
        echo "$script_dir/az"
        return
    fi
    # 2. Walk up to find repo root (look for cirrus.slnx), then check artifacts/
    local dir="$script_dir"
    while [[ -n "$dir" && "$dir" != "/" ]]; do
        if [[ -f "$dir/cirrus.slnx" ]]; then
            local cfg
            for cfg in Release Debug; do
                local candidate="$dir/artifacts/$cfg/Core.Cli/az"
                if [[ -x "$candidate" ]]; then
                    echo "$candidate"
                    return
                fi
            done
            break
        fi
        dir="$(dirname "$dir")"
    done
    # 3. Fall back to PATH
    if command -v az &>/dev/null; then
        command -v az
    fi
}

# Cache: associative array group -> json file path (temp files)
declare -A _az_cache
_az_top_level=""

_az_query() {
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    "$exe" --completions "$@" 2>/dev/null
}

_az_get_top_level() {
    if [[ -n "$_az_top_level" ]]; then
        echo "$_az_top_level"
        return
    fi
    local json
    json="$(_az_query)"
    if [[ -n "$json" ]]; then
        _az_top_level="$(echo "$json" | jq -r 'keys[]' 2>/dev/null)"
    fi
    echo "$_az_top_level"
}

_az_completions() {
    local cur prev words cword
    _init_completion 2>/dev/null || {
        COMPREPLY=()
        cur="${COMP_WORDS[COMP_CWORD]}"
        prev="${COMP_WORDS[COMP_CWORD-1]}"
        words=("${COMP_WORDS[@]}")
        cword=$COMP_CWORD
    }

    case $cword in
        1)
            # Level 0: command groups
            local groups
            groups="$(_az_get_top_level)"
            COMPREPLY=($(compgen -W "$groups" -- "$cur"))
            ;;
        2)
            # Level 1: subcommands + group-level params (dynamic)
            local group="${words[1]}"
            local json
            json="$(_az_query "$group")"
            if [[ -n "$json" ]]; then
                local subs params
                subs=$(echo "$json" | jq -r '._subcommands[]? // empty' 2>/dev/null)
                params=$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)
                COMPREPLY=($(compgen -W "$subs $params" -- "$cur"))
            fi
            ;;
        *)
            # Level 2+: parameters (dynamic)
            local group="${words[1]}"
            local sub="${words[2]}"
            local json
            json="$(_az_query "$group" "$sub")"
            local params=""
            if [[ -n "$json" ]]; then
                # Try subcommand-specific params first, then group-level
                params=$(echo "$json" | jq -r --arg s "$sub" '.[$s][]? // empty' 2>/dev/null)
                if [[ -z "$params" ]]; then
                    params=$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)
                fi
            fi

            # Filter out already-used params
            local used_params=()
            for ((i=3; i<cword; i++)); do
                if [[ "${words[i]}" == --* ]]; then
                    used_params+=("${words[i]}")
                fi
            done

            local available=""
            for p in $params; do
                local already_used=false
                for u in "${used_params[@]}"; do
                    if [[ "$p" == "$u" ]]; then
                        already_used=true
                        break
                    fi
                done
                if ! $already_used; then
                    available="$available $p"
                fi
            done

            COMPREPLY=($(compgen -W "$available" -- "$cur"))
            ;;
    esac
}

complete -F _az_completions az

echo "az tab completion registered (bash, dynamic)."
