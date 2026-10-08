# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment export - PowerShell (Save-AzResourceGroupDeploymentTemplate)
# Equivalent of: Save-AzResourceGroupDeploymentTemplate
#
# The export itself is READ-ONLY: it returns the template the deployment was created from and
# never touches any resource. The one write below reuses a STABLE deployment name, so repeated
# runs overwrite the same deployment record instead of accumulating new ones -- nothing to clean
# up in Azure. The storage account it provisions is likewise idempotent (same name every run).
# The only thing this script creates locally is a temp directory, removed at the end.
#
# Prereq: Connect-AzAccount has run. Accounts must be imported before Resources -- its module
# initializer is what calls AzclipsServiceProvider.Initialize(), and without it every cmdlet in
# this script fails with "AzclipsServiceProvider has not been initialized."
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = '282bb33f-067d-4179-a7a4-b833c13af735'
$ResourceGroupName = 'azclips-test-group-1'
$DeploymentName = 'azclips-test-deployment-export'
$StorageAccountName = 'azclipsdeploymentexport'

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

$templateFile = Join-Path $PSScriptRoot '..\Bicep\sample-storage.bicep'
$outputDir = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-export-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Arrange: ensure there is a deployment to export. Stable name -> idempotent.
$arranged = New-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -TemplateFile $templateFile `
    -Parameter "storageAccountName=$StorageAccountName" `
    -Mode Incremental
Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

# Happy path: -Path names an existing directory, so the file lands there as
# "<deployment name>.json" and the cmdlet returns that full path.
$saved = Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path $outputDir
Assert-Equal (Join-Path $outputDir "$DeploymentName.json") $saved.Path 'directory path: file named after the deployment'
Assert-Equal $true (Test-Path $saved.Path) 'directory path: file written'

# The saved file must be the template ITSELF, not the ARM { "template": ... } envelope.
$content = Get-Content $saved.Path -Raw | ConvertFrom-Json
Assert-Equal $null $content.template 'saved file is not the ARM envelope'
Assert-Match 'deploymentTemplate\.json' $content.'$schema' 'saved file is an ARM template'

# The saved file must be a usable template: feed it straight back into a validate.
$replay = Test-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name "$DeploymentName-replay" `
    -TemplateFile $saved.Path `
    -Parameter "storageAccountName=$StorageAccountName"
Assert-Equal 'Succeeded' $replay.Properties.ProvisioningState 'saved template validates'

# -Path naming a file gains a .json extension when it lacks one, and missing parent
# directories are created.
$nested = Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path (Join-Path $outputDir 'nested\prod-baseline')
Assert-Equal (Join-Path $outputDir 'nested\prod-baseline.json') $nested.Path 'file path: .json appended'
Assert-Equal $true (Test-Path $nested.Path) 'file path: parent directories created'

# Aliases: -ResourceGroup and -DeploymentName mirror the legacy parameter names.
$aliased = Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroup $ResourceGroupName `
    -DeploymentName $DeploymentName `
    -Path (Join-Path $outputDir 'via-aliases.json')
Assert-Equal $true (Test-Path (Join-Path $outputDir 'via-aliases.json')) "aliases: file written ($($aliased.Path))"

# SubscriptionId is optional: it defaults to the active profile's subscription.
$defaulted = Save-AzResourceGroupDeploymentTemplate `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path (Join-Path $outputDir 'default-subscription.json')
Assert-Equal $true (Test-Path (Join-Path $outputDir 'default-subscription.json')) "default subscription: file written ($($defaulted.Path))"

# Negative case: writing over an existing file without -Force must fail and leave the
# file untouched.
$before = Get-Content $saved.Path -Raw
Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path $outputDir `
    -ErrorVariable err | Out-Null
Assert-Match 'already exists.*-Force' $err.Message 'overwrite without -Force is rejected'
Assert-Equal $before (Get-Content $saved.Path -Raw) 'overwrite without -Force leaves the file untouched'

# ... and -Force makes the same call succeed.
$forced = Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path $outputDir `
    -Force
Assert-Equal $saved.Path $forced.Path '-Force overwrites the existing file'

# -WhatIf must report the intent without contacting ARM or writing anything.
Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Path (Join-Path $outputDir 'whatif.json') `
    -WhatIf
Assert-Equal $false (Test-Path (Join-Path $outputDir 'whatif.json')) '-WhatIf writes no file'

# Negative case: a deployment that does not exist must surface an ARM 404
# (DeploymentNotFound) as a terminating error, not an empty success.
Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name 'azclips-test-deployment-does-not-exist' `
    -Path $outputDir `
    -ErrorVariable err | Out-Null
Assert-Match 'DeploymentNotFound|not found|404' $err.Message 'missing deployment surfaces 404'

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound).
Save-AzResourceGroupDeploymentTemplate `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName 'azclips-test-group-does-not-exist' `
    -Name $DeploymentName `
    -Path $outputDir `
    -ErrorVariable err | Out-Null
Assert-Match 'ResourceGroupNotFound|not found|404' $err.Message 'missing resource group surfaces 404'

# Clean up every local artifact this script created. Nothing was created in Azure.
Remove-Item -Path $outputDir -Recurse -Force -ErrorAction SilentlyContinue

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0