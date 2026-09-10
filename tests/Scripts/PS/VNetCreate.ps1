# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzVirtualNetwork.
#
# Creates a virtual network with two subnets, gets it (plain and expanded), lists the resource
# group, removes it, then gets it again to confirm removal (the final Get is expected to report
# the virtual network no longer exists).
#
# Prereqs (run in a fresh pwsh, so an installed gallery Az.Network cannot shadow this module):
#   1. Build:     dotnet build azclips.slnx
#   2. Sign in:   Import-Module ./src/PS/Accounts/Accounts.psd1
#                 Connect-AzAccount
#   3. Module:    Import-Module ./src/PS/Network/Network.psd1
#   4. A resource group must already exist - the script creates and removes only the virtual
#      network. If you don't have one:
#                 Import-Module ./src/PS/Resources/Resources.psd1
#                 New-AzResourceGroup -SubscriptionId <id> -Name <rg> -Location eastus
#
# Usage:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<id>'
#   $env:AZCLIPS_RESOURCE_GROUP  = '<rg>'
#   ./tests/Scripts/PS/VNetCreate.ps1
#
# Both variables fall back to the author's test values below, so set your own.

$subscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { 'f758ac53-3e63-4317-a956-0997793808d7' }
$resourceGroup  = if ($env:AZCLIPS_RESOURCE_GROUP)  { $env:AZCLIPS_RESOURCE_GROUP }  else { 'yabo-test-group-1' }
$location       = 'eastus'
$vnetName       = 'azclips-test-vnet-2'
$subnetName     = 'azclips-test-subnet-2'

try
{
    New-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vnetName `
        -Location $location `
        -AddressPrefix '10.0.0.0/16' `
        -DnsServer '10.0.0.4', '10.0.0.5' `
        -Tag @{ owner = 'azclips'; scenario = 'vnet-smoke' } `
        -FlowTimeout 10 `
        -Subnet @{ Name = $subnetName; AddressPrefix = '10.0.2.0/24'; DefaultOutboundAccess = $false },
                 @{ Name = 'data-subnet'; AddressPrefix = '10.0.3.0/24'; ServiceEndpoint = 'Microsoft.Storage' } `
        -Debug

    Get-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vnetName

    Get-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vnetName `
        -ExpandResource 'subnets/ipConfigurations'

    Get-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup
}
finally
{
    # Always clean up so a partial run leaves no virtual network behind.
    Remove-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vnetName `
        -Force `
        -PassThru `
        -ErrorAction SilentlyContinue
}

# Expected to fail: the virtual network is gone.
Get-AzVirtualNetwork `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name $vnetName
