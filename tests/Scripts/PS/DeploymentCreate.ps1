# Resource group scope deployment - PowerShell (New-ResourceGroupDeployment)
# Equivalent of: New-AzResourceGroupDeployment
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped deployment inherits the resource group's region,
# so no location parameter is required here.

# Happy path: deploy a public quickstart template in Incremental mode.
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-deployment-1 `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Incremental `
    -Debug

# Inline local template file variant:
# New-ResourceGroupDeployment `
#     -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
#     -ResourceGroupName cirrus-test-group-1 `
#     -Name cirrus-test-deployment-2 `
#     -TemplateFile ./azuredeploy.json `
#     -ParametersFile ./azuredeploy.parameters.json `
#     -Mode Incremental

# Negative case: invalid -Mode value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message
# listing the allowed values (Incremental, Complete) and does NOT create a deployment.
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-deployment-negative `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Foo `
    -Debug
