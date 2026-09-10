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

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

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

# Arrange: start the deployment without waiting for it. ARM answers 201 Created and the deployment
# begins provisioning the storage account, which takes long enough to leave a wide cancel window.
PUT_STATUS=$(az rest \
    --method PUT \
    --uri "$DEPLOYMENT_URI" \
    --body "$TEMPLATE_PAYLOAD" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)
echo "ARRANGE: deployment '$DEPLOYMENT_NAME' PUT -> $PUT_STATUS (expect 201)"

# Happy path: cancel the in-flight deployment. ARM answers 204 No Content, so the command prints
# NOTHING on success -- silent output plus exit code 0 is the pass condition.
az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug
echo "cancel exit code -> $? (expect 0, with no output above)"

# Assert the cancel actually took effect. ARM flips provisioningState to Canceled asynchronously,
# so poll rather than sleeping a fixed amount and hoping.
STATE=""
for _ in $(seq 1 15); do
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
echo "cancel provisioningState -> $STATE (expect Canceled)"

# subscription-id is optional: it defaults to the active profile's subscription. The deployment is
# now terminal, so this doubles as the next negative case.

# Negative case: the deployment is now in a terminal state, so a second cancel must surface
# DeploymentCannotBeCancelled and exit non-zero, not report a silent success.
az deployment group cancel \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound) and exit non-zero, not print an empty success.
az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist \
    --debug

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound).
az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Negative case: --name is required; omitting it must fail argument binding before any HTTP call
# is made.
az deployment group cancel \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

# Cleanup: the cancelled deployment may have created the storage account before it was stopped.
# Cancelling does not roll anything back, so delete it explicitly. ARM answers 200 when the account
# existed and 204 when it did not, so both are a clean teardown.
DELETE_STATUS=$(az rest \
    --method DELETE \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT?api-version=2023-01-01" \
    --output json | grep -o '"statusCode": *[0-9]*' | head -1)
echo "CLEANUP: storage account '$STORAGE_ACCOUNT' delete -> $DELETE_STATUS (expect 200 or 204)"

# The deployment record itself is left behind on purpose: the name is stable, so the next run
# overwrites it rather than accumulating a new one.
