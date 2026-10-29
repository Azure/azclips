# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Smoke test for New-/Get-/Remove-AzPublicIpAddress.
#
# Creates a zone-redundant Standard public IP with a DNS label and tags, gets it (plain and with
# the IP configuration expanded), lists the resource group, removes it, and verifies the removal
# (a Get must now fail with 404 ResourceNotFound).
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
    throw 'AZCLIPS_RESOURCE_GROUP is required. Set $env:AZCLIPS_RESOURCE_GROUP to an existing resource group; the script creates and removes only the public IP address.'
}

# Fail fast with an actionable message. Checking existence is not enough: the official Az.Network
# module exports the same cmdlet names and PowerShell auto-loads it on first use, so an
# un-imported azclips module silently resolves to the official one - which rejects
# -SubscriptionId and fails mid-script.
foreach ($verb in 'New', 'Get', 'Remove')
{
    $resolved = Get-Command "$verb-AzPublicIpAddress" -ErrorAction SilentlyContinue
    if ($resolved.ModuleName -ne 'Network')
    {
        throw "$verb-AzPublicIpAddress resolves to module '$($resolved.ModuleName)', not the azclips 'Network' module. " +
              'Start a fresh pwsh and run: Import-Module ./src/PS/Network/Network.psd1'
    }
}

$location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'eastus' }
# Per-run names: New- issues a PUT, so a fixed name would silently overwrite a colliding address
# in a shared resource group - and cleanup would then destroy it. The DNS label additionally has
# to be unique within the whole region.
$suffix   = Get-Random -Maximum 99999
$pipName  = "azclips-test-pip-$suffix"
$dnsLabel = "$pipName-$(Get-Random -Maximum 99999)"

# True only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed. A failed Get surfaces both as an error record and as a terminating
# exception, so both are collected.
function Test-Absent([string] $Name)
{
    $found    = $null
    $failures = @()
    try
    {
        $found = Get-AzPublicIpAddress -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $Name `
            -ErrorAction SilentlyContinue -ErrorVariable failures
    }
    catch
    {
        $failures += $_
    }

    if ($found)
    {
        Write-Warning "Get-AzPublicIpAddress found '$Name' in '$resourceGroup'."
        return $false
    }

    $message = ($failures | ForEach-Object { $_.Exception.Message }) -join "`n"
    if ($message -like '*404 Not Found*' -and $message -like '*ResourceNotFound*')
    {
        return $true
    }

    Write-Warning "Unable to verify that '$Name' does not exist:`n$message"
    return $false
}

# Never touch an address this run did not create. This has to run before entering the try,
# otherwise the finally block would clean up after a run that never started.
if (-not (Test-Absent $pipName))
{
    throw 'Refusing to run: the name must be verifiably unused before anything is created.'
}

# Set just before New-: the name was verified free above, so whatever exists under it from then
# on is this run's - including an address whose PUT was accepted but whose polling then failed.
$pipCreated = $false

try
{
    $pipCreated = $true
    New-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -Location $location `
        -Sku Standard `
        -AllocationMethod Static `
        -IpAddressVersion IPv4 `
        -DomainNameLabel $dnsLabel `
        -IdleTimeoutInMinutes 10 `
        -Zone 1, 2, 3 `
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

    # The delete itself writes nothing, so -PassThru's $true is the only output.
    $removed = @(Remove-AzPublicIpAddress `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $pipName `
        -Force `
        -PassThru)
    if ($removed.Count -ne 1 -or $removed[0] -ne $true)
    {
        throw "FAIL: Remove-AzPublicIpAddress -PassThru wrote '$($removed -join ', ')' instead of a single True."
    }

    if (-not (Test-Absent $pipName))
    {
        throw "FAIL: public IP address '$pipName' was not confirmed removed."
    }
    $pipCreated = $false
    Write-Host "PASS: public IP address '$pipName' was created, read, listed and removed."
}
finally
{
    # Remove only what this run created; a failure is reported, so nothing is left behind silently.
    if ($pipCreated)
    {
        try
        {
            Remove-AzPublicIpAddress -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $pipName -Force
        }
        catch
        {
            Write-Warning "Cleanup: Remove-AzPublicIpAddress '$pipName' failed; remove it manually. $($_.Exception.Message)"
        }
    }
}
