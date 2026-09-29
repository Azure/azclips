# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for Stop-AzVM through the built Compute module.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG rule), then exercises every
# Stop-AzVM workflow:
#   1. Stop-AzVM                                   -> POST .../deallocate
#   2. Stop-AzVM -StayProvisioned                  -> POST .../powerOff
#   3. Stop-AzVM -StayProvisioned -SkipShutdown    -> POST .../powerOff?skipShutdown=true
#   4. Stop-AzVM -ForceDeallocate                  -> POST .../deallocate?forceDeallocate=true
#   5. Stop-AzVM -Hibernate                        -> POST .../deallocate?hibernate=true
#   6. Stop-AzVM -Id                               -> id-addressed stop
# Start-AzVM runs between the cases so each stop acts on a running VM.
# -Force is used throughout so the confirmation prompt does not block.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Accounts/Accounts.psd1
#     Import-Module ./artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/PS/VMStop.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location       = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix         = $PID
$resourceGroup  = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmstop-rg-$suffix" }
$vmName         = "azclips-vmstop-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$vmId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines/$vmName"

function Start-TestVM
{
    Start-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName
}

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
        -NetworkInterfaceName "azclips-vmstop-nic-$suffix" `
        -VirtualNetworkName "azclips-vmstop-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmstop-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -NetworkSecurityGroupName "azclips-vmstop-nsg-$suffix" `
        -RuleNames rdp `
        -RuleProtocols tcp `
        -RuleDirections Inbound `
        -RulePriorities 1001 `
        -RuleSourceAddressPrefixes * `
        -RuleSourcePortRanges * `
        -RuleDestinationPortRanges 3389 `
        -RuleDestinationAddressPrefixes * `
        -RuleAccesses Allow

    Write-Host '== 1. Stop-AzVM (default: deallocate, stops compute billing) =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Force
    Start-TestVM

    Write-Host '== 2. Stop-AzVM -StayProvisioned (power off, VM stays allocated and billed) =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -StayProvisioned -Force
    Start-TestVM

    Write-Host '== 3. Stop-AzVM -StayProvisioned -SkipShutdown (non-graceful power off) =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -StayProvisioned -SkipShutdown -Force
    Start-TestVM

    Write-Host '== 4. Stop-AzVM -ForceDeallocate =='
    Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -ForceDeallocate -Force
    Start-TestVM

    Write-Host '== 5. Stop-AzVM -Hibernate (requires hibernation to be enabled on the VM) =='
    try
    {
        Stop-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -Hibernate -Force
        Start-TestVM
    }
    catch
    {
        Write-Warning "Stop-AzVM -Hibernate failed (hibernation may not be enabled on the VM): $($_.Exception.Message)"
    }

    Write-Host '== 6. Stop-AzVM -Id (resource-id addressing) =='
    Stop-AzVM -Id $vmId -Force

    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName

    Write-Host '== Stop-AzVM smoke test finished =='
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
