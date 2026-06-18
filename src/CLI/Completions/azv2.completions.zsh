#compdef azv2
# azv2 Tab Completion for zsh (native compinit/compdef)
# Usage:
#   autoload -U compinit && compinit
#   source <path-to>/azv2.completions.zsh
# Or add to your ~/.zshrc to auto-load on every session.
# Requires: jq

# Opt-in debug logging:
#   export AZV2_COMPLETION_DEBUG=1
#   export AZV2_COMPLETION_DEBUG_LOG=/tmp/azv2_completion_debug.log
_azv2_debug() {
    [[ -n "${AZV2_COMPLETION_DEBUG:-}" ]] || return 0
    local log_file="${AZV2_COMPLETION_DEBUG_LOG:-/tmp/azv2_completion_debug.log}"
    print -r -- "[$(date '+%Y-%m-%d %H:%M:%S')] $*" >> "$log_file"
}

_azv2_subcommand_children() {
    local json_file="$1"
    local group="$2"
    shift 2

    local -a prefix_words subcommands entry_words children
    local -A seen
    local prefix_len entry child match i

    prefix_words=("$@")
    prefix_len=${#prefix_words[@]}
    subcommands=("${(@f)$(jq -r --arg g "$group" '.[$g]._subcommands[]? // empty' "$json_file" 2>/dev/null)}")

    for entry in "${subcommands[@]}"; do
        entry_words=("${(@s: :)entry}")
        (( ${#entry_words[@]} <= prefix_len )) && continue

        match=1
        for (( i = 1; i <= prefix_len; i++ )); do
            [[ "${entry_words[i]}" == "${prefix_words[i]}" ]] || {
                match=0
                break
            }
        done

        (( match )) || continue

        child="${entry_words[$((prefix_len + 1))]}"
        [[ -n "$child" && -z "${seen[$child]-}" ]] || continue

        seen[$child]=1
        children+=("$child")
    done

    if (( ${#children[@]} > 0 )); then
        print -rl -- "${children[@]}"
    fi
}

_azv2_path_params() {
    local json_file="$1"
    local group="$2"
    local command_path="$3"

    jq -r --arg g "$group" --arg p "$command_path" '.[$g][$p][]? // empty' "$json_file" 2>/dev/null
}

_azv2() {
    local json_file

    _azv2_debug "enter _azv2 CURRENT=$CURRENT words=(${words[*]})"

    # Allow the JSON path to be overridden via environment variable (useful for tests)
    if [[ -n "${AZUREV2_COMPLETIONS_JSON:-}" && -f "$AZUREV2_COMPLETIONS_JSON" ]]; then
        json_file="$AZUREV2_COMPLETIONS_JSON"
    else
        # Resolve the directory of THIS script so the sibling JSON file is found
        # regardless of where the shell's CWD is.
        local script_dir="${${(%):-%x}:A:h}"
        json_file="$script_dir/azv2.completions.json"
        if [[ ! -f "$json_file" ]]; then
            local repo_root="${script_dir:h:h:h}"
            json_file="$repo_root/bin/azv2.completions.json"
        fi
    fi
    _azv2_debug "json_file=$json_file"

    if [[ ! -f "$json_file" ]]; then
        _azv2_debug "missing completions json: $json_file"
        return 1
    fi

    local -a candidates prefix_words subcommands params used_params available
    local group command_path

    case $CURRENT in
        2)
            # Level 0: complete top-level command groups (jq returns keys sorted)
            candidates=("${(@f)$(jq -r 'keys[]' "$json_file" 2>/dev/null)}")
            _azv2_debug "level=0 candidates=(${candidates[*]})"
            ;;
        *)
            # Level 1+: complete the next subcommand token when children exist;
            # otherwise complete flags for the fully selected command path.
            group="${words[2]:l}"
            prefix_words=()
            local i
            for (( i = 2; i < CURRENT; i++ )); do
                [[ "${words[i]}" == --* ]] && break
                prefix_words+=("${words[i]:l}")
            done
            command_path="${(j: :)prefix_words}"
            _azv2_debug "level=1+ group=$group path=$command_path"

            local subcommand_output
            subcommand_output="$(_azv2_subcommand_children "$json_file" "$group" "${prefix_words[@]}")"
            if [[ -n "$subcommand_output" ]]; then
                subcommands=("${(@f)subcommand_output}")
                candidates=("${subcommands[@]}")
                _azv2_debug "next subcommand candidates=(${candidates[*]})"
            else
                local params_output
                params_output="$(_azv2_path_params "$json_file" "$group" "$command_path")"
                if [[ -n "$params_output" ]]; then
                    params=("${(@f)params_output}")
                else
                    params=()
                fi
                _azv2_debug "path params=(${params[*]})"

                # Collect flags that already appear on the command line.
                used_params=()
                for (( i = 2; i < CURRENT; i++ )); do
                    if [[ "${words[i]}" == --* ]]; then
                        used_params+=("${words[i]}")
                    fi
                done
                _azv2_debug "used_params=(${used_params[*]})"

                # Build the available list by excluding already-used flags.
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
                _azv2_debug "available=(${available[*]})"
            fi
            ;;
    esac

    (( ${#candidates[@]} > 0 )) && compadd -a candidates
    _azv2_debug "final candidates=(${candidates[*]}) count=${#candidates[@]}"
    return 0
}
compdef _azv2 azv2

echo "azv2 tab completion registered (zsh)."
