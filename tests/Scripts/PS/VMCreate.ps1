# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for New-AzVM's public IP workflows through the built Compute module.
#
# Creates four VMs in one resource group, one per public IP mode, and checks the
# detail fields New-AzVM returns:
#   1. New-AzVM                                    -> creates <vm>PublicIP; PublicIps is set
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName <existing>    -> reuses that public IP; PublicIps is set
#   4. New-AzVM -PublicIpAddressName ''            -> no public IP; PublicIps is empty
#   5. New-AzVM -PublicIpAddressName <missing id>  -> fails with "was not found" before
#                                                     anything is created
#   6. Get-AzVM / Remove-AzVM / Get-AzVM           -> the last get is expected to fail
#
# PrivateIps is checked alongside PublicIps to prove the output was enriched: an
# empty PublicIps with an empty PrivateIps means the detail lookup never ran.
#
# Case 3 pre-creates its public IP through Invoke-AzRestMethod, then verifies
# New-AzVM attaches that resource without replacing its tags.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
# The subscription needs quota for four Standard_D2s_v3 VMs and three Standard public IPs.
#
# Cleanup: on exit, whether the run passed or failed, the resource group this script
# created is deleted with Remove-AzResourceGroup, which removes the VMs, public IPs,
# NICs, VNets and NSGs with it, including the pre-created public IP. A group named
# through $env:AZCLIPS_RESOURCE_GROUP may already hold other resources, so the script
# only removes its VMs there and leaves the public IPs and network resources to you.
#
# Usage: ./tests/Scripts/PS/VMCreate.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID % 1000000
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmcreate-rg-$suffix" }

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
$defaultVm         = "vmc-def-$suffix"
$namedVm           = "vmc-nam-$suffix"
$reuseVm           = "vmc-reu-$suffix"
$noneVm            = "vmc-non-$suffix"
$missingVm         = "vmc-mis-$suffix"
$namedPublicIp     = "azclips-vmcreate-pip-$suffix"
$reusePublicIp     = "azclips-vmcreate-existing-pip-$suffix"
$missingPublicIpId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$suffix"

# Fail fast with an actionable message. Checking existence is not enough: the official
# Az modules (Az.Resources, Az.Compute) export the same cmdlet names and PowerShell
# auto-loads them on first use, so an un-imported azclips module silently resolves to
# the official one — which rejects -SubscriptionId and fails mid-script.
$requiredCommands = [ordered]@{
    'New-AzResourceGroup'    = 'Resources'
    'Remove-AzResourceGroup' = 'Resources'
    'Invoke-AzRestMethod'    = 'Resources'
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

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message)
{
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
}

function Get-PublicIpUrl([string]$Name)
{
    "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/${Name}?api-version=2025-05-01"
}

function Get-PublicIpByName([string]$Name)
{
    $response = Invoke-AzRestMethod -Method GET -Uri (Get-PublicIpUrl -Name $Name)
    $response.Content | ConvertFrom-Json
}

function Wait-PublicIpSucceeded([string]$Label, [string]$Name)
{
    $lastResult = $null
    for ($attempt = 0; $attempt -lt 60; $attempt++)
    {
        try
        {
            $publicIp = Get-PublicIpByName -Name $Name
            $lastResult = $publicIp.properties.provisioningState
            if ($lastResult -eq 'Succeeded')
            {
                Write-Host "OK: ${Label}: public IP '$Name' provisioningState is Succeeded"
                return $true
            }
            Write-Host "== waiting for public IP ${Name}: provisioningState='$lastResult' =="
        }
        catch
        {
            $lastResult = $_.Exception.Message
            Write-Host "== waiting for public IP ${Name}: GET failed: $lastResult =="
        }
        Start-Sleep -Seconds 5
    }

    Add-Failure "${Label}: public IP '$Name' did not reach provisioningState Succeeded after about 5 minutes (last result: $lastResult)"
    $false
}

