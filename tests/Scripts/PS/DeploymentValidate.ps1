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

# -Name is optional: when omitted, a GUID deployment name is generated client-side, matching
# Test-AzResourceGroupDeployment in Az PowerShell (which has no [Parameter] on DeploymentName).
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidatenoname" `
    -Mode Incremental

# -TemplateObject: an in-memory ARM template (hashtable) as a fourth template source,
# alongside -TemplateFile / -TemplateUri / -TemplateSpecId.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-template-object `
    -TemplateObject @{
        '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
        contentVersion = '1.0.0.0'
        resources      = @()
    } `
    -Mode Incremental

# A .bicepparam alone is a complete deployment input: its 'using' clause names the template,
# so the compiled envelope supplies both the template and the parameters.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-bicepparam-only `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
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

# Unknown -ValidationLevel value (Foo): PowerShell passes the string straight through to ARM,
# exactly as Az PowerShell does, so a value the client has never heard of still reaches the
# service and new server-side levels work without a client update.
# Expected result: the validate API is called and ARM rejects the value.
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-vl-negative `
        -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
        -ValidationLevel Foo `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: ARM accepted the unknown -ValidationLevel Foo' -ForegroundColor Red
} catch {
    # Expected: an ARM error (not a client-side ArgumentException listing allowed values).
    if ($_.Exception.Message -match 'ValidationLevel|InvalidRequestContent|validationLevel') {
        Write-Host "EXPECTED FAILURE (ARM rejected the unknown level): $($_.Exception.Message)"
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

# Negative case: a required template parameter with no value and no default is caught locally,
# naming the parameter, before any request is sent (az deployment does the same check).
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-missing-param `
        -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: missing required parameter did not throw' -ForegroundColor Red
} catch {
    # Expected: the error names 'storageAccountName', the only parameter without a default.
    if ($_.Exception.Message -match 'storageAccountName') {
        Write-Host "EXPECTED FAILURE (missing template parameter): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Negative case: -QueryString only applies to a template URI, so supplying it without
# -TemplateUri is rejected locally (az deployment raises the same usage error).
try {
    Test-AzResourceGroupDeployment `
        -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
        -ResourceGroupName azclips-test-group-1 `
        -Name azclips-test-validate-querystring-negative `
        -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
        -Parameter "storageAccountName=azclipsvalidateqs" `
        -QueryString "sv=2021&sig=ABC" `
        -Mode Incremental `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: -QueryString without -TemplateUri did not throw' -ForegroundColor Red
} catch {
    if ($_.Exception.Message -match 'TemplateUri') {
        Write-Host "EXPECTED FAILURE (query string without template uri): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}
