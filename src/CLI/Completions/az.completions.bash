# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Set $AZCLIPS_BINARY=1 to ensure we are using autocompletion for azclips (not azure-cli).
# Completions are cached for the current shell session and invalidated if the resolved exe path changes (e.g., after an update)
# or if the completion.enabled setting changes in user config.
# To refresh manually, run: _az_clear_cache

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
_az_completions_cache_candidates=""
_az_completions_cache_exe=""
_az_completions_cache_stamp=""
_az_completions_cache_key=""
_az_query_result=""

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
            _az_query_candidates
            COMPREPLY=($(compgen -W "$_az_query_result" -- "$cur"))
            ;;
        2)
            # Level 1: subcommands + group-level params
            local group="${words[1]}"
            _az_query_candidates "$group"
            COMPREPLY=($(compgen -W "$_az_query_result" -- "$cur"))
            ;;
        *)
            # Level 2+: parameters
            local group="${words[1]}"
            local sub="${words[2]}"
            local params=""
            if [[ "$sub" != --* ]]; then
                _az_query_candidates "$group" "$sub"
            else
                _az_query_candidates "$group"
            fi
            params="$_az_query_result"

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

complete -F _az_completions az az.exe

# Manual cache refresh function
_az_clear_cache() {
    _az_completions_cache_candidates=""
    _az_completions_cache_exe=""
    _az_completions_cache_stamp=""
    _az_completions_cache_key=""
    echo "az completions cache cleared."
}

# Show all completions on first Tab press (like PowerShell's MenuComplete)
bind 'set show-all-if-ambiguous on' 2>/dev/null

echo "az tab completion registered (bash, dynamic)."
