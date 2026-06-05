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

# Bicep happy path: compile a local .bicep template via the Bicep CLI and deploy it.
# Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
# Uses a timestamped storage account name (3-24 lowercase alphanumerics) to avoid collisions.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-bicep-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -ParametersFile ../Bicep/sample-storage.bicepparam `
    -Mode Incremental `
    -Debug

# Bicep negative case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No deployment is created.
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-bicep-negative `
    -TemplateFile ../Bicep/malformed.bicep `
    -Mode Incremental `
    -Debug

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

# Inline-parameter precedence + validation-level happy path.
# The .bicepparam supplies storageAccountName=cirrusbicep0001 (the base); the repeatable
# -Parameter (KEY=VALUE) overrides it with a unique, timestamped value, proving inline
# overlays the file. -ValidationLevel Provider runs full provider validation during deploy.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-inline-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -ParametersFile ../Bicep/sample-storage.bicepparam `
    -Parameter "storageAccountName=cirrusinline$stamp" `
    -ValidationLevel Provider `
    -Mode Incremental `
    -Debug

# Standalone -ValidationLevel Provider case (no parameters file).
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-vlevel-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirrusvlevel$stamp" `
    -ValidationLevel Provider `
    -Mode Incremental `
    -Debug

# -RollbackOnError case: roll back to the last successful deployment if this one fails.
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-rollback-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirrusroll$stamp" `
    -RollbackOnError `
    -Mode Incremental `
    -Debug

# Negative case: -ParametersUri combined with inline -Parameter must be rejected.
# Expected result: ArgumentException stating inline parameters cannot be combined with a
# parameters link; no deployment is created.
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-inline-conflict-negative `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -ParametersUri https://example.com/params.json `
    -Parameter storageAccountType=Standard_GRS `
    -Mode Incremental `
    -Debug

# LRO default (wait) case: with no -NoWait the command blocks and polls until the
# deployment reaches a terminal provisioningState. The final emitted DeploymentExtended
# has provisioningState = Succeeded (matches `az deployment group create`).
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-wait-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirruswait$stamp" `
    -Mode Incremental `
    -Debug

# LRO -NoWait case: returns as soon as the deployment is accepted, without polling to
# completion. The emitted DeploymentExtended typically shows provisioningState
# Accepted/Running (not necessarily Succeeded) and the command returns quickly.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-nowait-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirrusnowait$stamp" `
    -Mode Incremental `
    -NoWait `
    -Debug
