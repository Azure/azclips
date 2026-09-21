# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment export - PowerShell (Save-AzResourceGroupDeploymentTemplate)
# Equivalent of: Save-AzResourceGroupDeploymentTemplate
#
# The export itself is READ-ONLY: it returns the template the deployment was created from and
# never touches any resource. The one write below reuses a STABLE deployment name, so repeated
# runs overwrite the same deployment record instead of accumulating new ones -- nothing to clean
# up in Azure. The storage account it provisions is likewise idempotent (same name every run).
# The only thing this script creates locally is a temp directory, removed in the finally block.
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

$templateFile = Join-Path $PSScriptRoot '..\Bicep\sample-storage.bicep'
$outputDir = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-export-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

try {
    # Arrange: ensure there is a deployment to export. Stable name -> idempotent.
    New-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -TemplateFile $templateFile `
        -Parameter "storageAccountName=$StorageAccountName" `
        -Mode Incremental

    # Happy path: -Path names an existing directory, so the file lands there as
    # "<deployment name>.json" and the cmdlet returns that full path.
    $saved = Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Path $outputDir

    Write-Host "saved -> $($saved.Path)"
    $expected = Join-Path $outputDir "$DeploymentName.json"
    if ($saved.Path -ne $expected) { Write-Host "UNEXPECTED path: expected $expected" }
    if (-not (Test-Path $saved.Path)) { Write-Host "UNEXPECTED: file was not written" }

    # The saved file must be the template ITSELF, not the ARM { "template": ... } envelope.
    $content = Get-Content $saved.Path -Raw | ConvertFrom-Json
    if ($null -ne $content.template) { Write-Host "UNEXPECTED: the ARM envelope was not unwrapped" }
    Write-Host "saved `$schema -> $($content.'$schema')"

    # The saved file must be a usable template: feed it straight back into a validate.
    Test-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name "$DeploymentName-replay" `
        -TemplateFile $saved.Path `
        -Parameter "storageAccountName=$StorageAccountName"

    # -Path naming a file gains a .json extension when it lacks one, and missing parent
    # directories are created.
    $nested = Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Path (Join-Path $outputDir 'nested\prod-baseline')

    Write-Host "nested -> $($nested.Path)"
    if (-not $nested.Path.EndsWith('prod-baseline.json')) { Write-Host "UNEXPECTED: .json was not appended" }

    # Aliases: -ResourceGroup and -DeploymentName mirror the legacy parameter names.
    Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $SubscriptionId `
        -ResourceGroup $ResourceGroupName `
        -DeploymentName $DeploymentName `
        -Path (Join-Path $outputDir 'via-aliases.json')

    # SubscriptionId is optional: it defaults to the active profile's subscription.
    Save-AzResourceGroupDeploymentTemplate `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Path (Join-Path $outputDir 'default-subscription.json')

    # Negative case: writing over an existing file without -Force must fail and leave the
    # file untouched.
    try {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $DeploymentName `
            -Path $outputDir `
            -ErrorAction Stop
        Write-Host "UNEXPECTED: overwriting without -Force succeeded"
    }
    catch {
        Write-Host "expected overwrite failure -> $($_.Exception.Message)"
    }

    # ... and -Force makes the same call succeed.
    Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Path $outputDir `
        -Force

    # -WhatIf must report the intent without contacting ARM or writing anything.
    Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Path (Join-Path $outputDir 'whatif.json') `
        -WhatIf

    if (Test-Path (Join-Path $outputDir 'whatif.json')) { Write-Host "UNEXPECTED: -WhatIf wrote a file" }

    # Negative case: a deployment that does not exist must surface an ARM 404
    # (DeploymentNotFound) as a terminating error, not an empty success.
    try {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name 'azclips-test-deployment-does-not-exist' `
            -Path $outputDir `
            -ErrorAction Stop
        Write-Host "UNEXPECTED: exporting a missing deployment succeeded"
    }
    catch {
        Write-Host "expected DeploymentNotFound -> $($_.Exception.Message)"
    }

    # Negative case: a resource group that does not exist must surface an ARM 404
    # (ResourceGroupNotFound).
    try {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName 'azclips-test-group-does-not-exist' `
            -Name $DeploymentName `
            -Path $outputDir `
            -ErrorAction Stop
        Write-Host "UNEXPECTED: exporting from a missing resource group succeeded"
    }
    catch {
        Write-Host "expected ResourceGroupNotFound -> $($_.Exception.Message)"
    }
}
finally {
    # Clean up every local artifact this script created. Nothing was created in Azure.
    Remove-Item -Path $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}
