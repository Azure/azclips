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
# --show-details stays CLI-only. PowerShell's detail view is -Status, which applies
# to the list parameter sets only and is covered by VMList.ps1.
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
        -VmSize Standard_D2s_v3 `
        -AdminUsername Artemis `
        -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
        -NetworkInterfaceName "azclips-vmshow-nic-$suffix" `
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
