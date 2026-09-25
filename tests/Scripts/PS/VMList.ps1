# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for Get-AzVM's list parameter sets through the built Compute module.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# list workflow:
#   1. Get-AzVM -ResourceGroupName        -> resource-group scoped list
#   2. Get-AzVM                           -> subscription-wide list
#   3. Get-AzVM -Name                     -> subscription-wide list, exact name filter
#   4. Get-AzVM -Location                 -> location scoped list
#   5. Get-AzVM -ResourceGroupName -Status -> per-VM fan-out for power state and OS fields
#   6. Get-AzVM -ResourceGroupName -UserData -> per-VM fan-out for user data
#
# -Name takes an exact match only; wildcards are deliberately unsupported.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/PS/VMList.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmlist-rg-$suffix" }
$vmName        = "vmlst-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

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
        -NetworkInterfaceName "azclips-vmlist-nic-$suffix" `
        -VirtualNetworkName "azclips-vmlist-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmlist-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -NetworkSecurityGroupName "azclips-vmlist-nsg-$suffix"

    $vmCreated = $true

    Write-Host '== 1. Get-AzVM -ResourceGroupName (scoped list) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup

    Write-Host '== 2. Get-AzVM (subscription-wide list) =='
    Get-AzVM -SubscriptionId $subscriptionId

    Write-Host '== 3. Get-AzVM -Name (exact name filter across the subscription) =='
    Get-AzVM -SubscriptionId $subscriptionId -Name $vmName

    Write-Host '== 4. Get-AzVM -Location (location scoped list) =='
    Get-AzVM -SubscriptionId $subscriptionId -Location $location

    Write-Host '== 5. Get-AzVM -Status (expect powerState and osName) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Status

    Write-Host '== 6. Get-AzVM -UserData (list mode) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -UserData

    Write-Host '== Get-AzVM list smoke test finished =='
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
