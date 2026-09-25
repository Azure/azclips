#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Uses real Bash/compgen with a fake executable. No az installation or key store is used.
set -eo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)"
test_root="$root/.test-artifacts/bash-completion-$$"
mkdir -p "$test_root/home" "$test_root/history"
trap 'rm -rf -- "$test_root"' EXIT
export HOME="$test_root/home" AZCLIPS_HOME="$test_root/history"
export AZ_TEST_QUERY_LOG="$test_root/queries"
exe="$root/tests/Cli.Tests/Recovery/Fixtures/completion-fixture.sh"
chmod +x "$exe"
baseline="${1:-83f27105bebeb1619b06b4871b9d970d186530d5}"
before_source="$(git -C "$root" show "$baseline:src/CLI/Completions/az.completions.bash")"
after_source="$(< "$root/src/CLI/Completions/az.completions.bash")"

ordinary_suite() (
    eval "$1"
    : > "$AZ_TEST_QUERY_LOG"
    export AZ_TEST_MODE=enabled AZ_TEST_STAMP=enabled
    _az_get_config_stamp() { printf '%s\n' "$AZ_TEST_STAMP"; }
    _init_completion() {
        cur="${COMP_WORDS[COMP_CWORD]}"
        prev="${COMP_WORDS[COMP_CWORD-1]}"
        words=("${COMP_WORDS[@]}")
        cword=$COMP_CWORD
    }
    check() {
        local label="$1"; shift
        COMP_CWORD="$1"; shift
        COMP_WORDS=("$@")
        COMP_LINE="${COMP_WORDS[*]}"
        COMP_POINT=${#COMP_LINE}
        COMPREPLY=(unchanged-sentinel)
        _az_completions || true
        printf '%s|%s|' "$label" "$(wc -l < "$AZ_TEST_QUERY_LOG")"
        printf '<%s>' "${COMPREPLY[@]}"
        printf '\n'
    }
    check root 1 "$exe" ''
    check root-cache 1 "$exe" v
    check group 2 "$exe" deployment ''
    check deep 3 "$exe" deployment group ''
    check params 4 "$exe" deployment group create --
    check used 6 "$exe" deployment group create --name value --
    check equals 5 "$exe" deployment group create --name=value --
    check case 4 "$exe" DEPLOYMENT GROUP create --M
    check cursor 4 "$exe" deployment group create --na --mode later
    check whitespace 4 "$exe" deployment group create $'--name\t'
    check path 2 "$root/tests/Cli.Tests/Recovery/Fixtures/../Fixtures/completion-fixture.sh" vm ''
    check missing-exe 1 "$test_root/not-an-executable" ''
    export AZ_TEST_MODE=disabled AZ_TEST_STAMP=disabled
    check disabled 1 "$exe" ''
    check disabled-cache 1 "$exe" ''
    export AZ_TEST_MODE=enabled AZ_TEST_STAMP=enabled-again
    check enabled 1 "$exe" ''
    export AZ_TEST_MODE=old AZ_TEST_STAMP=old
    check old-cli 1 "$exe" ''
    check old-cli-cache 1 "$exe" ''
    printf 'queries:\n'
    cat "$AZ_TEST_QUERY_LOG"
)

before="$(ordinary_suite "$before_source")"
after="$(ordinary_suite "$after_source")"
if [[ "$before" != "$after" ]]; then
    printf 'Bash completion differs from %s.\nBASELINE:\n%s\nCURRENT:\n%s\n' "$baseline" "$before" "$after" >&2
    exit 1
fi

eval "$after_source"
: > "$AZ_TEST_QUERY_LOG"
export AZ_TEST_MODE=enabled
first=42-2026-0924-0000-0002
second=2-2026-0924-0000-0001
export AZ_TEST_IDS="$first"$'\n'"$second"
unset -f _init_completion 2>/dev/null || true

complete_ids() {
    local expected="$1"; shift
    COMP_WORDS=("$exe" "$@")
    COMP_CWORD=$((${#COMP_WORDS[@]} - 1))
    COMP_LINE="${COMP_WORDS[*]}"
    COMP_POINT=${#COMP_LINE}
    COMPREPLY=(stale-value)
    _az_completions || true
    if [[ "${COMPREPLY[*]}" != "$expected" ]]; then
        printf 'Wrong ID candidates for %s: expected [%s], got [%s].\n' "$*" "$expected" "${COMPREPLY[*]}" >&2
        exit 1
    fi
}

complete_ids "$first $second" troubleshoot --id ''
complete_ids "$first" troubleshoot --id 42
complete_ids "$second" troubleshoot show --id 2
complete_ids "$first" TROUBLESHOOT SHOW --ID 42
complete_ids "--id=$first" troubleshoot --id=42
complete_ids "--id=$first --id=$second" troubleshoot show --id=
complete_ids "$first" troubleshoot --id = 42
complete_ids "$first $second" troubleshoot show --id =
complete_ids '' troubleshoot --id nomatch
complete_ids '' troubleshoot --id '*0924*'
export AZ_TEST_IDS="$second"
complete_ids "$second" troubleshoot --id ''
export AZCLIPS_HOME="$test_root/history-two" AZ_TEST_IDS="$first"
complete_ids "$first" troubleshoot --id ''
export AZ_TEST_VALUES_EXIT_CODE=1
complete_ids '' troubleshoot --id ''
export AZ_TEST_VALUES_EXIT_CODE=0 AZ_TEST_IDS='$(unsafe)'
complete_ids '' troubleshoot --id ''
if grep -v -x -- '--completion-values troubleshoot --id' "$AZ_TEST_QUERY_LOG"; then
    printf 'ID fragments must not be filtered by the endpoint or enter the static cache.\n' >&2
    exit 1
fi
count_before="$(wc -l < "$AZ_TEST_QUERY_LOG")"
complete_ids '--name --ids' vm show --id ''
if [[ "$(grep -c -- '^--completion-values ' "$AZ_TEST_QUERY_LOG")" != "$count_before" ]]; then
    printf 'Ordinary completion queried history.\n' >&2
    exit 1
fi
printf 'Bash ID completion and ordinary compatibility with %s passed.\n' "$baseline"
