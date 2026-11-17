# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzNetworkInterface.
#
# Creates a virtual network (one subnet) and a Standard public IP as prerequisites, then creates a
# network interface that references both by resource id, gets it (plain and with the public IP
# expanded), lists the resource group, removes it, and verifies the removal (a Get must now fail
# with 404 ResourceNotFound). The prerequisites are removed on exit.
#
# NSG association is not exercised: azclips has no cmdlet that creates a network security group.
# It is covered offline by tests/Cli.Tests/NetworkInterfaceParameterTests.cs and
# tests/Scenario.Tests/NetworkInterfaceScenarioTests.cs.
#
# Prereqs (run in a fresh pwsh, so an installed gallery Az.Network cannot shadow this module):
#   1. Build:     dotnet build azclips.slnx
#   2. Sign in:   Import-Module ./src/PS/Accounts/Accounts.psd1
#                 Connect-AzAccount
#   3. Module:    Import-Module ./src/PS/Network/Network.psd1
#   4. A resource group must already exist - the script creates and removes only the network
#      interface, virtual network and public IP address. If you don't have one:
#                 Import-Module ./src/PS/Resources/Resources.psd1
#                 New-AzResourceGroup -SubscriptionId <id> -Name <rg> -Location eastus
#
# Usage:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<id>'
#   $env:AZCLIPS_RESOURCE_GROUP  = '<rg>'
#   ./tests/Scripts/PS/NicCreate.ps1
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
    throw 'AZCLIPS_RESOURCE_GROUP is required. Set $env:AZCLIPS_RESOURCE_GROUP to an existing resource group; the script creates and removes only the network interface, virtual network and public IP address.'
}

# Fail fast with an actionable message. Checking existence is not enough: the official Az.Network
# module exports the same cmdlet names and PowerShell auto-loads it on first use, so an
# un-imported azclips module silently resolves to the official one - which rejects
# -SubscriptionId and fails mid-script.
foreach ($noun in 'VirtualNetwork', 'PublicIpAddress', 'NetworkInterface')
{
    foreach ($verb in 'New', 'Get', 'Remove')
    {
        $resolved = Get-Command "$verb-Az$noun" -ErrorAction SilentlyContinue
        if ($resolved.ModuleName -ne 'Network')
        {
            throw "$verb-Az$noun resolves to module '$($resolved.ModuleName)', not the azclips 'Network' module. " +
                  'Start a fresh pwsh and run: Import-Module ./src/PS/Network/Network.psd1'
        }
    }
}

$location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'eastus' }
# Per-run names: New- issues a PUT, so a fixed name would silently overwrite a colliding resource
# in a shared resource group - and cleanup would then destroy it.
$suffix     = Get-Random -Maximum 99999
$vnetName   = "azclips-test-nic-vnet-$suffix"
$subnetName = 'azclips-test-subnet'
$pipName    = "azclips-test-nic-pip-$suffix"
$nicName    = "azclips-test-nic-$suffix"

# True only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed. A failed Get surfaces both as an error record and as a terminating
# exception, so both are collected.
function Test-Absent([string] $Command, [string] $Name)
{
    $found    = $null
    $failures = @()
    try
    {
        $found = & $Command -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $Name `
            -ErrorAction SilentlyContinue -ErrorVariable failures
    }
    catch
    {
        $failures += $_
    }

    if ($found)
    {
        Write-Warning "$Command found '$Name' in '$resourceGroup'."
        return $false
    }

    $message = ($failures | ForEach-Object { $_.Exception.Message }) -join "`n"
    if ($message -like '*404 Not Found*' -and $message -like '*ResourceNotFound*')
    {
        return $true
    }

    Write-Warning "Unable to verify that '$Name' does not exist ($Command):`n$message"
    return $false
}

# Never touch a resource this run did not create. This has to run before entering the try,
# otherwise the finally block would clean up after a run that never started.
if (-not ((Test-Absent 'Get-AzVirtualNetwork' $vnetName) -and
          (Test-Absent 'Get-AzPublicIpAddress' $pipName) -and
          (Test-Absent 'Get-AzNetworkInterface' $nicName)))
{
    throw 'Refusing to run: every name must be verifiably unused before anything is created.'
}

# Each flag is set just before its New- call: the name was verified free above, so whatever
# exists under it from then on is this run's - including a resource whose PUT was accepted but
# whose polling then failed.
$vnetCreated = $false
$pipCreated  = $false
$nicCreated  = $false

try
{
    $vnetCreated = $true
    $vnet = New-AzVirtualNetwork `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vnetName `
        -Location $location `
        -AddressPrefix '10.0.0.0/16' `
        -Subnet @{ Name = $subnetName; AddressPrefix = '10.0.0.0/24' }

    $pipCreated = $true
    $pip = New-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -Location $location `
        -Sku Standard `
        -AllocationMethod Static

    $nicCreated = $true
    New-AzNetworkInterface `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $nicName `
        -Location $location `
        -SubnetId $vnet.Subnets[0].Id `
        -PublicIpAddressId $pip.Id `
        -PrivateIpAddress '10.0.0.10' `
        -EnableIPForwarding `
        -DnsServer '10.0.0.4', '10.0.0.5' `
        -InternalDnsNameLabel $nicName `
        -Tag @{ owner = 'azclips'; scenario = 'nic-smoke' } `
        -Debug

    Get-AzNetworkInterface `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $nicName

    Get-AzNetworkInterface `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $nicName `
        -ExpandResource 'IpConfigurations/PublicIpAddress'

    Get-AzNetworkInterface `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup

    Remove-AzNetworkInterface `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $nicName `
        -Force `
        -PassThru

    if (-not (Test-Absent 'Get-AzNetworkInterface' $nicName))
    {
        throw "FAIL: network interface '$nicName' was not confirmed removed."
    }
    $nicCreated = $false
    Write-Host "PASS: network interface '$nicName' was created, read, listed and removed."
}
finally
{
    # Remove only what this run created, dependents first: the network interface references the
    # subnet and the public IP, so neither can be removed while it exists. Each removal is
    # attempted on its own and a failure is reported, so nothing is left behind silently.
    $cleanup = @(
        @{ Created = $nicCreated;  Command = 'Remove-AzNetworkInterface'; Name = $nicName },
        @{ Created = $pipCreated;  Command = 'Remove-AzPublicIpAddress';  Name = $pipName },
        @{ Created = $vnetCreated; Command = 'Remove-AzVirtualNetwork';   Name = $vnetName })

    foreach ($step in $cleanup | Where-Object { $_.Created })
    {
        try
        {
            & $step.Command -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $step.Name -Force
        }
        catch
        {
            Write-Warning "Cleanup: $($step.Command) '$($step.Name)' failed; remove it manually. $($_.Exception.Message)"
        }
    }
}
