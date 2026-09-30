# Build, import Accounts/Resources/Network, and sign in before running.
$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
$resourceGroup = $env:AZCLIPS_RESOURCE_GROUP
if (-not $subscriptionId -or -not $resourceGroup)
{
    throw 'Set AZCLIPS_SUBSCRIPTION_ID and AZCLIPS_RESOURCE_GROUP.'
}

$nsgName = 'azclips-nsg-smoke'
$location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'eastus' }

try
{
    New-AzNetworkSecurityGroup `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $nsgName `
        -Location $location `
        -Tag @{ owner = 'azclips'; scenario = 'nsg-smoke' }
}
finally
{
    Invoke-AzRestMethod `
        -Method DELETE `
        -Uri "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkSecurityGroups/$nsgName`?api-version=2025-07-01" `
        -ErrorAction SilentlyContinue
}
