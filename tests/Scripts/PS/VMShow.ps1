# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for Get-AzVM through the built Compute module.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# Get-AzVM workflow:
#   1. Get-AzVM -ResourceGroupName -Name   -> plain GET
#   2. Get-AzVM -UserData                  -> GET ...?$expand=userData
#   3. Get-AzVM -Id                        -> id-addressed get
#   4. Get-AzVM -Id -UserData              -> id-addressed get with expand
#   5. Get-AzVM -ResourceGroupName         -> list in the resource group
#   6. Get-AzVM -ResourceGroupName -Name -Status
#                                          -> GET ...?$expand=instanceView; the VM keeps
#                                             its instance view in Properties.InstanceView;
#                                             VM running
#   7. Get-AzVM -Status -UserData          -> one GET ...?$expand=instanceView,userData
#   8. Get-AzVM -Status:$false             -> plain GET, as in case 1
#   9. Stop-AzVM -StayProvisioned, then
#      Get-AzVM -Status                    -> VM stopped
#  10. Stop-AzVM, then Get-AzVM -Id -Status -> VM deallocated
#  11. Start-AzVM, then Get-AzVM -Status   -> VM running
#
# Cases 6, 7 and 9-11 check the power state in Properties.InstanceView.Statuses, and
# that it was not flattened onto the VM; case 8 checks there is no instance view. The
# script fails if any check fails. Stop-AzVM runs with -Force so its confirmation
# prompt does not block.
#
# --show-details stays CLI-only. -Status on the list parameter sets flattens the power
# state onto each VM instead, and is covered by VMList.ps1.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
#
# Resources is required because this script calls New-AzResourceGroup; importing
# only Accounts and Compute makes that call fail, and with $ErrorActionPreference
# = 'Stop' the failure jumps straight to cleanup.
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Set $env:AZCLIPS_LOCATION and $env:AZCLIPS_VM_SIZE to override the region (westus2)
# and the VM size (Standard_D2s_v3).
#
# Usage: ./tests/Scripts/PS/VMShow.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'australiaeast' } else { $env:AZCLIPS_LOCATION }
$vmSize        = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_VM_SIZE)) { 'Standard_D2s_v3' } else { $env:AZCLIPS_VM_SIZE }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmshow-rg-$suffix" }
$vmName        = "vmshw-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$vmId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines/$vmName"

# Fail fast with an actionable message. Checking existence is not enough: the official
# Az modules (Az.Resources, Az.Compute) export the same cmdlet names and PowerShell
# auto-loads them on first use, so an un-imported azclips module silently resolves to
# the official one — which rejects -SubscriptionId and fails mid-script.
$requiredCommands = [ordered]@{
    'New-AzResourceGroup' = 'Resources'
    'New-AzVM'            = 'Compute'
    'Get-AzVM'            = 'Compute'
    'Stop-AzVM'           = 'Compute'
    'Start-AzVM'          = 'Compute'
    'Remove-AzVM'         = 'Compute'
}

foreach ($requiredCommand in $requiredCommands.Keys)
{
    $expectedModule = $requiredCommands[$requiredCommand]
    $resolved = Get-Command $requiredCommand -ErrorAction SilentlyContinue

    if (-not $resolved)
    {
        throw "$requiredCommand is not available. Import the azclips modules first:`n" +
              "    Import-Module ./src/PS/Accounts/Accounts.psd1`n" +
              "    Import-Module ./src/PS/Resources/Resources.psd1`n" +
              "    Import-Module ./src/PS/Compute/Compute.psd1"
    }

    if ($resolved.ModuleName -ne $expectedModule)
    {
        throw "$requiredCommand resolves to module '$($resolved.ModuleName)' " +
              "(version $($resolved.Version)), not the azclips '$expectedModule' module.`n" +
              "The official Az modules shadow azclips unless you import it first:`n" +
              "    Import-Module ./src/PS/$expectedModule/$expectedModule.psd1`n" +
              "If the official Az module is already loaded in this session, start a fresh " +
              "pwsh session and import the azclips modules before running this script."
    }
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message)
{
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
}

