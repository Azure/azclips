# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash [/path/to/azclips/az]
# Or add to your ~/.bashrc to auto-load on every session.
# Requires: jq
#
# If the azclips binary path is not provided as an argument, the script will
# attempt to locate it relative to itself or in the repo artifacts directory.
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is configured.

# The resolved azclips binary path (set during source)
_az_azclips_exe=""

_az_resolve_exe() {
    # Return the configured azclips binary
    if [[ -n "$_az_azclips_exe" ]]; then
        echo "$_az_azclips_exe"
        return
    fi
    return 1
}

_az_init() {
    local user_path="${1:-}"

    # 1. User-provided path
    if [[ -n "$user_path" ]]; then
        if [[ -x "$user_path" ]] || command -v "$user_path" &>/dev/null; then
            _az_azclips_exe="$(cd "$(dirname "$user_path")" 2>/dev/null && pwd)/$(basename "$user_path")"
            return 0
        else
            echo "az completions: '$user_path' is not executable or not found." >&2
            return 1
        fi
    fi

    # 2. Same directory as this script
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    if [[ -x "$script_dir/az" ]]; then
        _az_azclips_exe="$script_dir/az"
        return 0
    fi
    if [[ -x "$script_dir/az.exe" ]]; then
        _az_azclips_exe="$script_dir/az.exe"
        return 0
    fi

    # 3. Walk up to find repo root (look for global.json), then check artifacts/
    local dir="$script_dir"
    while [[ -n "$dir" && "$dir" != "/" ]]; do
        if [[ -f "$dir/global.json" ]]; then
            local cfg
            for cfg in Release Debug; do
                if [[ -x "$dir/artifacts/$cfg/Core.Cli/az" ]]; then
                    _az_azclips_exe="$dir/artifacts/$cfg/Core.Cli/az"
                    return 0
                fi
                if [[ -x "$dir/artifacts/$cfg/Core.Cli/az.exe" ]]; then
                    _az_azclips_exe="$dir/artifacts/$cfg/Core.Cli/az.exe"
                    return 0
                fi
            done
            break
        fi
        dir="$(dirname "$dir")"
    done

    echo "az completions: could not locate azclips binary. Pass the path as an argument:" >&2
    echo "  source ${BASH_SOURCE[0]} /path/to/az" >&2
    return 1
}

_az_is_azclips_on_path() {
    # Check if bare "az" on PATH resolves to the same binary as our configured azclips
    local path_az
    path_az="$(type -P az 2>/dev/null)" || return 1
    [[ -n "$_az_azclips_exe" ]] || return 1
    # Compare real paths
    local real_path real_configured
    real_path="$(readlink -f "$path_az" 2>/dev/null || echo "$path_az")"
    real_configured="$(readlink -f "$_az_azclips_exe" 2>/dev/null || echo "$_az_azclips_exe")"
    [[ "$real_path" == "$real_configured" ]]
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
    # Bare "az" → only complete if PATH az is the same as our configured azclips binary.
    if [[ "${COMP_WORDS[0]}" != */* && "${COMP_WORDS[0]}" != *\\* ]]; then
        _az_is_azclips_on_path || return
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

# Initialize: resolve the azclips binary (accepts optional path argument)
if _az_init "$1"; then
    echo "az tab completion registered (bash, dynamic). Binary: $_az_azclips_exe"
fi
