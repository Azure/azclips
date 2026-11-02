#!/usr/bin/env bash
set -euo pipefail

: "${AZCLIPS_HOME:?Isolated test home is required}"
: "${TEST_SUBSCRIPTION:?Test subscription is required}"
: "${TEST_RUN_ID:?Unique pipeline job ID is required}"
AZ="${AZCLIPS_EXECUTABLE:-$HOME/.local/share/azcli/az}"
rg="rg-azclips-scenario6"
suffix=$(printf '%s' "$TEST_RUN_ID" | sha256sum | cut -c1-18)
storage="clips$suffix"
deployment="scenario6-$suffix"
scope="/subscriptions/$TEST_SUBSCRIPTION/resourceGroups/$rg"
storage_id="$scope/providers/Microsoft.Storage/storageAccounts/$storage"
storage_uri="$storage_id?api-version=2023-01-01"
deployment_uri="$scope/providers/Microsoft.Resources/deployments/$deployment?api-version=2022-09-01"
marker="$AZCLIPS_HOME/scenario6-resources-owned"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

expect_missing() {
    local response
    if response=$("$AZ" rest --method GET --uri "$1" --output json); then
        echo "ERROR: Expected a not-found response for $1." >&2
        return 1
    fi
    if ! jq -e '.statusCode == 404 and
        (.json.error.code == "ResourceNotFound" or .json.error.code == "DeploymentNotFound")' \
        <<< "$response" > /dev/null; then
        echo "ERROR: Lookup failed for a reason other than the expected ARM not-found error." >&2
        printf '%s\n' "$response" >&2
        return 1
    fi
}

delete_and_wait() {
    local uri="$1" response status
    if response=$("$AZ" rest --method DELETE --uri "$uri" --output json); then
        status=$(jq -er '.statusCode' <<< "$response") || return 1
        if [[ "$status" != 200 && "$status" != 202 && "$status" != 204 ]]; then
            echo "ERROR: Unexpected DELETE status: $status" >&2
            return 1
        fi
    elif ! jq -e '.statusCode == 404 and
        (.json.error.code == "ResourceNotFound" or .json.error.code == "DeploymentNotFound")' \
        <<< "$response" > /dev/null; then
        echo "ERROR: Delete failed for $uri" >&2
        return 1
    fi
    for ((attempt=1; attempt<=60; attempt++)); do
        if response=$("$AZ" rest --method GET --uri "$uri" --output json); then
            sleep 10
        elif jq -e '.statusCode == 404 and
            (.json.error.code == "ResourceNotFound" or .json.error.code == "DeploymentNotFound")' \
            <<< "$response" > /dev/null; then
            return 0
        else
            echo "ERROR: Could not verify deletion of $uri." >&2
            return 1
        fi
    done
    echo "ERROR: Timed out waiting for deletion of $uri." >&2
    return 1
}

case "${1:-}" in
    deploy)
        command -v bicep
        bicep --version
        expect_missing "$storage_uri"
        expect_missing "$deployment_uri"
        mkdir -p "$AZCLIPS_HOME"
        touch "$marker"
        for attempt in 1 2; do
            "$AZ" deployment group create --subscription-id "$TEST_SUBSCRIPTION" \
                --resource-group "$rg" --name "$deployment" \
                --template-file "$script_dir/../Bicep/sample-storage.bicep" \
                --parameter "storageAccountName=$storage" --mode Incremental
            actual=$("$AZ" deployment group show --subscription-id "$TEST_SUBSCRIPTION" \
                --resource-group "$rg" --name "$deployment" --output json)
            jq -e --arg id "$storage_id" \
                '.properties.provisioningState == "Succeeded" and
                 (.properties.outputs.storageAccountId.value | ascii_downcase) == ($id | ascii_downcase)' \
                <<< "$actual" > /dev/null
            account=$("$AZ" rest --method GET --uri "$storage_uri" --output json)
            jq -e --arg id "$storage_id" \
                '.statusCode == 200 and .json.properties.provisioningState == "Succeeded" and
                 (.json.id | ascii_downcase) == ($id | ascii_downcase)' <<< "$account" > /dev/null
            count=0
            next="$scope/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01"
            while [[ -n "$next" ]]; do
                page=$("$AZ" rest --method GET --uri "$next" --output json)
                matches=$(jq -er --arg name "$storage" \
                    '[.json.value[] | select(.name == $name)] | length' <<< "$page")
                count=$((count + matches))
                next=$(jq -r '.json.nextLink // empty' <<< "$page")
            done
            [[ "$count" == 1 ]] || { echo "ERROR: Expected exactly one test storage account, got $count." >&2; exit 1; }
            echo "PASS: deployment attempt $attempt returned the same single storage account."
        done
        expect_missing "$scope/providers/Microsoft.Storage/storageAccounts/${storage}x?api-version=2023-01-01"
        echo "PASS: expected missing-account lookup returned nonzero with ARM ResourceNotFound."
        if [[ "${TEST_FAIL_AFTER_DEPLOYMENT:-false}" == true ]]; then
            echo "ERROR: Deliberate post-deployment failure to test always-run cleanup." >&2
            exit 1
        fi
        ;;
    cleanup)
        if [[ ! -f "$marker" ]]; then
            echo "No resource creation was attempted; nothing to delete."
            exit 0
        fi
        result=0
        # Attempt both deletions even if one fails; preserve failure for the pipeline.
        delete_and_wait "$deployment_uri" || result=1
        delete_and_wait "$storage_uri" || result=1
        if [[ "$result" == 0 ]]; then
            rm -f "$marker"
            echo "PASS: this run's deployment record and storage account are absent; resource group retained."
        fi
        exit "$result"
        ;;
    *)
        echo "Usage: $0 deploy|cleanup" >&2
        exit 2
        ;;
esac
