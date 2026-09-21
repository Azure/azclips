# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [command-path...]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Detection: Old Azure CLI outputs error text (with uppercase letters) to stderr for unknown
# flags like --completion-candidates; azclips returns lowercase candidates or empty (disabled).
# Uppercase in stdout marks the binary as non-azclips. The result is cached per exe.
# Completions are cached for the current shell session and invalidated if the resolved exe path
# changes (e.g., after an update) or if the config file changes.
# Saved failure IDs use `--completion-values troubleshoot --id [prefix]` afresh
# on every Tab press, separately from the static command-candidate cache.
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
# Usage: _az_query_candidates <exe> [command-path...]
_az_query_candidates() {
    _az_query_result=""
    local exe="$1"; shift
    [[ -n "$exe" ]] || return 1
    # Already determined not azclips for this specific exe; skip.
    if [[ "$_az_not_azclips" == "$exe" ]]; then return 1; fi
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"
    local cache_key="$*"

    # Check cache validity: invalidate on exe/config stamp/context changes.
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

# ID output is untrusted: require a successful query containing only complete GUIDs.
_az_query_troubleshoot_ids() {
    _az_query_result=""
    local exe="$1" prefix="$2"
    [[ -n "$exe" && "$_az_not_azclips" != "$exe" ]] || return 1
    [[ "$prefix" =~ ^[0-9a-fA-F]{0,32}$ ]] || return 1

    local raw candidate
    raw="$("$exe" --completion-values troubleshoot --id "$prefix" 2>/dev/null)" || return 1
    raw="${raw//$'\r'/}"
    [[ "$raw" =~ ^[[:space:]0-9a-fA-F]*$ ]] || return 1
    for candidate in $raw; do
        [[ "$candidate" =~ ^[0-9a-fA-F]{32}$ && "$candidate" != 00000000000000000000000000000000 ]] || return 1
    done
    _az_query_result="$raw"
}

# Session cache (persists until exe path/config stamp/context changes).
_az_cache_candidates=""
_az_cache_exe=""
_az_cache_stamp=""
_az_cache_key=""
_az_query_result=""
_az_not_azclips=""

_az_completions() {
    COMPREPLY=()
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

    local completing_id=false id_prefix="" completion_prefix=""
    if [[ "${words[1]}" == [tT][rR][oO][uU][bB][lL][eE][sS][hH][oO][oO][tT] ]] && (( cword > 1 )); then
        if [[ "$cur" == --[iI][dD]=* ]]; then
            completing_id=true
            id_prefix="${cur#*=}"
            completion_prefix="${cur%%=*}="
        elif [[ "$prev" == --[iI][dD] ]]; then
            completing_id=true
            [[ "$cur" != "=" ]] && id_prefix="$cur"
        elif [[ "$prev" == "=" && "${words[cword-2]}" == --[iI][dD] ]]; then
            completing_id=true
            id_prefix="$cur"
        fi
    fi
    if $completing_id; then
        _az_query_troubleshoot_ids "$exe" "$id_prefix" || return
        local id
        for id in $_az_query_result; do
            COMPREPLY+=("$completion_prefix$id")
        done
        return
    fi

    # Build the command path: the leading run of non-flag words after the exe,
    # excluding the word currently being completed. The CLI walks this path through
    # the command tree, so command groups of any depth resolve (top-level groups,
    # intermediate groups like `deployment group`, and leaf commands).
    local -a cmd_path=()
    local i
    for ((i=1; i<cword; i++)); do
        [[ "${words[i]}" == -* ]] && break
        cmd_path+=("${words[i]}")
    done

    _az_query_candidates "$exe" "${cmd_path[@]}"
    if [[ -z "$_az_query_result" ]]; then
        COMPREPLY=()
        return
    fi

    # Filter out flags already present on the command line.
    local -a used_params=()
    for ((i=1; i<cword; i++)); do
        if [[ "${words[i]}" == --* ]]; then
            used_params+=("${words[i]%%=*}")
        fi
    done

    local available=""
    local candidate used_param already_used
    for candidate in $_az_query_result; do
        already_used=false
        for used_param in "${used_params[@]}"; do
            if [[ "$candidate" == "$used_param" ]]; then
                already_used=true
                break
            fi
        done
        if ! $already_used; then
            available="$available $candidate"
        fi
    done

    COMPREPLY=($(compgen -W "$available" -- "$cur"))
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
