# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzVirtualNetwork.
#
# Prereqs:
#   dotnet build azclips.slnx
#   Import-Module ./src/PS/Accounts/Accounts.psd1; Connect-AzAccount
#   Import-Module ./src/PS/Network/Network.psd1
#
# Usage: ./tests/Scripts/PS/VNetCreate.ps1

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
