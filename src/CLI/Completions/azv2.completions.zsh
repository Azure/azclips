#compdef azv2
# azv2 Tab Completion for zsh (native compinit/compdef)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/azv2.completions.zsh
# Or add to your ~/.zshrc to auto-load on every session.
# Requires: jq

_azv2() {
    local json_file

    # Allow the JSON path to be overridden via environment variable (useful for tests)
    if [[ -n "${AZUREV2_COMPLETIONS_JSON:-}" && -f "$AZUREV2_COMPLETIONS_JSON" ]]; then
        json_file="$AZUREV2_COMPLETIONS_JSON"
    else
        # Resolve the directory of THIS script so the sibling JSON file is found
        # regardless of where the shell's CWD is.
        local script_dir="${${(%):-%x}:A:h}"
        json_file="$script_dir/azv2.completions.json"
    fi

    if [[ ! -f "$json_file" ]]; then
        return 1
    fi

    local -a candidates subcommands params used_params available
    local group sub start_index

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups (jq returns keys sorted)
            candidates=(${(f)$(jq -r 'keys[]' "$json_file" 2>/dev/null)})
            ;;
        3)
            # Level 1: complete subcommands + group-level params
            group="${words[2]:l}"
            subcommands=(${(f)$(jq -r --arg g "$group" '.[$g]._subcommands[]? // empty' "$json_file" 2>/dev/null)})
            params=(${(f)$(jq -r --arg g "$group" '.[$g]._params[]? // empty' "$json_file" 2>/dev/null)})
            candidates=(${subcommands[@]} ${params[@]})
            ;;
        *)
            # Level 2+: complete parameters, filtering out already-used flags
            group="${words[2]:l}"
            sub="${words[3]:l}"

            if jq -e --arg g "$group" --arg s "$sub" '.[$g][$s]' "$json_file" >/dev/null 2>&1; then
                # Valid subcommand found — use its specific parameter list
                params=(${(f)$(jq -r --arg g "$group" --arg s "$sub" '.[$g][$s][]? // empty' "$json_file" 2>/dev/null)})
                start_index=4
            else
                # No recognised subcommand — fall back to group-level params
                params=(${(f)$(jq -r --arg g "$group" '.[$g]._params[]? // empty' "$json_file" 2>/dev/null)})
                start_index=3
            fi

            # Collect flags that already appear on the command line
            local i
            for (( i = start_index; i < CURRENT; i++ )); do
                if [[ "${words[i]}" == --* ]]; then
                    used_params+=("${words[i]}")
                fi
            done

            # Build the available list by excluding already-used flags
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
            ;;
    esac

    (( ${#candidates[@]} > 0 )) && compadd -a candidates
    return 0
}

compdef _azv2 azv2

echo "azv2 tab completion registered (zsh)."
