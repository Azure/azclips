# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzVirtualNetworkSubnetConfig.
#
# New-AzVirtualNetworkSubnetConfig is offline (legacy Az semantics): it builds the subnet object
# that New-AzVirtualNetwork -Subnet sends. The script creates a network security group, builds a
# subnet config that references it with a service endpoint, a delegation in the dotted legacy form
# and default outbound access off, creates a virtual network with that subnet, gets the subnet
# (plain and with the NSG expanded), lists the virtual network's subnets, removes the subnet, and
# verifies the removal (a Get must now fail with 404). The prerequisites are removed on exit.
#
# Prereqs (run in a fresh pwsh, so an installed gallery Az.Network cannot shadow this module):
#   1. Build:     dotnet build azclips.slnx
#   2. Sign in:   Import-Module ./src/PS/Accounts/Accounts.psd1
#                 Connect-AzAccount
#   3. Module:    Import-Module ./src/PS/Network/Network.psd1
#   4. A resource group must already exist - the script creates and removes only the network
#      security group, virtual network and subnet. If you don't have one:
#                 Import-Module ./src/PS/Resources/Resources.psd1
#                 New-AzResourceGroup -SubscriptionId <id> -Name <rg> -Location eastus
#
# Usage:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<id>'
#   $env:AZCLIPS_RESOURCE_GROUP  = '<rg>'
#   ./tests/Scripts/PS/SubnetConfig.ps1
#
# Both variables are required and deliberately have no default: a smoke test must never silently
# create resources in someone else's subscription. $env:AZCLIPS_LOCATION is optional.

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
$resourceGroup  = $env:AZCLIPS_RESOURCE_GROUP

if (-not $subscriptionId)
{
    throw 'AZCLIPS_SUBSCRIPTION_ID is required. Set $env:AZCLIPS_SUBSCRIPTION_ID to the subscription this smoke test may create resources in.'
}
if (-not $resourceGroup)
{
    throw 'AZCLIPS_RESOURCE_GROUP is required. Set $env:AZCLIPS_RESOURCE_GROUP to an existing resource group; the script creates and removes only the network security group, virtual network and subnet.'
}

# Fail fast with an actionable message. Checking existence is not enough: the official Az.Network
# module exports the same cmdlet names and PowerShell auto-loads it on first use, so an
# un-imported azclips module silently resolves to the official one - which rejects
# -SubscriptionId and fails mid-script.
$commands = 'New-AzNetworkSecurityGroup', 'Get-AzNetworkSecurityGroup', 'Remove-AzNetworkSecurityGroup',
            'New-AzVirtualNetwork', 'Get-AzVirtualNetwork', 'Remove-AzVirtualNetwork',
            'New-AzVirtualNetworkSubnetConfig', 'Get-AzVirtualNetworkSubnetConfig', 'Remove-AzVirtualNetworkSubnetConfig'
foreach ($command in $commands)
{
    $resolved = Get-Command $command -ErrorAction SilentlyContinue
    if ($resolved.ModuleName -ne 'Network')
    {
        throw "$command resolves to module '$($resolved.ModuleName)', not the azclips 'Network' module. " +
              'Start a fresh pwsh and run: Import-Module ./src/PS/Network/Network.psd1'
    }
}

$location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'eastus' }
# Per-run names: New- issues a PUT, so a fixed name would silently overwrite a colliding resource
# in a shared resource group - and cleanup would then destroy it. The subnet lives in this run's
# virtual network, so its name cannot collide.
$suffix     = Get-Random -Maximum 99999
$nsgName    = "azclips-test-subnet-nsg-$suffix"
$vnetName   = "azclips-test-subnet-vnet-$suffix"
$subnetName = 'azclips-test-subnet'
$scope      = @{ SubscriptionId = $subscriptionId; ResourceGroupName = $resourceGroup }
$subnetArgs = @{ SubscriptionId = $subscriptionId; ResourceGroupName = $resourceGroup; VirtualNetworkName = $vnetName; Name = $subnetName }

