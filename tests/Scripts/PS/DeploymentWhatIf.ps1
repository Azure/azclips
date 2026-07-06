# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment what-if - PowerShell (New-ResourceGroupDeploymentWhatIf)
# Equivalent of: Get-AzResourceGroupDeploymentWhatIfResult / New-AzResourceGroupDeployment -WhatIf
#
# What-if is READ-ONLY: it predicts the changes a template would make and never
# creates or modifies resources. The same name can be reused safely.
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)
# The resource-group-scoped what-if inherits the resource group's region,
# so no location parameter is required here.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
$BicepDir = Join-Path $PSScriptRoot '..' 'Bicep'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Happy path (TemplateUri): preview a public quickstart template in Incremental mode.
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-whatif-1 `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Incremental

# Bicep happy path (TemplateFile): compile a local .bicep template via the Bicep CLI and
# preview it. Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-whatif-bicep `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Mode Incremental

# Inline-parameter precedence: the .bicepparam supplies storageAccountName=azclipsbicep0001
# (the base); the repeatable -Parameter (KEY=VALUE) overrides it, proving inline overlays the file.
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-whatif-inline `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Parameter "storageAccountName=azclipswhatifinline" `
    -Mode Incremental

# -ResultFormat ResourceIdOnly: the predicted changes carry resource ids only
# (no full before/after payloads).
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-whatif-idonly `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipswhatifidonly" `
    -ResultFormat ResourceIdOnly `
    -Mode Incremental

# Expected-failure case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No what-if is run.
# Expected to throw; catch so the rest of the script still runs unattended.
try {
    New-ResourceGroupDeploymentWhatIf `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-whatif-bicep-negative `
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

# Negative case: invalid -ResultFormat value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message listing the
# allowed values (FullResourcePayloads, ResourceIdOnly) and does NOT run a what-if.
try {
    New-ResourceGroupDeploymentWhatIf `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-whatif-rf-negative `
        -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
        -ResultFormat Foo `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: invalid -ResultFormat Foo did not throw' -ForegroundColor Red
} catch {
    # Expected: orchestrator throws ArgumentException
    # "ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.".
    # Anything else is an unrelated failure, not the validation we are asserting.
    if ($_.Exception.Message -match 'ResultFormat must be one of') {
        Write-Host "EXPECTED FAILURE (invalid -ResultFormat): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Expected-failure case (LIVE what-if API error): a template that compiles/parses fine but
# is rejected by the what-if API. whatif-invalid-template.json references the undefined
# parameter 'undefinedNameParam', so ARM completes the what-if long-running operation with a
# terminal 'Failed' state and an InvalidTemplate error in result.error. This exercises the
# orchestrator's error handling on the wire (NOT a local pre-flight check): the failure MUST
# surface as a thrown error and MUST NOT be silently reported as a successful no-change result.
$TemplateDir = Join-Path $PSScriptRoot '..' 'Bicep'
try {
    New-ResourceGroupDeploymentWhatIf `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-whatif-apierror `
        -TemplateFile (Join-Path $TemplateDir 'whatif-invalid-template.json') `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: invalid-template what-if did not throw (reported no error)' -ForegroundColor Red
} catch {
    # Expected: the LRO terminates Failed / result.error carries the InvalidTemplate diagnostic,
    # and the orchestrator throws a flattened error mentioning the missing parameter.
    if ($_.Exception.Message -match 'InvalidTemplate|undefinedNameParam|Long running operation failed|What-if operation failed') {
        Write-Host "EXPECTED FAILURE (what-if API error): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

