# Resource group scope deployment delete - CLI (az deployment group delete)
# Equivalent of: az deployment group delete
#
# Deleting a deployment removes the HISTORY RECORD and its deployment operations. It does NOT
# delete the resources the deployment created and it does NOT touch the resource group. Two
# behaviours are worth pinning here because they surprise people:
#   1. Deleting a name that does not exist SUCCEEDS SILENTLY -- ARM answers 204 No Content whether
#      or not the deployment was there, so there is nothing to distinguish the two cases.
#   2. A deployment that is still RUNNING cannot be deleted; ARM rejects it. Cancel it first.
#
# The happy path needs a deployment that has already reached a terminal state, which is exactly what
# `az deployment group create` leaves behind (it blocks until terminal). An EMPTY template is used so
# the arrange step creates no resources and there is nothing to clean up afterwards.
#
# The "cannot delete a running deployment" case needs the opposite -- a deployment stuck in Running.
# That is arranged through `az rest`, which by design does NO LRO polling (see
# RestProcessor_Orchestrator): it returns the moment ARM accepts the PUT, leaving the deployment
# Running. Same trick DeploymentCancel.sh uses.
#
# All names are STABLE, so repeated runs overwrite the same deployment records instead of
# accumulating new ones.
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: azclips-test-group-1 (region: australiaeast)

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang. The caller's own setting is saved and restored on exit, so running this
# script does not change how az behaves afterwards.
PREVIOUS_AGENT_ENABLED=$(az config get agent.enabled --query value --output tsv 2>/dev/null)

cleanup() {
    if [ -n "$PREVIOUS_AGENT_ENABLED" ]; then
        az config set --key agent.enabled --value "$PREVIOUS_AGENT_ENABLED" >/dev/null 2>&1
    else
        az config unset agent.enabled >/dev/null 2>&1
    fi
    rm -f "$TEMPLATE_FILE"
}
trap cleanup EXIT

az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Every outcome is asserted instead, and any UNEXPECTED one is
# counted so the script can exit non-zero at the end.
FAILURES=0

fail() {
    echo "UNEXPECTED: $1" >&2
    FAILURES=$((FAILURES + 1))
}

# assert_status <actual> <expected-kind: zero|nonzero> <description>
assert_status() {
    case "$2" in
        zero)
            if [ "$1" -eq 0 ]; then echo "PASS: $3 (exit 0)"; else fail "$3 exited $1, expected 0"; fi
            ;;
        nonzero)
            if [ "$1" -ne 0 ]; then echo "PASS: $3 (exit $1)"; else fail "$3 exited 0, expected a failure"; fi
            ;;
    esac
}

SUBSCRIPTION_ID=282bb33f-067d-4179-a7a4-b833c13af735
RESOURCE_GROUP=azclips-test-group-1
DEPLOYMENT_NAME=azclips-test-deployment-delete
RUNNING_DEPLOYMENT_NAME=azclips-test-deployment-delete-running
STORAGE_ACCOUNT=azclipsdeploydelete

TEMPLATE_FILE="$(mktemp -t azclips-empty-template-XXXXXX.json)"

# Single-quoted heredoc: the ARM template's "$schema" key must NOT be expanded by the shell.
# No resources, so the deployment reaches Succeeded almost immediately and creates nothing.
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": []
}
JSON

# Arrange: create a deployment and wait for it to finish, leaving a terminal history record.
az deployment group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$TEMPLATE_FILE"
assert_status $? zero "ARRANGE: create deployment '$DEPLOYMENT_NAME'"

# Confirm the record exists before deleting it, so the assertion below is meaningful.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --output json | grep -o '"provisioningState": *"[^"]*"' | head -1

