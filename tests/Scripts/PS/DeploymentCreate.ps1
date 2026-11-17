# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment - PowerShell (New-AzResourceGroupDeployment)
# Equivalent of: New-AzResourceGroupDeployment
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)
# The resource-group-scoped deployment inherits the resource group's region,
# so no location parameter is required here.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
$BicepDir = Join-Path $PSScriptRoot '..' 'Bicep'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Happy path: deploy a public quickstart template in Incremental mode.
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-deployment-1 `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Incremental

# Inline local template file variant:
# New-AzResourceGroupDeployment `
#     -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
#     -ResourceGroupName azclips-test-group-1 `
#     -Name azclips-test-deployment-2 `
#     -TemplateFile ./azuredeploy.json `
#     -ParametersFile ./azuredeploy.parameters.json `
#     -Mode Incremental

# Bicep happy path: compile a local .bicep template via the Bicep CLI and deploy it.
# Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
# Uses a timestamped storage account name (3-24 lowercase alphanumerics) to avoid collisions.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name "azclips-test-bicep-$stamp" `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Mode Incremental

# Bicep negative case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No deployment is created.
# Expected to throw; catch so the rest of the script still runs unattended.
try {
    New-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-bicep-negative `
        -TemplateFile (Join-Path $BicepDir 'malformed.bicep') `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: malformed.bicep negative case did not throw' -ForegroundColor Red
} catch {
    # Expected: BicepCompiler throws "Bicep compilation failed (exit code N): <diagnostics>".
    # Anything else (auth, missing RG, network) is a real failure, not a pass.
    if ($_.Exception.Message -match 'Bicep compilation failed') {
        Write-Host "EXPECTED FAILURE (malformed bicep): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Negative case: invalid -Mode value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message
# listing the allowed values (Incremental, Complete) and does NOT create a deployment.
try {
    New-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-deployment-negative `
        -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
        -Mode Foo `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: invalid -Mode Foo did not throw' -ForegroundColor Red
} catch {
    # Expected: processor throws "Mode must be either 'Incremental' or 'Complete'.".
    # Anything else is an unrelated failure, not the validation we are asserting.
    if ($_.Exception.Message -match "Mode must be either") {
        Write-Host "EXPECTED FAILURE (invalid -Mode): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Inline-parameter precedence + validation-level happy path.
# The .bicepparam supplies storageAccountName=azclipsbicep0001 (the base); the repeatable
# -Parameter (KEY=VALUE) overrides it with a unique, timestamped value, proving inline
# overlays the file. -ValidationLevel Provider runs full provider validation during deploy.
# Storage account names max out at 24 characters and the stamp takes 12, so prefixes stay <= 12.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name "azclips-test-inline-$stamp" `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Parameter "storageAccountName=azclipsinl$stamp" `
    -ValidationLevel Provider `
    -Mode Incremental

# Standalone -ValidationLevel Provider case (no parameters file).
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name "azclips-test-vlevel-$stamp" `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipslvl$stamp" `
    -ValidationLevel Provider `
    -Mode Incremental

# -RollbackOnError case: roll back to the last successful deployment if this one fails.
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name "azclips-test-rollback-$stamp" `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsroll$stamp" `
    -RollbackOnError `
    -Mode Incremental

# Negative case: -ParametersUri combined with inline -Parameter must be rejected.
# Expected result: ArgumentException stating inline parameters cannot be combined with a
# parameters link; no deployment is created.
try {
    New-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-inline-conflict-negative `
        -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
        -ParametersUri https://example.com/params.json `
        -Parameter storageAccountType=Standard_GRS `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: -ParametersUri + inline -Parameter conflict did not throw' -ForegroundColor Red
} catch {
    # Expected: processor throws ArgumentException
    # "Inline parameters cannot be combined with a parameters URI; ...".
    # Anything else is an unrelated failure, not the conflict we are asserting.
    if ($_.Exception.Message -match 'Inline parameters cannot be combined with a parameters URI') {
        Write-Host "EXPECTED FAILURE (inline + parameters-link conflict): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# LRO default (wait) case: the command blocks and polls until the deployment reaches a
# terminal provisioningState. The final emitted DeploymentExtended has provisioningState =
# Succeeded (matches `az deployment group create`).
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name "azclips-test-wait-$stamp" `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipswait$stamp" `
    -Mode Incremental

