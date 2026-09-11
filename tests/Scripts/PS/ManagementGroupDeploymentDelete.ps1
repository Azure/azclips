# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory)]
    [string] $ManagementGroupId,
    [Parameter(Mandatory)]
    [string] $Name
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
Import-Module (Join-Path $repoRoot 'src\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'src\PS\Resources\Resources.psd1') -Force

Remove-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId -Name $Name -Confirm:$false -PassThru
