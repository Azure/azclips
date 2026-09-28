# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for New-AzVM's public IP workflows through the built Compute module.
#
# Creates three VMs in one resource group, one per public IP mode, and checks the
# detail fields New-AzVM returns:
#   1. New-AzVM                                    -> creates <vm>PublicIP; PublicIps is set
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName ''            -> no public IP; PublicIps is empty
#   4. New-AzVM -PublicIpAddressName <missing id>  -> fails with "was not found" before
#                                                     anything is created
#   5. Get-AzVM / Remove-AzVM / Get-AzVM           -> the last get is expected to fail
#
# PrivateIps is checked alongside PublicIps to prove the output was enriched: an
# empty PublicIps with an empty PrivateIps means the detail lookup never ran.
#
# Reusing an existing public IP is not covered: an unattached public IP can't be
# created with these modules.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
# The subscription needs quota for three Standard_D2s_v3 VMs and two Standard public IPs.
#
# Cleanup: on exit, whether the run passed or failed, the resource group this script
# created is deleted with Remove-AzResourceGroup, which removes the VMs, public IPs,
# NICs, VNets and NSGs with it. A group named through $env:AZCLIPS_RESOURCE_GROUP may
# already hold other resources, so the script only removes its VMs there.
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
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmcreate-rg-$suffix" }

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
$defaultVm         = "vmc-def-$suffix"
$namedVm           = "vmc-nam-$suffix"
$noneVm            = "vmc-non-$suffix"
$missingVm         = "vmc-mis-$suffix"
$namedPublicIp     = "azclips-vmcreate-pip-$suffix"
$missingPublicIpId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$suffix"

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

# PrivateIps proves the detail lookup ran; then PublicIps must be set or empty as expected.
function Test-PublicIp([string]$Label, $Vm, [bool]$ExpectPublicIp)
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
    Test-PublicIp -Label 'default' -Vm (New-SmokeVM -Name $defaultVm) -ExpectPublicIp $true

    Write-Host "== 2. New-AzVM -PublicIpAddressName $namedPublicIp (new name) =="
    $createdVms.Add($namedVm)
    Test-PublicIp -Label 'named' -Vm (New-SmokeVM -Name $namedVm -Extra @{ PublicIpAddressName = $namedPublicIp }) -ExpectPublicIp $true

    Write-Host "== 3. New-AzVM -PublicIpAddressName '' (no public IP) =="
    $createdVms.Add($noneVm)
    Test-PublicIp -Label 'none' -Vm (New-SmokeVM -Name $noneVm -Extra @{ PublicIpAddressName = '' }) -ExpectPublicIp $false

    Write-Host "== 4. New-AzVM -PublicIpAddressName <missing resource id> (expect 'was not found') =="
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

    Write-Host '== 5. Get-AzVM, Remove-AzVM, Get-AzVM (the last get is expected to fail) =='
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
