# az Tab Completion for Bash (Dynamic)
# Usage: source <path-to>/az.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
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

# Sets _az_query_result (avoids subshell so cache persists).
# Usage: _az_query <exe> [group]
_az_query() {
    _az_query_result=""
    local exe="$1"; shift
    [[ -n "$exe" ]] || return 1
    # Already determined not azclips for this specific exe; skip.
    if [[ "$_az_not_azclips" == "$exe" ]]; then return 1; fi
    local config_stamp
    config_stamp="$(_az_get_config_stamp)"

    # Check cache validity: invalidate on exe/config stamp changes.
    if [[ -n "$_az_completions_cache_root" && "$_az_completions_cache_exe" == "$exe" && "$_az_completions_cache_stamp" == "$config_stamp" ]]; then
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
_az_completions_cache_root=""
_az_completions_cache_exe=""
_az_completions_cache_stamp=""
_az_query_result=""
_az_not_azclips=""

_az_completions() {
    # Resolve the actual exe from what the user typed
    local exe=""
    local typed_cmd="${COMP_WORDS[0]}"
    if [[ "$typed_cmd" == */* || "$typed_cmd" == *\\* ]]; then
        # Path-qualified: normalize backslashes to forward slashes (WSL compat)
        exe="${typed_cmd//\\//}"
    else
        # Bare command: resolve via PATH
        exe="$(type -P "$typed_cmd" 2>/dev/null)"
        # If not in PATH, check current directory (Windows users expect this)
        if [[ -z "$exe" && -x "./$typed_cmd" ]]; then
            exe="./$typed_cmd"
        fi
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
            _az_query "$exe"
            local groups=""
            if [[ -n "$_az_query_result" ]]; then
                groups="$(echo "$_az_query_result" | jq -r 'keys[]' 2>/dev/null)"
            fi
            COMPREPLY=($(compgen -W "$groups" -- "$cur"))
            ;;
        2)
            # Level 1: subcommands + group-level params
            local group="${words[1]}"
            local json
            _az_query "$exe" "$group"
            json="$_az_query_result"
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
            _az_query "$exe" "$group"
            json="$_az_query_result"
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
    _az_completions_cache_root=""
    _az_completions_cache_exe=""
    _az_completions_cache_stamp=""
    _az_not_azclips=""
    echo "az completions cache cleared."
}

# Show all completions on first Tab press (like PowerShell's MenuComplete)
bind 'set show-all-if-ambiguous on' 2>/dev/null

echo "az tab completion registered (bash, dynamic)."
