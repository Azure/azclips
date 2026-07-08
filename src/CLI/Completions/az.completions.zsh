#compdef az
# az Tab Completion for zsh (Dynamic)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/az.completions.zsh
# Or add to your ~/.zshrc to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Set $AZCLIPS_BINARY=1 to ensure we are using autocompletion for azclips (not azure-cli).
# Completions are cached for the current shell session and invalidated if the resolved exe path changes (e.g., after an update)
# or if the completion.enabled setting changes in user config.
# To refresh manually, run: _az_clear_cache
#
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

_az_is_azclips() {
    # Environment variable override: set AZCLIPS_BINARY=1 for azclips, 0 for old az
    if [[ "$AZCLIPS_BINARY" == "1" ]]; then return 0; fi
    if [[ "$AZCLIPS_BINARY" == "0" ]]; then return 1; fi
    # Default: assume old az (unset env var means old az completions)
    return 1
}

_az_get_config_stamp() {
    local config_path="$HOME/.azclips/config.json"

    # Missing config means defaults apply (completion.enabled=true).
    if [[ ! -f "$config_path" ]]; then
        echo "$config_path|enabled:true|missing"
        return
    fi

    # Use metadata only; the CLI remains the source of truth for completion.enabled.
    local size mtime

    # GNU stat first, then BSD/macOS stat.
    size="$(stat -c %s "$config_path" 2>/dev/null)"
    if [[ -z "$size" ]]; then
        size="$(stat -f %z "$config_path" 2>/dev/null)"
    fi
    mtime="$(stat -c %Y "$config_path" 2>/dev/null)"
    if [[ -z "$mtime" ]]; then
        mtime="$(stat -f %m "$config_path" 2>/dev/null)"
    fi

    if [[ -n "$size" || -n "$mtime" ]]; then
        echo "$config_path|fallback-meta|$size|$mtime"
    else
        echo "$config_path|error"
    fi
}

# Sets _az_query_result (avoids subshell so cache persists).
_az_query_candidates() {
    _az_query_result=""
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"
    local cache_key="$*"
    
    # Check cache validity: invalidate on exe/config stamp changes.
    if [[ "$_az_completions_cache_exe" == "$exe" && "$_az_completions_cache_stamp" == "$config_stamp" && "$_az_completions_cache_key" == "$cache_key" ]]; then
        _az_query_result="$_az_completions_cache_candidates"
        return 0
    fi

    _az_query_result="$("$exe" --completion-candidates "$@" 2>/dev/null)"
    _az_completions_cache_candidates="$_az_query_result"
    _az_completions_cache_exe="$exe"
    _az_completions_cache_stamp="$config_stamp"
    _az_completions_cache_key="$cache_key"
}

# Session cache for candidate output (persists until exe path/config stamp/context changes).
typeset -g _az_completions_cache_candidates=""
typeset -g _az_completions_cache_exe=""
typeset -g _az_completions_cache_stamp=""
typeset -g _az_completions_cache_key=""
typeset -g _az_query_result=""

_az() {
    # Path-qualified invocation → always complete.
    # Bare "az" → check PATH to ensure it's azclips, not system azure-cli.
    if [[ "${words[1]}" != */* && "${words[1]}" != *\\* ]]; then
        if ! _az_is_azclips; then
            _default
            return
        fi
    fi

    _az_debug "enter _az CURRENT=$CURRENT words=(${words[*]})"

    local -a candidates prefix_words subcommands params used_params available
    local group

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups
            _az_query_candidates
            candidates=("${(@f)_az_query_result}")
            _az_debug "level=0 candidates=(${candidates[*]})"
            ;;
        3)
            # Level 1: subcommands + group params
            group="${words[2]:l}"
            _az_query_candidates "$group"
            candidates=("${(@f)_az_query_result}")
            _az_debug "level=1 group=$group candidates=(${candidates[*]})"
            ;;
        *)
            # Level 2+: parameters for the command
            group="${words[2]:l}"
            local sub="${words[3]:l}"
            if [[ "$sub" != --* ]]; then
                _az_query_candidates "$group" "$sub"
            else
                _az_query_candidates "$group"
            fi
            params=("${(@f)_az_query_result}")

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

# Manual cache refresh function
_az_clear_cache() {
    _az_completions_cache_candidates=""
    _az_completions_cache_exe=""
    _az_completions_cache_stamp=""
    _az_completions_cache_key=""
    echo "az completions cache cleared."
}

echo "az tab completion registered (zsh, dynamic)."
