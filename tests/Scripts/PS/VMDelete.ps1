# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# End-to-end smoke test for Remove-AzVM through the Azclips PowerShell modules.
#
# Steps:
#   1. Create the resource group (delegated to CreateResourceGroup.ps1).
#   2. Create two VMs in it.
#   3. Remove one by -Name/-ResourceGroupName with -ForceDeletion, and the other by -Id.
#   4. Get-AzVM after each removal to confirm it is gone — those calls are EXPECTED to fail.
#   5. Best-effort cleanup of both VMs on exit.
#
# The resource group itself is left behind: azclips has no Remove-AzResourceGroup cmdlet yet.
#
# Prereqs: build the solution, import src/PS/Compute/Compute.psd1 and
# src/PS/Resources/Resources.psd1, then sign in. Set $env:AZCLIPS_VM_ADMIN_PASSWORD.
# The resource group must not have a delete lock, or every delete fails with ScopeLocked.
#
# Usage: ./tests/Scripts/PS/VMDelete.ps1 <resource-group> [-Location eastus] [-Prefix vmdel]
#
# Example: ./tests/Scripts/PS/VMDelete.ps1 jttest-ae -Location australiaeast

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $ResourceGroupName,

    [string] $Location = 'eastus',

    # The VM name doubles as the Windows computer name, which Azure caps at 15 characters.
    [ValidateLength(1, 13)]
    [string] $Prefix = 'vmdel',

    [string] $SubscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
)

# Keep going after a failed call: the Get-AzVM calls after each delete are EXPECTED to
# fail, and cleanup must still run.
$ErrorActionPreference = 'Continue'

# The official Az.Compute module exports New-AzVM/Remove-AzVM cmdlets with no
# -SubscriptionId. If it is installed, PowerShell auto-loads it and every call below fails
# with a confusing "parameter cannot be found" error, so import the azclips manifest
# explicitly and verify the cmdlets actually resolve to it.
$manifest = Join-Path $PSScriptRoot '..' '..' '..' 'src' 'PS' 'Compute' 'Compute.psd1'
if (-not (Test-Path $manifest)) {
    throw "Module manifest '$manifest' not found. Build the solution first with dotnet build."
}
Import-Module $manifest -Force -ErrorAction Stop

foreach ($cmdletName in @('New-AzVM', 'Remove-AzVM', 'Get-AzVM')) {
    $resolved = Get-Command $cmdletName -ErrorAction Stop
    if ($resolved.Module.Name -ne 'Compute') {
        throw "$cmdletName resolves to module '$($resolved.Module.Name)', not the azclips 'Compute' module. Remove or hide the conflicting module and retry."
    }
}

Set-Variable -Name VmSize -Value 'Standard_D2s_v3' -Option Constant

$script:Failed = $false

# Confirms a VM is gone. Get-AzVM raises a TERMINATING error for a missing VM, which would
# otherwise unwind the try block and skip the remaining scenarios, so absence is asserted
# here via catch rather than by letting the call fail inline.
function Assert-VMRemoved {
    param([string] $Name)

    try {
        Get-AzVM -SubscriptionId $SubscriptionId -ResourceGroupName $ResourceGroupName -Name $Name -ErrorAction Stop | Out-Null
        Write-Warning "FAIL: VM '$Name' still exists after delete."
        $script:Failed = $true
    }
    catch {
        Write-Host "PASS: VM '$Name' was deleted."
    }
}

$byNameVm = "$Prefix-1"
$byIdVm   = "$Prefix-2"

Write-Host "Resource group: $ResourceGroupName | location: $Location | prefix: $Prefix"

function New-TestVM {
    param([string] $Name, [string] $Suffix)

    Write-Host "--- Create VM '$Name' ---"
    New-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $Name `
        -Location $Location `
        -VmSize $VmSize `
        -AdminUsername azadmin `
        -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
        -VirtualNetworkName "$Prefix-vnet-$Suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "$Prefix-subnet-$Suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -SecurityGroupName "$Prefix-nsg-$Suffix"
}

# Setup: the resource group must exist before the VMs can be created. Runs outside the
# try/finally so a group-create failure exits without pointless deletes.
& (Join-Path $PSScriptRoot 'CreateResourceGroup.ps1') `
    -ResourceGroupName $ResourceGroupName `
    -Location $Location `
    -SubscriptionId $SubscriptionId
if (-not $?) {
    throw "Failed to create resource group '$ResourceGroupName'."
}

try {
    New-TestVM -Name $byNameVm -Suffix 1
    New-TestVM -Name $byIdVm   -Suffix 2

    Write-Host '--- Remove by -Name/-ResourceGroupName with -ForceDeletion ---'
    Remove-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $byNameVm `
        -ForceDeletion $true `
        -Force

    # Expected to fail: the VM no longer exists.
    Assert-VMRemoved -Name $byNameVm

    Write-Host '--- Remove by -Id ---'
    $byIdResourceId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Compute/virtualMachines/$byIdVm"
    Remove-AzVM -Id $byIdResourceId -Force

    # Expected to fail: the VM no longer exists.
    Assert-VMRemoved -Name $byIdVm
}
finally {
    # Best-effort cleanup so a mid-script failure never leaves VMs behind.
    Write-Host '--- Cleanup ---'
    foreach ($name in @($byNameVm, $byIdVm)) {
        Remove-AzVM `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $name `
            -Force `
            -ErrorAction SilentlyContinue
    }
    Write-Host "Resource group '$ResourceGroupName' was left in place; delete it manually when finished."
}

if ($script:Failed) {
    throw 'VMDelete smoke test failed: at least one VM still existed after its delete.'
}

Write-Host 'VMDelete smoke test passed.'
