# Resource group scope deployment export - CLI (az deployment group export)
# Equivalent of: az deployment group export
#
# export is READ-ONLY: it returns the template the deployment was created from and never
# touches any resource. The one write below reuses a STABLE deployment name, so repeated runs
# overwrite the same deployment record instead of accumulating new ones -- nothing to clean up
# in Azure. The storage account it provisions is likewise idempotent (same name every run).
# The only thing this script creates locally is a temp directory, removed at the end.
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

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

SUBSCRIPTION_ID=282bb33f-067d-4179-a7a4-b833c13af735
RESOURCE_GROUP=azclips-test-group-1
DEPLOYMENT_NAME=azclips-test-deployment-export

OUTPUT_DIR=$(mktemp -d)
# Remove the temp directory however the script exits, including the negative cases.
trap 'rm -rf "$OUTPUT_DIR"' EXIT

# Arrange: ensure there is a deployment to export. Stable name -> idempotent.
az deployment group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=azclipsdeploymentexport" \
    --mode Incremental

# Happy path: export the deployment's template.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Assert the payload is the template ITSELF, not the ARM { "template": ... } envelope:
# the top level must carry $schema/contentVersion and must NOT carry a "template" key.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --output json > "$OUTPUT_DIR/exported.json"

SCHEMA=$(grep -o '"\$schema": *"[^"]*"' "$OUTPUT_DIR/exported.json" | head -1)
echo "export \$schema -> $SCHEMA"
if grep -q '"template"' "$OUTPUT_DIR/exported.json"; then
    echo "UNEXPECTED: the ARM envelope was not unwrapped"
else
    echo "envelope unwrapped as expected"
fi

# The redirected file must be a usable template: feed it straight back into a validate.
az deployment group validate \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME-replay" \
    --template-file "$OUTPUT_DIR/exported.json" \
    --parameter "storageAccountName=azclipsdeploymentexport"

# subscription-id is optional: it defaults to the active profile's subscription.
az deployment group export --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME"

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound) and exit non-zero, not print an empty success.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name azclips-test-deployment-does-not-exist \
    --debug

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound) and exit non-zero.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group azclips-test-group-does-not-exist \
    --name "$DEPLOYMENT_NAME" \
    --debug

# Negative case: --name is required; omitting it must fail argument binding before any
# HTTP call is made.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

# Negative case: --resource-group is required; omitting it must fail argument binding too.
az deployment group export \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$DEPLOYMENT_NAME"