# Get-AzVM -Status for one VM returns the VM with its instance view in place, in
# Properties.InstanceView. Checks the power state there, and that it was not flattened
# onto the VM the way the list forms flatten it.
function Test-InstanceView([string]$Label, $Output, [string]$ExpectedPowerState)
{
    $vms = @($Output | Where-Object { $null -ne $_ })
    if ($vms.Count -ne 1)
    {
        Add-Failure "${Label}: expected one VM, got $($vms.Count)"
        return
    }

    $powerState = ($vms[0].Properties.InstanceView.Statuses | Where-Object Code -like 'PowerState/*').DisplayStatus
    Write-Host "${Label}: $powerState"

    if ($powerState -ne $ExpectedPowerState)
    {
        Add-Failure "${Label}: expected power state '$ExpectedPowerState', got '$powerState'"
    }

    if ($null -ne $vms[0].PowerState)
    {
        Add-Failure "${Label}: expected the instance view in place, but the power state was flattened onto the VM"
    }
}

# Guards the cleanup block: without it, a failure before the VM exists triggers a
# Remove-AzVM that 404s and masks the real error.
$vmCreated = $false

try
{
    New-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $resourceGroup `
        -Location $location

    New-AzVM `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vmName `
        -Location $location `
        -VmSize $vmSize `
        -AdminUsername Artemis `
        -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
        -VirtualNetworkName "azclips-vmshow-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmshow-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -SecurityGroupName "azclips-vmshow-nsg-$suffix"

    $vmCreated = $true

    Write-Host '== 1. Get-AzVM -ResourceGroupName -Name (plain) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName

    Write-Host '== 2. Get-AzVM -UserData =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -UserData

    Write-Host '== 3. Get-AzVM -Id (resource-id addressing) =='
    Get-AzVM -Id $vmId

    Write-Host '== 4. Get-AzVM -Id -UserData =='
    Get-AzVM -Id $vmId -UserData

    Write-Host '== 5. Get-AzVM -ResourceGroupName (list) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup

    Write-Host '== 6. Get-AzVM -ResourceGroupName -Name -Status (instance view in Properties.InstanceView; expect VM running) =='
    $vm = Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Status
    $vm.Properties.InstanceView
    Test-InstanceView '6. -Status' $vm 'VM running'

    Write-Host '== 7. Get-AzVM -Status -UserData (one GET for both expands; expect VM running) =='
    Test-InstanceView '7. -Status -UserData' (Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Status -UserData) 'VM running'

    Write-Host '== 8. Get-AzVM -Status:$false (plain GET, no instance view) =='
    $vm = Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Status:$false
    if ($null -eq $vm.Properties.InstanceView)
    {
        Write-Host '8. -Status:$false: plain VM'
    }
    else
    {
        Add-Failure '8. -Status:$false: expected the plain VM, but it has an instance view'
    }

    Write-Host '== 9. Stop-AzVM -StayProvisioned, then Get-AzVM -Status (expect VM stopped) =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -StayProvisioned -Force
    Test-InstanceView '9. after Stop-AzVM -StayProvisioned' (Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Status) 'VM stopped'

    Write-Host '== 10. Stop-AzVM, then Get-AzVM -Id -Status (expect VM deallocated) =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Force
    Test-InstanceView '10. after Stop-AzVM' (Get-AzVM -Id $vmId -Status) 'VM deallocated'

    Write-Host '== 11. Start-AzVM, then Get-AzVM -Status (expect VM running) =='
    Start-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName
    Test-InstanceView '11. after Start-AzVM' (Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Status) 'VM running'

    if ($failures.Count -gt 0)
    {
        throw "Get-AzVM smoke test finished with $($failures.Count) failure(s)."
    }
    Write-Host '== Get-AzVM smoke test finished =='
}
finally
{
    if ($vmCreated)
    {
        Write-Host "== cleanup: removing VM $vmName =="
        Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName
    }
    else
    {
        Write-Host '== cleanup: skipped, the VM was never created =='
    }

    if ($vmCreated -and $env:AZCLIPS_DELETE_GROUP -eq '1' -and (Get-Command az -ErrorAction SilentlyContinue))
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        az group delete --subscription $subscriptionId --name $resourceGroup --yes --no-wait
    }
    elseif ($vmCreated)
    {
        Write-Host "== cleanup: remove leftover network resources with: az group delete --name $resourceGroup --yes =="
    }
}
