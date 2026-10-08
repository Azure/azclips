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

# Every check is tallied rather than thrown, so all cases run; the script exits non-zero at the
# end if any failed (same pattern as DeploymentParametersQueryString.ps1).
$script:failures = 0
function Assert-Equal {
    param($Expected, $Actual, [string]$Case)
    if ($Expected -eq $Actual) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected '$Expected' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}
function Assert-Match {
    param([string]$Pattern, $Actual, [string]$Case)
    if ($Actual -match $Pattern) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected a message matching '$Pattern' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}

# A successful validate emits a DeploymentValidateResult with no error and provisioningState Succeeded.
function Assert-Validated {
    param($Result, [string]$Case)
    Assert-Equal $null $Result.Error "${Case}: no error"
    Assert-Equal 'Succeeded' $Result.Properties.ProvisioningState "${Case}: provisioningState"
}

# Happy path (TemplateUri): validate a public quickstart template in Incremental mode.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-1 `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -Mode Incremental
Assert-Validated $result 'template uri'

# Bicep happy path (TemplateFile): compile a local .bicep template via the Bicep CLI and
# validate it. Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-bicep `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Mode Incremental
Assert-Validated $result 'bicep'

# Inline-parameter precedence: the .bicepparam supplies storageAccountName=azclipsbicep0001
# (the base); the repeatable -Parameter (KEY=VALUE) overrides it, proving inline overlays the file.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-inline `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Parameter "storageAccountName=azclipsvalidateinline" `
    -Mode Incremental
Assert-Validated $result 'inline parameter'

# -ValidationLevel Template: validate the template only, without calling resource providers.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-level `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidatelevel" `
    -ValidationLevel Template `
    -Mode Incremental
Assert-Validated $result 'validation level Template'

# -Name is optional: when omitted, a GUID deployment name is generated client-side, matching
# Test-AzResourceGroupDeployment in Az PowerShell (which has no [Parameter] on DeploymentName).
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidatenoname" `
    -Mode Incremental
Assert-Validated $result 'no -Name'
Assert-Match '^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$' $result.Name 'no -Name: generated name is a GUID'

# -TemplateObject: an in-memory ARM template (hashtable) as a fourth template source,
# alongside -TemplateFile / -TemplateUri / -TemplateSpecId.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-template-object `
    -TemplateObject @{
        '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
        contentVersion = '1.0.0.0'
        resources      = @()
    } `
    -Mode Incremental
Assert-Validated $result 'template object'

# A .bicepparam alone is a complete deployment input: its 'using' clause names the template,
# so the compiled envelope supplies both the template and the parameters.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-bicepparam-only `
    -ParametersFile (Join-Path $BicepDir 'sample-storage.bicepparam') `
    -Mode Incremental
Assert-Validated $result 'bicepparam only'

# Rollback option: record that the group should roll back to its last successful deployment
# on failure. Accepted by validate and carried in the request body.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-rollback `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidaterollback" `
    -RollbackOnError `
    -Mode Incremental
Assert-Validated $result 'rollback on error'

# Long-running validate (HTTP 202): with -ValidationLevel Provider, ARM checks the template with
# the resource provider in the background. It answers the POST with 202 + Location, and the
# processor then polls that Location with GET until the result is ready. The output must be the
# final validate result with provisioningState Succeeded, not an empty value or an
# intermediate status document. (-Debug output shows the 202 and the GET polls.)
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-lro `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidatelro" `
    -ValidationLevel Provider `
    -Mode Incremental
Assert-Validated $result 'long-running validate'

# Expected-failure case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No validate call is made.
# Anything else (auth, missing RG, network) is a real failure, not a pass.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-bicep-negative `
    -TemplateFile (Join-Path $BicepDir 'malformed.bicep') `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'Bicep compilation failed' $err.Message 'malformed .bicep is rejected'

# Unknown -ValidationLevel value (Foo): PowerShell passes the string straight through to ARM,
# exactly as Az PowerShell does, so a value the client has never heard of still reaches the
# service and new server-side levels work without a client update.
# Expected: an ARM error (not a client-side ArgumentException listing allowed values).
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-vl-negative `
    -TemplateUri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json `
    -ValidationLevel Foo `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'ValidationLevel|InvalidRequestContent' $err.Message 'unknown -ValidationLevel is rejected by ARM'

# Negative case: the two rollback options are mutually exclusive and must be rejected
# locally before any request is sent.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-rollback-negative `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -RollbackOnError `
    -RollbackToDeploymentName azclips-test-validate-1 `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'mutually exclusive' $err.Message 'rollback options are mutually exclusive'

# Expected-failure case (LIVE validate API error): a template that compiles/parses fine but is
# rejected by the validate API. whatif-invalid-template.json references the undefined parameter
# 'undefinedNameParam', so ARM answers HTTP 400 carrying a DeploymentValidateResult whose error
# describes the failure. This exercises the processor's 400 handling on the wire (NOT a local
# pre-flight check): the failure MUST surface as a thrown, flattened error and MUST NOT be
# reported as a successful validation.
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-apierror `
    -TemplateFile (Join-Path $BicepDir 'whatif-invalid-template.json') `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'Deployment validation failed' $err.Message 'HTTP 400 surfaces a readable validation error'
Assert-Match 'InvalidTemplate|undefinedNameParam' $err.Message 'HTTP 400 error carries the ARM diagnostic'

# Negative case: a required template parameter with no value and no default is caught locally,
# naming the parameter, before any request is sent (az deployment does the same check).
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-missing-param `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'storageAccountName' $err.Message 'missing required parameter is named'

# Negative case: -QueryString only applies to a template URI, so supplying it without
# -TemplateUri is rejected locally (az deployment raises the same usage error).
Test-AzResourceGroupDeployment `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName azclips-test-group-1 `
    -Name azclips-test-validate-querystring-negative `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsvalidateqs" `
    -QueryString "sv=2021&sig=ABC" `
    -Mode Incremental `
    -ErrorVariable err | Out-Null
Assert-Match 'TemplateUri' $err.Message 'query string without template uri is rejected'

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
