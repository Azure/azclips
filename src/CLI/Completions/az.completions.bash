# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
# Requires: jq
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.

_az_resolve_exe() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    # 1. Same directory as this script
    if [[ -x "$script_dir/az" ]]; then
        echo "$script_dir/az"
        return
    fi
    if [[ -x "$script_dir/az.exe" ]]; then
        echo "$script_dir/az.exe"
        return
    fi
    # 2. Walk up to find repo root (look for global.json), then check artifacts/
    local dir="$script_dir"
    while [[ -n "$dir" && "$dir" != "/" ]]; do
        if [[ -f "$dir/global.json" ]]; then
            local cfg
            for cfg in Release Debug; do
                if [[ -x "$dir/artifacts/$cfg/Core.Cli/az" ]]; then
                    echo "$dir/artifacts/$cfg/Core.Cli/az"
                    return
                fi
                if [[ -x "$dir/artifacts/$cfg/Core.Cli/az.exe" ]]; then
                    echo "$dir/artifacts/$cfg/Core.Cli/az.exe"
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

# Cached result: empty = not checked, 1 = azclips, 0 = not azclips
_az_is_azclips_cached=""

_az_is_azclips() {
    if [[ -n "$_az_is_azclips_cached" ]]; then
        return $(( 1 - _az_is_azclips_cached ))
    fi
    # Use type -P to get the filesystem path, skipping aliases/functions
    local exe
    exe="$(type -P az 2>/dev/null)"
    if [[ -z "$exe" ]]; then
        _az_is_azclips_cached=0
        return 1
    fi
    local ver
    if ! ver="$("$exe" --version 2>&1)" || [[ -z "$ver" ]]; then
        # --version failed or empty output → not azclips
        _az_is_azclips_cached=0
        return 1
    fi
    if echo "$ver" | grep -q 'azure-cli'; then
        _az_is_azclips_cached=0
        return 1
    else
        _az_is_azclips_cached=1
        return 0
    fi
}

_az_query() {
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    "$exe" --completions "$@" 2>/dev/null
}

# Always query `az --completions` at tab time (no caching).
_az_get_top_level() {
    local json
    json="$(_az_query)"
    if [[ -n "$json" ]]; then
        echo "$json" | jq -r 'keys[]' 2>/dev/null
    fi
}

_az_completions() {
    # Path-qualified invocation → always complete.
    # Bare "az" → check PATH to ensure it's azclips, not system azure-cli.
    if [[ "${COMP_WORDS[0]}" != */* && "${COMP_WORDS[0]}" != *\\* ]]; then
        _az_is_azclips || return
    fi

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
            # If sub is a flag, query group-level params only
            if [[ "$sub" == --* ]]; then
                json="$(_az_query "$group")"
            else
                json="$(_az_query "$group" "$sub")"
            fi
            local params=""
            if [[ -n "$json" ]]; then
                # Try subcommand-specific params first, then group-level
                if [[ "$sub" != --* ]]; then
                    params=$(echo "$json" | jq -r --arg s "$sub" '.[$s][]? // empty' 2>/dev/null)
                fi
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

complete -F _az_completions az az.exe

# Show all completions on first Tab press (like PowerShell's MenuComplete)
bind 'set show-all-if-ambiguous on' 2>/dev/null

echo "az tab completion registered (bash, dynamic)."