# Happy path: delete the terminal deployment. ARM answers 202 and the command polls the Location
# header until it returns 204, so this BLOCKS until the delete has actually finished. There is no
# response body, so the command prints NOTHING -- silent output plus exit code 0 is the pass
# condition, which is why no --debug is passed here: diagnostics would defeat the silence check.
DELETE_OUTPUT=$(az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME")
assert_status $? zero "delete '$DEPLOYMENT_NAME'"
if [ -n "$DELETE_OUTPUT" ]; then
    fail "delete printed '$DELETE_OUTPUT', expected no output"
else
    echo "PASS: delete produced no output"
fi

# Assert the record is gone. Because delete waits for the LRO to complete, no polling is needed
# here -- this must ALREADY fail with DeploymentNotFound and a non-zero exit.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME"
assert_status $? nonzero "show after delete (expect DeploymentNotFound)"

# subscription-id is optional: it defaults to the active profile's subscription. Deleting the same
# name a second time is the "does not exist" case -- ARM answers 204 and this SUCCEEDS SILENTLY.
# That is a deliberate divergence from legacy Azure PowerShell, which pre-checked existence and
# errored. Exit code 0 with no output is the expected result, NOT an error.
az deployment group delete \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME"
assert_status $? zero "delete an already deleted deployment (silent success)"

# Same reason: a name that has never existed also exits 0 silently.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist
assert_status $? zero "delete a deployment that never existed (silent success)"

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound) and exit non-zero. The resource group is a real path segment, so unlike
# the deployment name it is NOT silently tolerated.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --name "$DEPLOYMENT_NAME" \
    --debug
assert_status $? nonzero "delete in a missing resource group (expect ResourceGroupNotFound)"

# Negative case: --name is required; omitting it must fail argument binding before any HTTP call
# is made.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"
assert_status $? nonzero "delete without --name (expect an argument error)"

# Negative case: a deployment that is still running cannot be deleted. Arrange one via `az rest`,
# which returns as soon as ARM accepts the PUT and leaves the deployment Running.
read -r -d '' RUNNING_TEMPLATE_PAYLOAD <<'JSON'
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
          "name": "azclipsdeploydelete",
          "location": "australiaeast",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
JSON

RUNNING_URI="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP/providers/Microsoft.Resources/deployments/$RUNNING_DEPLOYMENT_NAME?api-version=2025-04-01"

PUT_STATUS=$(az rest \
    --method PUT \
    --uri "$RUNNING_URI" \
    --body "$RUNNING_TEMPLATE_PAYLOAD" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)
case "$PUT_STATUS" in
    *201*) echo "PASS: ARRANGE running deployment '$RUNNING_DEPLOYMENT_NAME' accepted ($PUT_STATUS)" ;;
    *) fail "ARRANGE running deployment PUT returned '$PUT_STATUS', expected 201 -- the running-delete case below is not meaningful" ;;
esac

# The delete must be rejected while the deployment is in flight, not silently accepted.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME" \
    --debug
assert_status $? nonzero "delete a running deployment (expect DeploymentCannotBeDeleted)"

# Cleanup. Cancel the in-flight deployment so it reaches a terminal state, poll until it gets there,
# then delete its record -- which also demonstrates that a cancelled deployment IS deletable.
az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME"

STATE=""
for _ in $(seq 1 15); do
    STATE=$(az deployment group show \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$RUNNING_DEPLOYMENT_NAME" \
        --output json | grep -o '"provisioningState": *"[^"]*"' | head -1)
    case "$STATE" in
        *Cancel*|*Succeeded*|*Failed*) break ;;
    esac
    sleep 2
done
echo "CLEANUP: running deployment provisioningState -> $STATE (expect a terminal state)"
case "$STATE" in
    *Cancel*|*Succeeded*|*Failed*) ;;
    *) fail "CLEANUP: running deployment never reached a terminal state (last state '$STATE')" ;;
esac

az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME"
assert_status $? zero "CLEANUP: delete the cancelled deployment record"

# The cancelled deployment may have created the storage account before it was stopped, and deleting
# the deployment record does not remove it. ARM answers 200 when the account existed and 204 when it
# did not, so both are a clean teardown.
DELETE_STATUS=$(az rest \
    --method DELETE \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)
case "$DELETE_STATUS" in
    *200*|*204*) echo "CLEANUP: storage account '$STORAGE_ACCOUNT' removed ($DELETE_STATUS)" ;;
    *) fail "CLEANUP: storage account delete returned '$DELETE_STATUS'; '$STORAGE_ACCOUNT' may still exist" ;;
esac

# The template file and the agent.enabled setting are restored by the EXIT trap.
if [ "$FAILURES" -eq 0 ]; then
    echo "RESULT: all assertions passed"
else
    echo "RESULT: $FAILURES unexpected outcome(s)" >&2
fi
exit $FAILURES