# New-AzVM with per-VM network resource names; $Extra is splatted on top.
function New-SmokeVM([string]$Name, [hashtable]$Extra = @{})
{
    $parameters = @{
        SubscriptionId           = $subscriptionId
        ResourceGroupName        = $resourceGroup
        Name                     = $Name
        Location                 = $location
        VmSize                   = 'Standard_D2s_v3'
        AdminUsername            = 'Artemis'
        AdminPassword            = $env:AZCLIPS_VM_ADMIN_PASSWORD
        NetworkInterfaceName     = "$Name-nic"
        VirtualNetworkName       = "$Name-vnet"
        AddressPrefixes          = '10.0.0.0/16'
        SubnetNames              = "$Name-subnet"
        SubnetAddressPrefixes    = '10.0.2.0/24'
        NetworkSecurityGroupName = "$Name-nsg"
    }
    New-AzVM @parameters @Extra
}

function Test-PublicIpResource([string]$Label, [string]$ExpectedName, [string]$ExpectedAddress, [string]$ExpectedSmokeTag = '')
{
    try
    {
        $publicIp = Get-PublicIpByName -Name $ExpectedName
    }
    catch
    {
        Add-Failure "${Label}: could not read public IP '$ExpectedName': $($_.Exception.Message)"
        return
    }

    if ($publicIp.properties.ipAddress -ne $ExpectedAddress)
    {
        Add-Failure "${Label}: public IP '$ExpectedName' address '$($publicIp.properties.ipAddress)' did not match PublicIps '$ExpectedAddress'"
        return
    }

    if ($ExpectedSmokeTag -and $publicIp.tags.'azclips-smoke' -ne $ExpectedSmokeTag)
    {
        Add-Failure "${Label}: public IP '$ExpectedName' azclips-smoke tag '$($publicIp.tags.'azclips-smoke')' did not match '$ExpectedSmokeTag'"
        return
    }

    Write-Host "OK: ${Label}: public IP '$ExpectedName' address matches PublicIps='$ExpectedAddress'"
}

