#compdef az
# az Tab Completion for zsh (Dynamic)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/az.completions.zsh
# Or add to your ~/.zshrc to auto-load on every session.
# Requires: jq
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.

# Opt-in debug logging:
#   export AZ_COMPLETION_DEBUG=1
#   export AZ_COMPLETION_DEBUG_LOG=/tmp/az_completion_debug.log
_az_debug() {
    [[ -n "${AZ_COMPLETION_DEBUG:-}" ]] || return 0
    local log_file="${AZ_COMPLETION_DEBUG_LOG:-/tmp/az_completion_debug.log}"
    print -r -- "[$(date '+%Y-%m-%d %H:%M:%S')] $*" >> "$log_file"
}

_az_resolve_exe() {
    local script_dir="${${(%):-%x}:A:h}"
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
        dir="${dir:h}"
    done
    # 3. Fall back to PATH
    if (( $+commands[az] )); then
        echo "${commands[az]}"
    fi
}

_az_query() {
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    "$exe" --completions "$@" 2>/dev/null
}

_az() {
    _az_debug "enter _az CURRENT=$CURRENT words=(${words[*]})"

    local -a candidates prefix_words subcommands params used_params available
    local group json

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups
            json="$(_az_query)"
            if [[ -n "$json" ]]; then
                candidates=("${(@f)$(echo "$json" | jq -r 'keys[]' 2>/dev/null)}")
            fi
            _az_debug "level=0 candidates=(${candidates[*]})"
            ;;
        3)
            # Level 1: subcommands + group params
            group="${words[2]:l}"
            json="$(_az_query "$group")"
            if [[ -n "$json" ]]; then
                local subs_output params_output
                subs_output="$(echo "$json" | jq -r '._subcommands[]? // empty' 2>/dev/null)"
                params_output="$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)"
                [[ -n "$subs_output" ]] && candidates+=("${(@f)$subs_output}")
                [[ -n "$params_output" ]] && candidates+=("${(@f)$params_output}")
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
            if [[ -n "$json" ]]; then
                local params_output
                # Try subcommand-specific params, then group-level
                if [[ "$sub" != --* ]]; then
                    params_output="$(echo "$json" | jq -r --arg s "$sub" '.[$s][]? // empty' 2>/dev/null)"
                fi
                if [[ -z "$params_output" ]]; then
                    params_output="$(echo "$json" | jq -r '._params[]? // empty' 2>/dev/null)"
                fi
                [[ -n "$params_output" ]] && params=("${(@f)$params_output}")
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

echo "az tab completion registered (zsh, dynamic)."
