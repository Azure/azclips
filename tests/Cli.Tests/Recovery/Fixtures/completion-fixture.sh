#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[[ -n "$AZ_TEST_QUERY_LOG" ]] || exit 2
printf '%s\n' "$*" >> "$AZ_TEST_QUERY_LOG"
case "$1" in
    --completion-values)
        printf '%s\n' "${AZ_TEST_IDS:-}"
        exit "${AZ_TEST_VALUES_EXIT_CODE:-0}"
        ;;
    --completion-candidates)
        [[ "${AZ_TEST_MODE:-}" != disabled ]] || exit 0
        if [[ "${AZ_TEST_MODE:-}" == old ]]; then
            printf '%s\n' 'ERROR: unsupported completion protocol'
            exit 2
        fi
        shift
        case "$*" in
            '') printf '%s\n' 'account deployment vm' ;;
            deployment) printf '%s\n' group ;;
            'deployment group') printf '%s\n' 'create what-if' ;;
            'deployment group create') printf '%s\n' '--name --location --mode' ;;
            vm) printf '%s\n' 'show list' ;;
            'vm show') printf '%s\n' '--name --ids' ;;
            troubleshoot) printf '%s\n' '--id --list --style show' ;;
            'troubleshoot show') printf '%s\n' '--id' ;;
        esac
        ;;
    *) exit 2 ;;
esac
