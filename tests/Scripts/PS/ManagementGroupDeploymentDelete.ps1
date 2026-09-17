# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Creates completed management-group deployments, deletes them by name and ID,
# verifies their records are gone, and cleans up.
param(
    [Parameter(Mandatory)]
    [string] $ManagementGroupId,
    [string] $Location = 'eastus'
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nameDeployment = "azclips-mg-delete-$stamp"
$idDeployment = "azclips-mg-delete-id-$stamp"
$template = New-TemporaryFile

try {
    Import-Module (Join-Path $repoRoot 'src\PS\Accounts\Accounts.psd1') -Force
    Import-Module (Join-Path $repoRoot 'src\PS\Resources\Resources.psd1') -Force

    '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' |
        Set-Content -Path $template -NoNewline

    foreach ($name in @($nameDeployment, $idDeployment)) {
        New-AzManagementGroupDeployment `
            -ManagementGroupId $ManagementGroupId `
            -Name $name `
            -Location $Location `
            -TemplateFile $template `
            -Confirm:$false |
            Out-Null
    }

    $removed = Remove-AzManagementGroupDeployment `
        -ManagementGroupId $ManagementGroupId `
        -Name $nameDeployment `
        -PassThru `
        -Confirm:$false
    if ($removed -ne $true) {
        throw 'Delete by name did not return $true with -PassThru.'
    }

    $id = "/providers/Microsoft.Management/managementGroups/$ManagementGroupId/providers/Microsoft.Resources/deployments/$idDeployment"
    $removed = Remove-AzManagementGroupDeployment -Id $id -PassThru -Confirm:$false
    if ($removed -ne $true) {
        throw 'Delete by ID did not return $true with -PassThru.'
    }

    $remainingNames = @(Get-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId).Name
    foreach ($name in @($nameDeployment, $idDeployment)) {
        if ($name -in $remainingNames) {
            throw "Deployment '$name' still exists."
        }
    }
}
finally {
    foreach ($name in @($nameDeployment, $idDeployment)) {
        Invoke-AzRestMethod `
            -Method DELETE `
            -Uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$ManagementGroupId/providers/Microsoft.Resources/deployments/$name`?api-version=2025-04-01" `
            -ErrorAction SilentlyContinue |
            Out-Null
    }
    Remove-Item $template -Force -ErrorAction SilentlyContinue
}
