# Resource group scope deployment - CLI (azv2 deployment group create)
# Equivalent of: az deployment group create
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped deployment inherits the resource group's region,
# so no location flag is required here.

# Happy path: deploy a public quickstart template in Incremental mode.
deployment group create `
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 `
    --resource-group cirrus-test-group-1 `
    --name cirrus-test-deployment-1 `
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    --mode Incremental `
    --debug

# Inline local template file variant:
# deployment group create `
#     --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 `
#     --resource-group cirrus-test-group-1 `
#     --name cirrus-test-deployment-2 `
#     --template-file ./azuredeploy.json `
#     --parameters-file ./azuredeploy.parameters.json `
#     --mode Incremental

# Negative case: invalid --mode value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message
# listing the allowed values (Incremental, Complete) and does NOT create a deployment.
deployment group create `
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 `
    --resource-group cirrus-test-group-1 `
    --name cirrus-test-deployment-negative `
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    --mode Foo `
    --debug
