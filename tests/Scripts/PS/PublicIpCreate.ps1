# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzPublicIpAddress.
#
# Creates a zone-redundant Standard public IP with a DNS label and tags, gets it (plain and
# expanded), lists the resource group, removes it, then gets it again to confirm removal (the
# final Get is expected to report the address no longer exists).
#
# Prereqs (run in a fresh pwsh, so an installed gallery Az.Network cannot shadow this module):
#   1. Build:     dotnet build azclips.slnx
#   2. Sign in:   Import-Module ./src/PS/Accounts/Accounts.psd1
#                 Connect-AzAccount
#   3. Module:    Import-Module ./src/PS/Network/Network.psd1
#   4. A resource group must already exist - the script creates and removes only the public IP
#      address. If you don't have one:
#                 Import-Module ./src/PS/Resources/Resources.psd1
#                 New-AzResourceGroup -SubscriptionId <id> -Name <rg> -Location eastus
#
# Usage:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<id>'
#   $env:AZCLIPS_RESOURCE_GROUP  = '<rg>'
#   ./tests/Scripts/PS/PublicIpCreate.ps1
#
# Both variables are required and deliberately have no default: a smoke test must never silently
# create resources in someone else's subscription.

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
$resourceGroup  = $env:AZCLIPS_RESOURCE_GROUP

if (-not $subscriptionId)
{
    throw 'AZCLIPS_SUBSCRIPTION_ID is required. Set $env:AZCLIPS_SUBSCRIPTION_ID to the subscription this smoke test may create resources in.'
}
if (-not $resourceGroup)
{
    throw 'AZCLIPS_RESOURCE_GROUP is required. Set $env:AZCLIPS_RESOURCE_GROUP to an existing resource group; the script creates and removes only the public IP address.'
}

$location = 'eastus'
# Both names get a per-run suffix. The finally block below removes $pipName unconditionally and
# New- issues a PUT, so a fixed name would silently overwrite a colliding address in the caller's
# resource group and then destroy it - most likely another developer's run against a shared group.
# The DNS label additionally has to be unique within the whole region.
$pipName  = "azclips-test-pip-$(Get-Random -Maximum 99999)"
$dnsName  = "$pipName-$(Get-Random -Maximum 99999)"

# Never touch an address this run did not create: the finally block removes $pipName
# unconditionally. This has to run before entering the try, otherwise that cleanup would delete
# the very address being protected.
$existing = Get-AzPublicIpAddress `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name $pipName `
    -ErrorAction SilentlyContinue

if ($existing)
{
    throw "Public IP address '$pipName' already exists in '$resourceGroup'. Refusing to overwrite and remove it; re-run to generate a different name."
}

try
{
    New-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -Location $location `
        -Sku Standard `
        -AllocationMethod Static `
        -IpAddressVersion IPv4 `
        -DomainNameLabel $dnsName `
        -IdleTimeoutInMinutes 10 `
        -Zone '1', '2', '3' `
        -Tag @{ owner = 'azclips'; scenario = 'public-ip-smoke' } `
        -Debug

    Get-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName

    Get-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -ExpandResource 'ipConfiguration'

    Get-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup
}
finally
{
    # Always clean up so a partial run leaves no public IP address behind.
    Remove-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -Force `
        -PassThru `
        -ErrorAction SilentlyContinue
}

# Expected to fail: the public IP address is gone.
Get-AzPublicIpAddress `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name $pipName
