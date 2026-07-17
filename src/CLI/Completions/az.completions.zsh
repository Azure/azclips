#compdef az
# az Tab Completion for zsh (Dynamic)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/az.completions.zsh
# Or add to your ~/.zshrc to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [command-path...]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Detection: Old Azure CLI outputs error text (with uppercase letters) to stderr for unknown
# flags like --completion-candidates; azclips returns lowercase candidates or empty (disabled).
# Uppercase in stdout marks the binary as non-azclips. The result is cached per exe.
# Completions are cached for the current shell session and invalidated if the resolved exe path
# changes (e.g., after an update) or if the config file changes.
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
        echo "$config_path|missing"
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
        echo "$config_path|$size|$mtime"
    else
        echo "$config_path|error"
    fi
}

# Sets _az_query_result (avoids subshell so cache persists).
# Usage: _az_query_candidates <exe> [command-path...]
_az_query_candidates() {
    _az_query_result=""
    local exe="$1"; shift
    [[ -n "$exe" ]] || return 1
    # Already determined not azclips for this specific exe; skip.
    if [[ "${_az_not_azclips:-}" == "$exe" ]]; then return 1; fi
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"
    local cache_key="$*"

    # Check cache validity: invalidate on exe/config stamp changes.
    if [[ "$_az_cache_exe" == "$exe" && "$_az_cache_stamp" == "$config_stamp" && "$_az_cache_key" == "$cache_key" ]]; then
        _az_query_result="$_az_cache_candidates"
        return 0
    fi

    local raw
    raw="$("$exe" --completion-candidates "$@" 2>/dev/null)"
    raw="${raw//$'\r'/}"

    # Detection: old Azure CLI outputs error text with uppercase letters to stderr
    # (suppressed by 2>/dev/null -> empty stdout) or to stdout. azclips outputs
    # lowercase candidates or empty (disabled). Uppercase in output -> not azclips.
    if [[ -n "$raw" && "$raw" =~ [A-Z] ]]; then
        _az_not_azclips="$exe"
        _az_query_result=""
        return 1
    fi

    _az_query_result="$raw"
    _az_cache_candidates="$_az_query_result"
    _az_cache_exe="$exe"
    _az_cache_stamp="$config_stamp"
    _az_cache_key="$cache_key"
}

# Session cache (persists until exe path/config stamp/context changes).
typeset -g _az_cache_candidates=""
typeset -g _az_cache_exe=""
typeset -g _az_cache_stamp=""
typeset -g _az_cache_key=""
typeset -g _az_query_result=""
typeset -g _az_not_azclips=""

_az() {
    # Resolve the actual exe from what the user typed
    local exe=""
    local typed_cmd="${words[1]}"
    if (( $+commands[$typed_cmd] )); then
        exe="${commands[$typed_cmd]}"
    elif [[ -x "$typed_cmd" ]]; then
        exe="$typed_cmd"
    elif [[ -x "./$typed_cmd" ]]; then
        exe="./$typed_cmd"
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

    local -a candidates used_params raw cmd_path
    local i candidate used_param already_used

    # Build the command path: the leading run of non-flag words after the command,
    # excluding the word currently being completed. The CLI walks this path through
    # the command tree, so command groups of any depth resolve (top-level groups,
    # intermediate groups like `deployment group`, and leaf commands).
    for (( i = 2; i < CURRENT; i++ )); do
        [[ "${words[i]}" == -* ]] && break
        cmd_path+=("${words[i]:l}")
    done

    _az_query_candidates "$exe" "${cmd_path[@]}"
    if [[ -n "$_az_query_result" ]]; then
        raw=( ${=_az_query_result} )
    fi

    # Filter out flags already present on the command line.
    for (( i = 2; i < CURRENT; i++ )); do
        [[ "${words[i]}" == --* ]] && used_params+=("${words[i]}")
    done

    for candidate in "${raw[@]}"; do
        already_used=0
        for used_param in "${used_params[@]}"; do
            if [[ "$candidate" == "$used_param" ]]; then
                already_used=1
                break
            fi
        done
        (( ! already_used )) && candidates+=("$candidate")
    done
    _az_debug "cmd_path=(${cmd_path[*]}) candidates=(${candidates[*]})"

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
    _az_cache_candidates=""
    _az_cache_exe=""
    _az_cache_stamp=""
    _az_cache_key=""
    _az_not_azclips=""
    echo "az completions cache cleared."
}

echo "az tab completion registered (zsh, dynamic)."