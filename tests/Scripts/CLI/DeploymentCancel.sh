# Resource group scope deployment cancel - CLI (az deployment group cancel)
# Equivalent of: az deployment group cancel
#
# A deployment can only be cancelled while its provisioningState is Accepted or Running.
# `az deployment group create` BLOCKS until the deployment reaches a terminal state, so it cannot
# arrange the happy path -- by the time it returns there is nothing left to cancel. The arrange step
# below therefore issues the deployment PUT through `az rest`, which by design does NO LRO polling
# (see RestProcessor_Orchestrator): it returns the moment ARM accepts the request, leaving the
# deployment Running and cancellable. No background job and no sleep-and-hope required.
#
# All names are STABLE, so repeated runs overwrite the same deployment record and reuse the same
# storage account instead of accumulating new ones. The cleanup step at the bottom deletes the
# storage account the cancelled deployment may have partially created.
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: azclips-test-group-1 (region: australiaeast)

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang.
az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must continue past
# them so cleanup always runs. Every expectation is therefore asserted explicitly and tallied in
# FAILURES, and the script exits non-zero at the very bottom if any of them did not hold. Without
# this the final expected-failure would become the script's exit status and a broken cancel would
# still look like a clean run.
FAILURES=0
fail() {
    echo "UNEXPECTED: $*" >&2
    FAILURES=$((FAILURES + 1))
}

SUBSCRIPTION_ID=282bb33f-067d-4179-a7a4-b833c13af735
RESOURCE_GROUP=azclips-test-group-1
DEPLOYMENT_NAME=azclips-test-deployment-cancel
STORAGE_ACCOUNT=azclipsdeploycancel

DEPLOYMENT_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$DEPLOYMENT_NAME?api-version=2025-04-01"

# Single-quoted: the ARM template's "$schema" key must NOT be expanded by the shell.
read -r -d '' TEMPLATE_PAYLOAD <<'JSON'
{
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Storage/storageAccounts",
          "apiVersion": "2023-01-01",
          "name": "azclipsdeploycancel",
          "location": "australiaeast",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
JSON

# Arrange: start the deployment without waiting for it. ARM answers 201/200 the moment it accepts
# the request and the deployment begins provisioning the storage account, leaving it Running and
# cancellable.
PUT_STATUS=$(az rest \
    --method PUT \
    --uri "$DEPLOYMENT_URI" \
    --body "$TEMPLATE_PAYLOAD" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)

# The deployment name is stable and reused across runs, so a failed arrange would leave the checks
# below asserting against STALE state from an earlier run. Skip straight to cleanup instead.
# ARM answers 201 the first time this name is created and 200 when it overwrites the existing
# record, so both mean the deployment was accepted and is now running.
ARRANGE_OK=0
case "$PUT_STATUS" in
    *200*|*201*) ARRANGE_OK=1 ;;
esac
if [[ $ARRANGE_OK -eq 0 ]]; then
    fail "arrange PUT returned '$PUT_STATUS', expected 200 or 201 - skipping the cancel checks"
else
    echo "ARRANGE: deployment '$DEPLOYMENT_NAME' PUT -> $PUT_STATUS"
fi

