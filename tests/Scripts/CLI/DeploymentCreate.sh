# Resource group scope deployment - CLI (azv2 deployment group create)
# Equivalent of: az deployment group create
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped deployment inherits the resource group's region,
# so no location flag is required here.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

# Happy path: deploy a public quickstart template in Incremental mode.
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-deployment-1 \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --mode Incremental \
    --debug

# Inline local template file variant:
# deployment group create \
#     --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
#     --resource-group cirrus-test-group-1 \
#     --name cirrus-test-deployment-2 \
#     --template-file ./azuredeploy.json \
#     --parameters-file ./azuredeploy.parameters.json \
#     --mode Incremental

# Bicep happy path: compile a local .bicep template via the Bicep CLI and deploy it.
# Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
# Uses a timestamped storage account name (3-24 lowercase alphanumerics) to avoid collisions.
STAMP=$(date +%y%m%d%H%M%S)
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-bicep-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --mode Incremental \
    --debug

# Bicep negative case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No deployment is created.
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-bicep-negative \
    --template-file "$BICEP_DIR/malformed.bicep" \
    --mode Incremental \
    --debug

# Negative case: invalid --mode value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message
# listing the allowed values (Incremental, Complete) and does NOT create a deployment.
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-deployment-negative \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --mode Foo \
    --debug

# Inline-parameter precedence + validation-level happy path.
# The .bicepparam supplies storageAccountName=cirrusbicep0001 (the base); the repeatable
# --parameter overrides it with a unique, timestamped value, proving inline overlays the file.
# --validation-level Provider runs full provider validation as part of the deploy.
STAMP=$(date +%y%m%d%H%M%S)
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-inline-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --parameter "storageAccountName=cirrusinline$STAMP" \
    --validation-level Provider \
    --mode Incremental \
    --debug

# Standalone --validation-level Provider case (no parameters file; inline --parameter only).
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-vlevel-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirrusvlevel$STAMP" \
    --validation-level Provider \
    --mode Incremental \
    --debug

# --rollback-on-error case: roll back to the last successful deployment if this one fails.
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-rollback-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirrusroll$STAMP" \
    --rollback-on-error \
    --mode Incremental \
    --debug

# Negative case: --parameters-uri combined with inline --parameter must be rejected.
# Expected result: ArgumentException stating inline parameters cannot be combined with a
# parameters link; no deployment is created.
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-inline-conflict-negative \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --parameters-uri https://example.com/params.json \
    --parameter storageAccountType=Standard_GRS \
    --mode Incremental \
    --debug

# LRO default (wait) case: with no --no-wait the command blocks and polls until the
# deployment reaches a terminal provisioningState. The final emitted DeploymentExtended
# has provisioningState = Succeeded (matches `az deployment group create`).
STAMP=$(date +%y%m%d%H%M%S)
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-wait-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirruswait$STAMP" \
    --mode Incremental \
    --debug

# LRO --no-wait case: returns as soon as the deployment is accepted, without polling to
# completion. The emitted DeploymentExtended typically shows provisioningState
# Accepted/Running (not necessarily Succeeded) and the command returns quickly.
STAMP=$(date +%y%m%d%H%M%S)
deployment group create \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-nowait-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirrusnowait$STAMP" \
    --mode Incremental \
    --no-wait \
    --debug
