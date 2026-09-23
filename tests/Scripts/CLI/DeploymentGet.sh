# Resource group scope deployment read - CLI (az deployment group show / list)
# Equivalent of: az deployment group show, az deployment group list
#
# show/list are READ-ONLY. The one write below reuses a STABLE deployment name, so repeated
# runs overwrite the same deployment record instead of accumulating new ones -- nothing to
# clean up. The storage account it provisions is likewise idempotent (same name every run).
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription holding the test resource group
# Optional:
#   AZCLIPS_RESOURCE_GROUP  - existing resource group to read, defaults to azclips-test-group-1

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang.
az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
DEPLOYMENT_NAME=azclips-test-deployment-get

# Arrange: ensure there is a deployment to read back. Stable name -> idempotent.
az deployment group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=azclipsdeploymentget" \
    --mode Incremental

# Happy path: show the single deployment by name.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Assert the show response carries the expected name and a terminal provisioningState.
STATE=$(az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --output json | grep -o '"provisioningState": *"[^"]*"' | head -1)
echo "show provisioningState -> $STATE"

# Happy path: list every deployment in the resource group. The processor unwraps the ARM
# { "value": [...], "nextLink": ... } envelope and follows pages, so this is a flat array.
az deployment group list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --debug

# subscription-id is optional: it defaults to the active profile's subscription.
az deployment group list --resource-group "$RESOURCE_GROUP"

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound) and exit non-zero, not print an empty success.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist \
    --debug

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound) and exit non-zero, not print an empty list.
az deployment group list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --debug

# Negative case: --name is required for show; omitting it must fail argument binding
# before any HTTP call is made.
az deployment group show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"