# True only on a verified 404 with the given error code. Any other failure - auth, throttling,
# transport, or a missing resource group (ResourceGroupNotFound) - is not proof of absence, so it
# fails closed. Top-level resources answer ResourceNotFound; the network RP answers NotFound for a
# missing child subnet of an existing virtual network. A failed Get surfaces both as an error
# record and as a terminating exception, so both are collected.
function Test-Absent([string] $Command, [hashtable] $Parameters, [string] $Code = 'ResourceNotFound')
{
    $found    = $null
    $failures = @()
    try
    {
        $found = & $Command @Parameters -ErrorAction SilentlyContinue -ErrorVariable failures
    }
    catch
    {
        $failures += $_
    }

    if ($found)
    {
        Write-Warning "$Command found '$($Parameters.Name)' in '$resourceGroup'."
        return $false
    }

    $message = ($failures | ForEach-Object { $_.Exception.Message }) -join "`n"
    if ($message -match '\b404\b' -and $message -like "*`"$Code`"*")
    {
        return $true
    }

    Write-Warning "Unable to verify that '$($Parameters.Name)' does not exist ($Command):`n$message"
    return $false
}

# Never touch a resource this run did not create. This has to run before entering the try,
# otherwise the finally block would clean up after a run that never started.
if (-not ((Test-Absent 'Get-AzNetworkSecurityGroup' ($scope + @{ Name = $nsgName })) -and
          (Test-Absent 'Get-AzVirtualNetwork' ($scope + @{ Name = $vnetName }))))
{
    throw 'Refusing to run: every name must be verifiably unused before anything is created.'
}

# Each flag is set just before its New- call: the name was verified free above, so whatever
# exists under it from then on is this run's - including a resource whose PUT was accepted but
# whose polling then failed.
$nsgCreated  = $false
$vnetCreated = $false

try
{
    $nsgCreated = $true
    $nsg = New-AzNetworkSecurityGroup @scope -Name $nsgName -Location $location

    $subnet = New-AzVirtualNetworkSubnetConfig `
        -Name $subnetName `
        -AddressPrefix '10.20.1.0/24' `
        -NetworkSecurityGroupId $nsg.Id `
        -ServiceEndpoint 'Microsoft.Storage' `
        -DelegatedService 'Microsoft.Web.serverFarms' `
        -DefaultOutboundAccess $false

    # The config carries the legacy addressPrefixes list, not the singular addressPrefix.
    if ($subnet.Name -ne $subnetName -or ($subnet.Properties.AddressPrefixes -join ',') -ne '10.20.1.0/24')
    {
        throw 'New-AzVirtualNetworkSubnetConfig returned an unexpected subnet.'
    }

    $vnetCreated = $true
    New-AzVirtualNetwork @scope `
        -Name $vnetName `
        -Location $location `
        -AddressPrefix '10.20.0.0/16' `
        -Subnet $subnet `
        -Debug

    Get-AzVirtualNetworkSubnetConfig @subnetArgs
    Get-AzVirtualNetworkSubnetConfig @subnetArgs -ExpandResource 'networkSecurityGroup'
    Get-AzVirtualNetworkSubnetConfig @scope -VirtualNetworkName $vnetName

    $removed = Remove-AzVirtualNetworkSubnetConfig @subnetArgs -Force -PassThru
    if ($removed -ne $true)
    {
        throw "FAIL: Remove-AzVirtualNetworkSubnetConfig -PassThru returned '$removed', expected only True."
    }

    if (-not (Test-Absent 'Get-AzVirtualNetworkSubnetConfig' $subnetArgs 'NotFound'))
    {
        throw "FAIL: subnet '$subnetName' was not confirmed removed."
    }
    Write-Host "PASS: subnet '$subnetName' was created, read, listed and removed."
}
finally
{
    # Remove only what this run created. Removing the virtual network takes the subnet with it and
    # releases the NSG association, so the NSG goes last. Each removal is attempted on its own and
    # a failure is reported, so nothing is left behind silently.
    $cleanup = @(
        @{ Created = $vnetCreated; Command = 'Remove-AzVirtualNetwork';       Name = $vnetName },
        @{ Created = $nsgCreated;  Command = 'Remove-AzNetworkSecurityGroup'; Name = $nsgName })

    foreach ($step in $cleanup | Where-Object { $_.Created })
    {
        try
        {
            & $step.Command @scope -Name $step.Name -Force
        }
        catch
        {
            Write-Warning "Cleanup: $($step.Command) '$($step.Name)' failed; remove it manually. $($_.Exception.Message)"
        }
    }
}