# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param([Parameter(Mandatory)][string]$PlanPath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
if ($plan.Service -ne 'Blob') { throw 'PowerShell performance plans support Blob transfers only.' }
if ($plan.Direction -notin @('Upload', 'Download')) { throw 'Unsupported PowerShell transfer direction.' }
$null = [Reflection.Assembly]::LoadFrom($plan.HostAssembly)
Import-Module (Join-Path $plan.Repo 'src\PS\Accounts\Accounts.psd1') -ErrorAction Stop
Import-Module (Join-Path $plan.Repo 'src\PS\Storage\Storage.psd1') -ErrorAction Stop
$context = New-AzStorageContext -StorageAccountName $plan.Account -Profile $plan.Profile -EnableFileBackupRequestIntent
$ProgressPreference = 'SilentlyContinue'

function Invoke-Transfer([string]$Direction, [string]$Name, [string]$Source, [string]$Destination) {
    if ($Direction -eq 'Upload') {
        $null = Set-AzStorageBlobContent -Context $context -Container perf -Blob $Name -File $Source `
            -ConcurrentTaskCount 4 -ChunkSize 8388608 -Force -Confirm:$false
    } else {
        $null = Get-AzStorageBlobContent -Context $context -Container perf -Blob $Name -Destination $Destination `
            -ConcurrentTaskCount 4 -ChunkSize 8388608 -Force -Confirm:$false
    }
}

Invoke-Transfer Upload $plan.WarmName $plan.WarmSource $plan.WarmDestination
Invoke-Transfer Download $plan.WarmName $plan.WarmSource $plan.WarmDestination
if ((Get-FileHash -LiteralPath $plan.WarmSource).Hash -ne (Get-FileHash -LiteralPath $plan.WarmDestination).Hash) {
    throw 'PowerShell performance warmup SHA256 mismatch.'
}
$measurement = [StoragePerformance.MemoryMeasurement]::new()
try {
    Invoke-Transfer $plan.Direction $plan.Name $plan.Source $plan.Destination
    $measurement.Complete() | ConvertTo-Json | Set-Content -LiteralPath $plan.MetricsPath -Encoding utf8
}
finally {
    $measurement.Dispose()
}
