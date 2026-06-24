# azv2 Tab Completion for Bash
# Usage: source <path-to>/azv2.completions.bash
# Or add to your ~/.bashrc to auto-load on every session.
# Requires: jq

_azv2_completions() {
    local cur prev words cword
    _init_completion 2>/dev/null || {
        COMPREPLY=()
        cur="${COMP_WORDS[COMP_CWORD]}"
        prev="${COMP_WORDS[COMP_CWORD-1]}"
        words=("${COMP_WORDS[@]}")
        cword=$COMP_CWORD
    }

    local json_file="${BASH_SOURCE%/*}/azv2.completions.json"
    if [[ ! -f "$json_file" ]]; then
        return
    fi

    case $cword in
        1)
            # Level 0: command groups
            COMPREPLY=($(compgen -W "$(jq -r 'keys[]' "$json_file" 2>/dev/null)" -- "$cur"))
            ;;
        2)
            # Level 1: subcommands + group-level params
            local group="${words[1]}"
            local subs
            subs=$(jq -r --arg g "$group" '.[$g]._subcommands[]? // empty' "$json_file" 2>/dev/null)
            local params
            params=$(jq -r --arg g "$group" '.[$g]._params[]? // empty' "$json_file" 2>/dev/null)
            COMPREPLY=($(compgen -W "$subs $params" -- "$cur"))
            ;;
        *)
            # Level 2+: parameters
            local group="${words[1]}"
            local sub="${words[2]}"
            local params

            # Check if sub is a valid subcommand
            if jq -e --arg g "$group" --arg s "$sub" '.[$g][$s]' "$json_file" &>/dev/null; then
                params=$(jq -r --arg g "$group" --arg s "$sub" '.[$g][$s][]? // empty' "$json_file" 2>/dev/null)
            else
                params=$(jq -r --arg g "$group" '.[$g]._params[]? // empty' "$json_file" 2>/dev/null)
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

complete -F _azv2_completions azv2

echo "azv2 tab completion registered (bash)."
