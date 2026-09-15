# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory)]
    [string] $ManagementGroupId,
    [string] $Location = 'eastus'
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$name = "azclips-mg-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$template = New-TemporaryFile

try {
    Import-Module (Join-Path $repoRoot 'src\PS\Accounts\Accounts.psd1') -Force
    Import-Module (Join-Path $repoRoot 'src\PS\Resources\Resources.psd1') -Force

    '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' |
        Set-Content -Path $template -NoNewline
    New-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId -Name $name `
        -Location $Location -TemplateFile $template -Confirm:$false | Out-Null

    if ((Get-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId -Name $name).Name -ne $name) {
        throw 'Show did not return the deployment.'
    }
    if ($name -notin (Get-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId).Name) {
        throw 'List did not return the deployment.'
    }
}
finally {
    Invoke-AzRestMethod -Method DELETE `
        -Uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$ManagementGroupId/providers/Microsoft.Resources/deployments/$name`?api-version=2025-04-01" |
        Out-Null
    Remove-Item $template -Force -ErrorAction SilentlyContinue
}
