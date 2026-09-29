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
#     Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Accounts/Accounts.psd1
#     Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources/Resources.psd1
#     Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: on exit, whether the run passed or failed, the VM is removed and then the
# resource group this script created is deleted with Remove-AzResourceGroup. Deleting
# the group also removes the NIC, VNet, NSG, OS disk and anything a failed run left
# half-created. A group named through $env:AZCLIPS_RESOURCE_GROUP may already hold
# other resources, so the script never deletes it; clean that group up yourself.
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
    'New-AzResourceGroup'    = 'Resources'
    'Remove-AzResourceGroup' = 'Resources'
    'New-AzVM'               = 'Compute'
    'Get-AzVM'               = 'Compute'
    'Remove-AzVM'            = 'Compute'
}

foreach ($requiredCommand in $requiredCommands.Keys)
{
    $expectedModule = $requiredCommands[$requiredCommand]
    $resolved = Get-Command $requiredCommand -ErrorAction SilentlyContinue

    if (-not $resolved)
    {
        throw "$requiredCommand is not available. Import the azclips modules first:`n" +
              "    Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Accounts/Accounts.psd1`n" +
              "    Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources/Resources.psd1`n" +
              "    Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Compute/Compute.psd1"
    }

    if ($resolved.ModuleName -ne $expectedModule)
    {
        throw "$requiredCommand resolves to module '$($resolved.ModuleName)' " +
              "(version $($resolved.Version)), not the azclips '$expectedModule' module.`n" +
              "The official Az modules shadow azclips unless you import it first:`n" +
              "    Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.$expectedModule/$expectedModule.psd1`n" +
              "If the official Az module is already loaded in this session, start a fresh " +
              "pwsh session and import the azclips modules before running this script."
    }
}

# Guard the cleanup block: each resource is only deleted once it exists, so a failure
# before then doesn't trigger a delete that 404s.
$groupCreated = $false
$vmCreated    = $false

# Only a group this script named itself is safe to delete. One supplied through
# AZCLIPS_RESOURCE_GROUP may already hold other resources.
$ownsResourceGroup = -not $env:AZCLIPS_RESOURCE_GROUP

try
{
    New-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $resourceGroup `
        -Location $location

    $groupCreated = $true

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
    # Cleanup failures are warnings, not throws: a throw here would skip the remaining
    # steps and replace the exception that ended the run.
    if ($vmCreated)
    {
        Write-Host "== cleanup: removing VM $vmName =="
        try
        {
            Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName
        }
        catch
        {
            Write-Warning "Could not remove VM ${vmName}. Error: $($_.Exception.Message)"
        }
    }
    else
    {
        Write-Host '== cleanup: skipped VM removal, the VM was never created =='
    }

    if ($groupCreated -and $ownsResourceGroup)
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        try
        {
            $null = Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Force
        }
        catch
        {
            Write-Warning ("Could not delete resource group ${resourceGroup}. Remove it with " +
                "'az group delete --name $resourceGroup --yes'. Error: $($_.Exception.Message)")
        }
    }
    elseif ($groupCreated)
    {
        Write-Host "== cleanup: kept resource group $resourceGroup because AZCLIPS_RESOURCE_GROUP supplied it; remove leftover resources yourself =="
    }
}
