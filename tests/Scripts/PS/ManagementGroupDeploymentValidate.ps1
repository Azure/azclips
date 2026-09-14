# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory)]
    [string] $ManagementGroupId,
    [string] $Location = 'eastus'
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$template = New-TemporaryFile

try {
    Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
    Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

    '{"$schema":"https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#","contentVersion":"1.0.0.0","parameters":{},"resources":[]}' |
        Set-Content -Path $template -NoNewline

    Test-AzManagementGroupDeployment `
        -ManagementGroupId $ManagementGroupId `
        -Location $Location `
        -TemplateFile $template |
        Out-Null
}
finally {
    Remove-Item $template -Force -ErrorAction SilentlyContinue
}
