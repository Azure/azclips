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
# don't hang.
az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

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
echo "ARRANGE: create exit code -> $? (expect 0)"

# Confirm the record exists before deleting it, so the assertion below is meaningful.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --output json | grep -o '"provisioningState": *"[^"]*"' | head -1

# Happy path: delete the terminal deployment. ARM answers 202 and the command polls the Location
# header until it returns 204, so this BLOCKS until the delete has actually finished. There is no
# response body, so the command prints NOTHING -- silent output plus exit code 0 is the pass
# condition.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug
echo "delete exit code -> $? (expect 0, with no output above)"

# Assert the record is gone. Because delete waits for the LRO to complete, no polling is needed
# here -- this must ALREADY fail with DeploymentNotFound and a non-zero exit.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME"
echo "show-after-delete exit code -> $? (expect non-zero, DeploymentNotFound)"

# subscription-id is optional: it defaults to the active profile's subscription. Deleting the same
# name a second time is the "does not exist" case -- ARM answers 204 and this SUCCEEDS SILENTLY.
# That is a deliberate divergence from legacy Azure PowerShell, which pre-checked existence and
# errored. Exit code 0 with no output is the expected result, NOT an error.
az deployment group delete \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug
echo "delete-already-deleted exit code -> $? (expect 0 -- silent success, see comment above)"

# Same reason: a name that has never existed also exits 0 silently.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist
echo "delete-never-existed exit code -> $? (expect 0 -- silent success)"

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound) and exit non-zero. The resource group is a real path segment, so unlike
# the deployment name it is NOT silently tolerated.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Negative case: --name is required; omitting it must fail argument binding before any HTTP call
# is made.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

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
echo "ARRANGE: running deployment '$RUNNING_DEPLOYMENT_NAME' PUT -> $PUT_STATUS (expect 201)"

# The delete must be rejected while the deployment is in flight, not silently accepted.
az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME" \
    --debug
echo "delete-while-running exit code -> $? (expect non-zero)"

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

az deployment group delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$RUNNING_DEPLOYMENT_NAME"
echo "CLEANUP: delete of cancelled deployment exit code -> $? (expect 0)"

# The cancelled deployment may have created the storage account before it was stopped, and deleting
# the deployment record does not remove it. ARM answers 200 when the account existed and 204 when it
# did not, so both are a clean teardown.
DELETE_STATUS=$(az rest \
    --method DELETE \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)
echo "CLEANUP: storage account '$STORAGE_ACCOUNT' delete -> $DELETE_STATUS (expect 200 or 204)"

rm -f "$TEMPLATE_FILE"
