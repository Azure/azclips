# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment validate - PowerShell (Test-AzResourceGroupDeployment)
# Equivalent of: Test-AzResourceGroupDeployment
#
# Validate is READ-ONLY: it asks ARM whether a template would be accepted and never
# creates or modifies resources. The same name can be reused safely.
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)
# The resource-group-scoped validate inherits the resource group's region,
# so no location parameter is required here.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
$BicepDir = Join-Path $PSScriptRoot '..' 'Bicep'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Happy path (TemplateUri): validate a public quickstart template in Incremental mode.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-1 `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Incremental

# Bicep happy path (TemplateFile): compile a local .bicep template via the Bicep CLI and
# validate it. Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-bicep `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Mode Incremental

# Inline-parameter precedence: the .bicepparam supplies storageAccountName=azclipsbicep0001
# (the base); the repeatable -Parameter (KEY=VALUE) overrides it, proving inline overlays the file.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-inline `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Parameter "storageAccountName=azclipsvalidateinline" `
    -Mode Incremental

# -ValidationLevel Template: validate the template only, without calling resource providers.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-level `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidatelevel" `
    -ValidationLevel Template `
    -Mode Incremental

# Rollback option: record that the group should roll back to its last successful deployment
# on failure. Accepted by validate and carried in the request body.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-rollback `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidaterollback" `
    -RollbackOnError `
    -Mode Incremental

# Expected-failure case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No validate call is made.
# Expected to throw; catch so the rest of the script still runs unattended.
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-bicep-negative `
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

# Negative case: invalid -ValidationLevel value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message listing the
# allowed values (Template, Provider, ProviderNoRbac) and does NOT call the validate API.
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-vl-negative `
        -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
        -ValidationLevel Foo `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: invalid -ValidationLevel Foo did not throw' -ForegroundColor Red
} catch {
    # Expected: the orchestrator throws an ArgumentException naming the allowed values.
    # Anything else is an unrelated failure, not the validation we are asserting.
    if ($_.Exception.Message -match 'ValidationLevel') {
        Write-Host "EXPECTED FAILURE (invalid -ValidationLevel): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Negative case: the two rollback options are mutually exclusive and must be rejected
# locally before any request is sent.
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-rollback-negative `
        -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
        -RollbackOnError `
        -RollbackToDeploymentName azclips-test-validate-1 `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: mutually exclusive rollback options did not throw' -ForegroundColor Red
} catch {
    # Expected: "Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive."
    if ($_.Exception.Message -match 'mutually exclusive') {
        Write-Host "EXPECTED FAILURE (rollback conflict): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Expected-failure case (LIVE validate API error): a template that compiles/parses fine but is
# rejected by the validate API. whatif-invalid-template.json references the undefined parameter
# 'undefinedNameParam', so ARM answers HTTP 400 carrying a DeploymentValidateResult whose error
# describes the failure. This exercises the processor's 400 handling on the wire (NOT a local
# pre-flight check): the failure MUST surface as a thrown, flattened error and MUST NOT be
# reported as a successful validation.
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-apierror `
        -TemplateFile (Join-Path $BicepDir 'whatif-invalid-template.json') `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: invalid-template validate did not throw (reported no error)' -ForegroundColor Red
} catch {
    # Expected: the 400 body carries the InvalidTemplate diagnostic and the orchestrator throws
    # a flattened error mentioning the missing parameter.
    if ($_.Exception.Message -match 'InvalidTemplate|undefinedNameParam|Deployment validation failed') {
        Write-Host "EXPECTED FAILURE (validate API error): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}