# Synchronize on a cancellable state instead of assuming the PUT left one. A deployment is only
# cancellable while it is Accepted or Running, and assuming that window is open is exactly what made
# this test flake with DeploymentCannotBeCancelled. The loop exits on the first cancellable reading,
# so in practice it costs a single GET.
CANCELLABLE=0
if [[ $ARRANGE_OK -eq 1 ]]; then
    PRE_CANCEL_STATE=""
    for _ in $(seq 1 15); do
        PRE_CANCEL_STATE=$(az deployment group show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$DEPLOYMENT_NAME" \
            --output json | grep -o '"provisioningState": *"[^"]*"' | head -1)
        case "$PRE_CANCEL_STATE" in
            *Accepted*|*Running*) CANCELLABLE=1; break ;;
        esac
        sleep 2
    done
    if [[ $CANCELLABLE -eq 1 ]]; then
        echo "ARRANGE: $PRE_CANCEL_STATE, deployment is cancellable"
    else
        fail "deployment settled at '$PRE_CANCEL_STATE' before it could be cancelled - skipping the cancel checks"
    fi
fi

if [[ $CANCELLABLE -eq 1 ]]; then
    # Happy path: cancel the in-flight deployment. ARM answers 204 No Content, so the command prints
    # NOTHING on stdout -- silent output plus exit code 0 is the pass condition. --debug writes to
    # stderr, so it stays visible without polluting the captured stdout.
    CANCEL_OUTPUT=$(az deployment group cancel \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --debug)
    CANCEL_EXIT=$?
    if [[ $CANCEL_EXIT -ne 0 ]]; then
        fail "cancel exited $CANCEL_EXIT, expected 0"
    elif [[ -n "${CANCEL_OUTPUT//[[:space:]]/}" ]]; then
        fail "cancel printed '$CANCEL_OUTPUT', expected no output"
    else
        echo 'PASS (cancel): exited 0 with no output'
    fi

    # Assert the cancel actually took effect. ARM acknowledges the cancel with 204 immediately but
    # only flips provisioningState to Canceled after the in-flight resource operation settles, so
    # the budget has to outlast one storage account create rather than just the POST. The loop exits
    # as soon as the state flips.
    STATE=""
    for _ in $(seq 1 30); do
        STATE=$(az deployment group show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$DEPLOYMENT_NAME" \
            --output json | grep -o '"provisioningState": *"[^"]*"' | head -1)
        case "$STATE" in
            *Cancel*) break ;;
        esac
        sleep 2
    done
    if [[ "$STATE" == *Cancel* ]]; then
        echo "PASS (state): $STATE"
    else
        fail "provisioningState is '$STATE', expected Canceled"
    fi

    # subscription-id is optional: it defaults to the active profile's subscription. The deployment is
    # now terminal, so this doubles as the next negative case.

    # Negative case: the deployment is now in a terminal state, so a second cancel must surface
    # DeploymentCannotBeCancelled and exit non-zero, not report a silent success.
    if TERMINAL_OUTPUT=$(az deployment group cancel \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --debug 2>&1); then
        fail 'cancelling a terminal deployment did not fail'
    elif [[ "$TERMINAL_OUTPUT" != *DeploymentCannotBeCancelled* ]]; then
        fail "terminal deployment failed without DeploymentCannotBeCancelled -> $TERMINAL_OUTPUT"
    else
        echo 'EXPECTED FAILURE: DeploymentCannotBeCancelled'
    fi
fi

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound) and exit non-zero, not print an empty success.
if MISSING_DEPLOYMENT_OUTPUT=$(az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist \
    --debug 2>&1); then
    fail 'missing deployment did not fail'
elif [[ "$MISSING_DEPLOYMENT_OUTPUT" != *DeploymentNotFound* ]]; then
    fail "missing deployment failed without DeploymentNotFound -> $MISSING_DEPLOYMENT_OUTPUT"
else
    echo 'EXPECTED FAILURE: DeploymentNotFound'
fi

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound).
if MISSING_GROUP_OUTPUT=$(az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --name "$DEPLOYMENT_NAME" \
    --debug 2>&1); then
    fail 'missing resource group did not fail'
elif [[ "$MISSING_GROUP_OUTPUT" != *ResourceGroupNotFound* ]]; then
    fail "missing resource group failed without ResourceGroupNotFound -> $MISSING_GROUP_OUTPUT"
else
    echo 'EXPECTED FAILURE: ResourceGroupNotFound'
fi

# Negative case: --name is required; omitting it must fail argument binding before any HTTP call
# is made.
if MISSING_NAME_OUTPUT=$(az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" 2>&1); then
    fail 'omitting --name did not fail'
else
    echo 'EXPECTED FAILURE: --name is required'
fi

# Cleanup: the cancelled deployment may have created the storage account before it was stopped.
# Cancelling does not roll anything back, so delete it explicitly. ARM answers 200 when the
# account existed and 204 when it did not, so both are a clean teardown. An account whose create was
# already in flight when the cancel landed keeps provisioning in the Storage RP and answers 409
# until it settles, so retry rather than stranding it -- but a delete that never succeeds is still
# tallied, because a leaked account must make the run non-zero.
REMOVED=0
for _ in $(seq 1 12); do
    DELETE_STATUS=$(az rest \
        --method DELETE \
        --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01" \
        --output json 2>&1 | grep -o '"statusCode": *[0-9]*' | head -1)
    if [[ "$DELETE_STATUS" == *200* || "$DELETE_STATUS" == *204* ]]; then
        REMOVED=1
        break
    fi
    sleep 5
done
if [[ $REMOVED -eq 1 ]]; then
    echo "CLEANUP: storage account '$STORAGE_ACCOUNT' delete -> $DELETE_STATUS"
else
    fail "cleanup of '$STORAGE_ACCOUNT' never succeeded, last status '$DELETE_STATUS'; it may still exist"
fi

# The deployment record itself is left behind on purpose: the name is stable, so the next run
# overwrites it rather than accumulating a new one.

if [[ $FAILURES -gt 0 ]]; then
    echo "FAILED: $FAILURES check(s) did not meet expectations" >&2
    exit 1
fi
echo 'PASS: all deployment cancel checks met expectations'
