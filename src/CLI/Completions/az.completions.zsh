#compdef az
# az Tab Completion for zsh (Dynamic)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/az.completions.zsh [/path/to/azclips/az]
# Or add to your ~/.zshrc to auto-load on every session.
# Requires: jq
#
# If the azclips binary path is not provided as an argument, the script will
# attempt to locate it relative to itself or in the repo artifacts directory.
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is configured.

# Opt-in debug logging:
#   export AZ_COMPLETION_DEBUG=1
#   export AZ_COMPLETION_DEBUG_LOG=/tmp/az_completion_debug.log
_az_debug() {
    [[ -n "${AZ_COMPLETION_DEBUG:-}" ]] || return 0
    local log_file="${AZ_COMPLETION_DEBUG_LOG:-/tmp/az_completion_debug.log}"
    print -r -- "[$(date '+%Y-%m-%d %H:%M:%S')] $*" >> "$log_file"
}

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
        if [[ -x "$user_path" ]] || (( $+commands[$user_path] )); then
            _az_azclips_exe="${user_path:A}"
            return 0
        else
            print -u2 "az completions: '$user_path' is not executable or not found."
            return 1
        fi
    fi

    # 2. Same directory as this script
    local script_dir="${${(%):-%x}:A:h}"
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
        dir="${dir:h}"
    done

    print -u2 "az completions: could not locate azclips binary. Pass the path as an argument:"
    print -u2 "  source ${(%):-%x} /path/to/az"
    return 1
}

_az_is_azclips_on_path() {
    # Check if bare "az" on PATH resolves to the same binary as our configured azclips
    [[ -n "$_az_azclips_exe" ]] || return 1
    (( $+commands[az] )) || return 1
    local path_az="${commands[az]}"
    # Compare real paths
    local real_path="${path_az:A}"
    local real_configured="${_az_azclips_exe:A}"
    [[ "$real_path" == "$real_configured" ]]
}

_az_query() {
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    "$exe" --completions "$@" 2>/dev/null
}

_az() {
    # Path-qualified invocation → always complete.
    # Bare "az" → only complete if PATH az is the same as our configured azclips binary.
    if [[ "${words[1]}" != */* && "${words[1]}" != *\\* ]]; then
        if ! _az_is_azclips_on_path; then
            _default
            return
        fi
    fi

    _az_debug "enter _az CURRENT=$CURRENT words=(${words[*]})"

    local -a candidates prefix_words subcommands params used_params available
    local group json

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups
            json="$(_az_query)"
            _az_debug "json=$json"
            if [[ -n "$json" ]]; then
                candidates=("${(@f)$(echo "$json" | jq -r 'keys[]' 2>/dev/null)}")
            fi
            _az_debug "level=0 candidates=(${candidates[*]})"
            ;;
        3)
            # Level 1: subcommands + group params
            group="${words[2]:l}"
            json="$(_az_query "$group")"
            _az_debug "json=$json"
            if [[ -n "$json" ]]; then
                local subs_output params_output
                local -a arr

                subs_output="$(echo "$json" | jq -r '._subcommands[]? // empty' 2>/dev/null)"
                if [[ -n "$subs_output" ]]; then
                    arr=("${(f)subs_output}")
                    candidates+=("${arr[@]}")
                fi

                params_output="$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)"
                if [[ -n "$params_output" ]]; then
                    arr=("${(f)params_output}")
                    candidates+=("${arr[@]}")
                fi

                _az_debug "candidates=(${candidates[*]})"
            fi
            _az_debug "level=1 group=$group candidates=(${candidates[*]})"
            ;;
        *)
            # Level 2+: parameters for the command
            group="${words[2]:l}"
            local sub="${words[3]:l}"
            # If sub is a flag, query group-level params only
            if [[ "$sub" == --* ]]; then
                json="$(_az_query "$group")"
            else
                json="$(_az_query "$group" "$sub")"
            fi
            _az_debug "json=$json"
            if [[ -n "$json" ]]; then
                local params_output
                # Try subcommand-specific params, then group-level
                if [[ "$sub" != --* ]]; then
                    params_output="$(echo "$json" | jq -r --arg s "$sub" '.[$s][]? // empty' 2>/dev/null)"
                fi
                if [[ -z "$params_output" ]]; then
                    params_output="$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)"
                fi

                if [[ -n "$params_output" ]]; then
                    local -a arr
                    arr=("${(f)params_output}")
                    params=("${arr[@]}")
                fi
            fi

            # Filter out already-used flags
            local i
            for (( i = 2; i < CURRENT; i++ )); do
                if [[ "${words[i]}" == --* ]]; then
                    used_params+=("${words[i]}")
                fi
            done

            local p already_used u
            for p in "${params[@]}"; do
                already_used=0
                for u in "${used_params[@]}"; do
                    if [[ "$p" == "$u" ]]; then
                        already_used=1
                        break
                    fi
                done
                (( ! already_used )) && available+=("$p")
            done
            candidates=("${available[@]}")
            _az_debug "level=2+ group=$group sub=$sub candidates=(${candidates[*]})"
            ;;
    esac

    (( ${#candidates[@]} > 0 )) && compadd -a candidates
    _az_debug "final candidates=(${candidates[*]}) count=${#candidates[@]}"
    return 0
}
compdef _az az az.exe

# Initialize: resolve the azclips binary (accepts optional path argument)
if _az_init "$1"; then
    echo "az tab completion registered (zsh, dynamic). Binary: $_az_azclips_exe"
fi
