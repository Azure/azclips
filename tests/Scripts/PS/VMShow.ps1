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
#
# -ShowDetails has no PowerShell spelling: the design dropped -Status and did not
# add a PowerShell detail view, so the CLI's --show-details is CLI-only.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/PS/VMShow.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmshow-rg-$suffix" }
$vmName        = "azclips-vmshow-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$vmId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines/$vmName"

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
        -VmSize Standard_D2s_v3 `
        -AdminUsername Artemis `
        -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
        -NetworkInterfaceName "azclips-vmshow-nic-$suffix" `
        -VirtualNetworkName "azclips-vmshow-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmshow-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -NetworkSecurityGroupName "azclips-vmshow-nsg-$suffix"

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

    Write-Host '== Get-AzVM smoke test finished =='
}
finally
{
    Write-Host "== cleanup: removing VM $vmName =="
    Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName

    if ($env:AZCLIPS_DELETE_GROUP -eq '1' -and (Get-Command az -ErrorAction SilentlyContinue))
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        az group delete --subscription $subscriptionId --name $resourceGroup --yes --no-wait
    }
    else
    {
        Write-Host "== cleanup: remove leftover network resources with: az group delete --name $resourceGroup --yes =="
    }
}
