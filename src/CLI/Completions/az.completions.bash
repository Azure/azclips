# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Detection: Old Azure CLI outputs error text (with uppercase letters) to stderr for unknown
# flags like --completion-candidates; azclips returns lowercase candidates or empty (disabled).
# Uppercase in stdout marks the binary as non-azclips. The result is cached per exe.
# Completions are cached for the current shell session and invalidated if the resolved exe path
# changes (e.g., after an update) or if the config file changes.
# To refresh manually, run: _az_clear_cache

# Resolve config path once at source time.
# In WSL, Windows exe writes config to the Windows user profile, not $HOME.
_az_config_path="$HOME/.azclips/config.json"
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
# Usage: _az_query_candidates <exe> [group] [subcommand]
_az_query_candidates() {
    _az_query_result=""
    local exe="$1"; shift
    [[ -n "$exe" ]] || return 1
    # Already determined not azclips for this specific exe; skip.
    if [[ "$_az_not_azclips" == "$exe" ]]; then return 1; fi
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
_az_cache_candidates=""
_az_cache_exe=""
_az_cache_stamp=""
_az_cache_key=""
_az_query_result=""
_az_not_azclips=""

_az_completions() {
    # Resolve the actual exe from what the user typed
    local exe=""
    local typed_cmd="${COMP_WORDS[0]}"
    if type -P "$typed_cmd" >/dev/null 2>&1; then
        exe="$(type -P "$typed_cmd")"
    elif [[ -x "$typed_cmd" ]]; then
        exe="$typed_cmd"
    elif [[ -x "./$typed_cmd" ]]; then
        exe="./$typed_cmd"
    fi
    [[ -n "$exe" ]] || return

    # If we already determined this specific exe is not azclips, skip.
    if [[ "$_az_not_azclips" == "$exe" ]]; then return; fi

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
            _az_query_candidates "$exe"
            COMPREPLY=($(compgen -W "$_az_query_result" -- "$cur"))
            ;;
        2)
            # Level 1: subcommands + group-level params
            local group="${words[1]}"
            _az_query_candidates "$exe" "$group"
            COMPREPLY=($(compgen -W "$_az_query_result" -- "$cur"))
            ;;
        *)
            # Level 2+: parameters
            local group="${words[1]}"
            local sub="${words[2]}"
            if [[ "$sub" != --* ]]; then
                _az_query_candidates "$exe" "$group" "$sub"
            else
                _az_query_candidates "$exe" "$group"
            fi
            local params="$_az_query_result"

            # Filter out already-used params
            local used_params=()
            for ((i=2; i<cword; i++)); do
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

complete -F _az_completions -o default az az.exe

# Dynamic completion for path-qualified az invocations (./az, ../bin/az.exe, etc.).
# When bash encounters a command with no registered completion, _az_default_completer
# checks if the basename is az/az.exe and, if so, registers and triggers _az_completions.
_az_default_completer() {
    local cmd="${COMP_WORDS[0]}"
    # Extract basename handling both / and \ separators
    local base="${cmd##*/}"
    base="${base##*\\}"
    if [[ "$base" == "az" || "$base" == "az.exe" ]]; then
        complete -F _az_completions -o default "$cmd"
        return 124  # retry with the newly registered spec
    fi
    # Not an az command — return 124 to let bash use default completion
    return 124
}
complete -D -F _az_default_completer

# Manual cache refresh function
_az_clear_cache() {
    _az_cache_candidates=""
    _az_cache_exe=""
    _az_cache_stamp=""
    _az_cache_key=""
    _az_not_azclips=""
    echo "az completions cache cleared."
}

# Show all completions on first Tab press (like PowerShell's MenuComplete)
bind 'set show-all-if-ambiguous on' 2>/dev/null

echo "az tab completion registered (bash, dynamic)."