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
#
# Detection: The script auto-detects azclips by calling `az --completions`.
# If the binary returns valid JSON, it is treated as azclips; otherwise completions are skipped.
# The result is cached per session so detection only runs once.
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

# Resolve config path once at source time.
# In WSL, Windows exe writes config to the Windows user profile, not $HOME.
typeset -g _az_config_path="$HOME/.azclips/config.json"
if [[ -f /proc/version ]] && grep -qi microsoft /proc/version 2>/dev/null; then
    _az_wsl_profile="$(cmd.exe /c 'echo %USERPROFILE%' 2>/dev/null | tr -d '\r\n')"
    if [[ -n "$_az_wsl_profile" ]] && command -v wslpath >/dev/null 2>&1; then
        _az_wsl_home="$(wslpath "$_az_wsl_profile" 2>/dev/null)"
        [[ -n "$_az_wsl_home" ]] && _az_config_path="$_az_wsl_home/.azclips/config.json"
    fi
    unset _az_wsl_profile _az_wsl_home
fi

_az_get_config_stamp() {
    local config_path="$_az_config_path"

    # Missing config means defaults apply (completion.enabled=true).
    if [[ ! -f "$config_path" ]]; then
        echo "$config_path|enabled:true|missing"
        return
    fi

    # Prefer semantic invalidation by reading completion.enabled.
    if (( $+commands[jq] )); then
        local enabled
        enabled="$(jq -r '
            if has("completion") and (.completion|type=="object") and (.completion|has("enabled")) then
                .completion.enabled
            elif has("completion.enabled") then
                ."completion.enabled"
            else
                true
            end
        ' "$config_path" 2>/dev/null)"

        if [[ -n "$enabled" && "$enabled" != "null" ]]; then
            if [[ "$enabled" == "true" || "$enabled" == "false" ]]; then
                echo "$config_path|enabled:$enabled"
                return
            fi
        fi
    fi

    # Fallback to metadata stamp when semantic read is unavailable/invalid.
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
# Usage: _az_query <exe> [group]
_az_query() {
    _az_query_result=""
    local exe="$1"; shift
    [[ -n "$exe" ]] || return 1
    # Already determined not azclips for this specific exe; skip.
    if [[ "${_az_not_azclips:-}" == "$exe" ]]; then return 1; fi
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"
    
    # Check cache validity: invalidate on exe/config stamp changes.
    if [[ -n "${_az_completions_cache_root:-}" && "$_az_completions_cache_exe" == "$exe" && "$_az_completions_cache_stamp" == "$config_stamp" ]]; then
        # Navigate cached tree by context args using jq
        if [[ $# -eq 0 ]]; then
            _az_query_result="$_az_completions_cache_root"
            return 0
        fi
        local subtree
        subtree="$(echo "$_az_completions_cache_root" | jq -e --arg k "$1" '.[$k]' 2>/dev/null)"
        if [[ $? -eq 0 && -n "$subtree" && "$subtree" != "null" ]]; then
            _az_query_result="$subtree"
        fi
        # Cache is valid; don't re-query even if key was missing
        return 0
    fi

    # Cache miss: query fresh root tree and cache it
    local raw_output
    raw_output="$("$exe" --completions 2>/dev/null)"
    # Validate output is JSON (old Azure CLI returns error text, not JSON)
    local root_json
    root_json="$(echo "$raw_output" | jq -e 'type == "object"' >/dev/null 2>&1 && echo "$raw_output")"
    if [[ -z "$root_json" ]]; then
        # Not valid JSON → not azclips; remember to skip future attempts for this exe
        _az_not_azclips="$exe"
        return 1
    fi
    _az_completions_cache_root="$root_json"
    _az_completions_cache_exe="$exe"
    _az_completions_cache_stamp="$config_stamp"

    # Return the requested subtree (or root if no args)
    if [[ $# -eq 0 ]]; then
        _az_query_result="$root_json"
    else
        _az_query_result="$(echo "$root_json" | jq -e --arg k "$1" '.[$k]' 2>/dev/null)"
    fi
}

# Session cache for completions tree (persists until exe path changes).
typeset -g _az_completions_cache_root=""
typeset -g _az_completions_cache_exe=""
typeset -g _az_completions_cache_stamp=""
typeset -g _az_query_result=""
typeset -g _az_not_azclips=""

_az() {
    # Resolve the actual exe from what the user typed
    local exe=""
    local typed_cmd="${words[1]}"
    if [[ "$typed_cmd" == */* || "$typed_cmd" == *\\* ]]; then
        # Path-qualified: normalize backslashes to forward slashes (WSL compat)
        exe="${typed_cmd//\\//}"
    else
        # Bare command: resolve via PATH
        if (( $+commands[$typed_cmd] )); then
            exe="${commands[$typed_cmd]}"
        fi
        # If not in PATH, check current directory (Windows users expect this)
        if [[ -z "$exe" && -x "./$typed_cmd" ]]; then
            exe="./$typed_cmd"
        fi
    fi
    if [[ -z "$exe" ]]; then
        _default
        return
    fi

    # If we already determined this specific exe is not azclips, fall back to default.
    if [[ "${_az_not_azclips:-}" == "$exe" ]]; then
        _default
        return
    fi

    _az_debug "enter _az CURRENT=$CURRENT words=(${words[*]})"

    local -a candidates prefix_words subcommands params used_params available
    local group json

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups
            _az_query "$exe"
            json="$_az_query_result"
            _az_debug "json=$json"
            if [[ -n "$json" ]]; then
                candidates=("${(@f)$(echo "$json" | jq -r 'keys[]' 2>/dev/null)}")
            fi
            _az_debug "level=0 candidates=(${candidates[*]})"
            ;;
        3)
            # Level 1: subcommands + group params
            group="${words[2]:l}"
            _az_query "$exe" "$group"
            json="$_az_query_result"
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
            _az_query "$exe" "$group"
            json="$_az_query_result"
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

# Also handle path-qualified az invocations (./az, ../bin/az.exe, etc.)
# compdef -p registers a pattern-based completion that matches commands by path suffix.
compdef '_az_path_completer' -p '*/az'
compdef '_az_path_completer' -p '*/az.exe'

_az_path_completer() {
    _az "$@"
}

# Manual cache refresh function
_az_clear_cache() {
    _az_completions_cache_root=""
    _az_completions_cache_exe=""
    _az_completions_cache_stamp=""
    _az_not_azclips=""
    echo "az completions cache cleared."
}

echo "az tab completion registered (zsh, dynamic)."