# PrivateIps proves the detail lookup ran; then PublicIps must be set or empty as expected.
function Test-PublicIp([string]$Label, $Vm, [bool]$ExpectPublicIp, [string]$ExpectedPublicIpName = '', [string]$ExpectedSmokeTag = '')
{
    if ([string]::IsNullOrEmpty($Vm.PrivateIps))
    {
        Add-Failure "${Label}: PrivateIps is empty, so the output was not enriched"
    }
    elseif ($ExpectPublicIp -and [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected a public IP, got PublicIps=''"
    }
    elseif (-not $ExpectPublicIp -and -not [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected no public IP, got PublicIps='$($Vm.PublicIps)'"
    }
    else
    {
        Write-Host "OK: ${Label}: PublicIps='$($Vm.PublicIps)' PrivateIps='$($Vm.PrivateIps)'"
    }

    if ($ExpectPublicIp -and -not [string]::IsNullOrEmpty($Vm.PublicIps) -and -not [string]::IsNullOrEmpty($ExpectedPublicIpName))
    {
        Test-PublicIpResource -Label $Label -ExpectedName $ExpectedPublicIpName -ExpectedAddress $Vm.PublicIps -ExpectedSmokeTag $ExpectedSmokeTag
    }
}

# Guard the cleanup block: each resource is only deleted once it exists.
$groupCreated = $false
$createdVms   = [System.Collections.Generic.List[string]]::new()

# Only a group this script named itself is safe to delete. One supplied through
# AZCLIPS_RESOURCE_GROUP may already hold other resources.
$ownsResourceGroup = -not $env:AZCLIPS_RESOURCE_GROUP

try
{
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Location $location
    $groupCreated = $true

    Write-Host "== 1. New-AzVM (default public IP ${defaultVm}PublicIP) =="
    $createdVms.Add($defaultVm)
    $defaultResult = New-SmokeVM -Name $defaultVm
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"

    Write-Host "== 2. New-AzVM -PublicIpAddressName $namedPublicIp (new name) =="
    $createdVms.Add($namedVm)
    $namedResult = New-SmokeVM -Name $namedVm -Extra @{ PublicIpAddressName = $namedPublicIp }
    Test-PublicIp -Label 'named' -Vm $namedResult -ExpectPublicIp $true -ExpectedPublicIpName $namedPublicIp

    Write-Host "== 3. New-AzVM -PublicIpAddressName $reusePublicIp (existing name) =="
    $body = @{
        location = $location
        sku = @{ name = 'Standard' }
        tags = @{ 'azclips-smoke' = 'reuse' }
        properties = @{ publicIPAllocationMethod = 'Static' }
    }
    $reusePublicIpCreated = $false
    try
    {
        $null = Invoke-AzRestMethod -Method PUT -Uri (Get-PublicIpUrl -Name $reusePublicIp) -Payload ($body | ConvertTo-Json -Depth 5 -Compress)
        $reusePublicIpCreated = $true
    }
    catch
    {
        Add-Failure "reuse: failed to pre-create public IP '$reusePublicIp': $($_.Exception.Message)"
    }

    if ($reusePublicIpCreated)
    {
        if (Wait-PublicIpSucceeded -Label 'reuse' -Name $reusePublicIp)
        {
            $createdVms.Add($reuseVm)
            $reuseResult = New-SmokeVM -Name $reuseVm -Extra @{ PublicIpAddressName = $reusePublicIp }
            Test-PublicIp -Label 'reuse' -Vm $reuseResult -ExpectPublicIp $true -ExpectedPublicIpName $reusePublicIp -ExpectedSmokeTag 'reuse'
        }
        else
        {
            Write-Host '== reuse: skipping New-AzVM because public IP pre-create did not complete =='
        }
    }

    Write-Host "== 4. New-AzVM -PublicIpAddressName '' (no public IP) =="
    $createdVms.Add($noneVm)
    Test-PublicIp -Label 'none' -Vm (New-SmokeVM -Name $noneVm -Extra @{ PublicIpAddressName = '' }) -ExpectPublicIp $false

    Write-Host "== 5. New-AzVM -PublicIpAddressName <missing resource id> (expect 'was not found') =="
    $createdVms.Add($missingVm)
    $expected = "Public IP address '$missingPublicIpId' was not found."
    try
    {
        $null = New-SmokeVM -Name $missingVm -Extra @{ PublicIpAddressName = $missingPublicIpId }
        Add-Failure 'missing id: expected New-AzVM to fail'
    }
    catch
    {
        if ("$_".Contains($expected) -or $_.Exception.Message.Contains($expected))
        {
            Write-Host 'OK: missing id: failed with the expected message'
        }
        else
        {
            Add-Failure "missing id: New-AzVM failed, but not with the expected message: $_"
        }
    }

    Write-Host '== 6. Get-AzVM, Remove-AzVM, Get-AzVM (the last get is expected to fail) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
    Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm -Force
    $null = $createdVms.Remove($defaultVm)
    try
    {
        $null = Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
        Add-Failure "Get-AzVM still finds $defaultVm after Remove-AzVM"
    }
    catch
    {
        Write-Host "OK: $defaultVm no longer exists"
    }

    if ($failures.Count -gt 0)
    {
        throw "New-AzVM smoke test finished with $($failures.Count) failure(s)."
    }
    Write-Host '== New-AzVM smoke test finished =='
}
finally
{
    # Cleanup failures are warnings, not throws: a throw here would replace the exception
    # that ended the run.
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
        foreach ($vm in $createdVms)
        {
            Write-Host "== cleanup: removing VM $vm =="
            try
            {
                Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vm -Force
            }
            catch
            {
                Write-Warning "Could not remove VM ${vm}. Error: $($_.Exception.Message)"
            }
        }
        Write-Host "== cleanup: kept resource group $resourceGroup because AZCLIPS_RESOURCE_GROUP supplied it; remove its public IPs and network resources yourself =="
    }
}
