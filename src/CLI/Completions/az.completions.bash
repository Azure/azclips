# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
# Requires: jq
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
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

    # Prefer semantic invalidation by reading completion.enabled.
    if command -v jq >/dev/null 2>&1; then
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
            # Normalize to strict boolean strings for stable stamps.
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

_az_query() {
    local exe
    exe="$(_az_resolve_exe)"
    [[ -n "$exe" ]] || return 1
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"

    # Check cache validity: invalidate on exe/config stamp changes.
    if [[ -n "$_az_completions_cache_root" && "$_az_completions_cache_exe" == "$exe" && "$_az_completions_cache_stamp" == "$config_stamp" ]]; then
        # Navigate cached tree by context args using jq
        if [[ $# -eq 0 ]]; then
            echo "$_az_completions_cache_root"
            return 0
        fi
        local subtree
        subtree="$(echo "$_az_completions_cache_root" | jq -e --arg k "$1" '.[$k]' 2>/dev/null)"
        if [[ $? -eq 0 && -n "$subtree" && "$subtree" != "null" ]]; then
            echo "$subtree"
        fi
        # Cache is valid; don't re-query even if key was missing
        return 0
    fi

    # Cache miss: query fresh root tree and cache it
    local root_json
    root_json="$("$exe" --completions 2>/dev/null)"
    if [[ -n "$root_json" ]]; then
        _az_completions_cache_root="$root_json"
        _az_completions_cache_exe="$exe"
        _az_completions_cache_stamp="$config_stamp"
    fi

    # Return the requested subtree (or root if no args)
    if [[ $# -eq 0 ]]; then
        echo "$root_json"
    else
        echo "$root_json" | jq -e --arg k "$1" '.[$k]' 2>/dev/null
    fi
}

# Session cache for completions tree (persists until exe path changes)
_az_completions_cache_root=""
_az_completions_cache_exe=""
_az_completions_cache_stamp=""

# Always query `az --completions` at tab time (cached).
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
            # Level 1: subcommands + group-level params
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
            # Level 2+: parameters
            local group="${words[1]}"
            local sub="${words[2]}"
            local json
            json="$(_az_query "$group")"
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

# Manual cache refresh function
_az_clear_cache() {
    _az_completions_cache_root=""
    _az_completions_cache_exe=""
    _az_completions_cache_stamp=""
    echo "az completions cache cleared."
}

# Show all completions on first Tab press (like PowerShell's MenuComplete)
bind 'set show-all-if-ambiguous on' 2>/dev/null

echo "az tab completion registered (bash, dynamic)."
