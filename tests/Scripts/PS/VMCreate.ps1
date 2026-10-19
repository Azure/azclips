# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for New-AzVM's public IP and network security group workflows through
# the built Compute module.
#
# Creates VMs in one resource group, one per public IP mode/security mode, and checks the
# detail fields New-AzVM returns:
#   1. New-AzVM -SecurityType TrustedLaunch        -> enables secure boot/vTPM; creates
#                                                     <vm>PublicIP; PublicIps is set;
#                                                     creates <vm>NSG with only the rdp rule
#   2. New-AzVM -SecurityType Standard -PublicIpAddressName <new name>
#                                                    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName <existing>    -> reuses that public IP; PublicIps is set
#   4. New-AzVM -PublicIpAddressName ''            -> no public IP; PublicIps is empty;
#               -SecurityGroupName <existing name>    attaches the pre-created NSG untouched
#   5. New-AzVM -PublicIpAddressName <missing id>  -> fails with "was not found" before
#                                                     anything is created
#   6. Get-AzVM / Remove-AzVM / Get-AzVM           -> the last get is expected to fail
#   7. optional AZCLIPS_ENABLE_CVM=1               -> creates a ConfidentialVM with OS disk security encryption
#   8. optional AZCLIPS_ENABLE_EXISTING_NIC=1      -> subnet name/ID selection, missing-subnet
#                                                     preflight, two NICs and a shared delete option
#
# PrivateIps is checked alongside PublicIps to prove the output was enriched: an
# empty PublicIps with an empty PrivateIps means the detail lookup never ran.
#
# Case 3 pre-creates its public IP through Invoke-AzRestMethod, then verifies
# New-AzVM attaches that resource without replacing its tags.
#
# Before case 1 the script pre-creates an NSG through Invoke-AzRestMethod, with its
# own tag and one rule, smoke-allow-https. The NSG checks read the VM's NIC and the
# NSG back through Invoke-AzRestMethod: the NIC must reference the expected NSG, and
# the NSG must hold exactly one rule: rdp for case 1's default <vm>NSG, and
# smoke-allow-https, with the tag intact, for the NSG that cases 3 and 4 attach.
# Every other VM gets its own default <vm>NSG.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
# The subscription needs quota for four Standard_D2s_v3 VMs and three Standard public IPs.
#
# Standard run:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<subscription-id>'
#   $env:AZCLIPS_VM_ADMIN_PASSWORD = '<test-password>'
#   ./tests/Scripts/PS/VMCreate.ps1
#
# The standard run covers TrustedLaunch, Standard, public IP create/reuse/none,
# missing public IP validation, and remove/show validation.
#
# To also test ConfidentialVM, set:
#   $env:AZCLIPS_ENABLE_CVM = '1'
#   $env:AZCLIPS_CVM_IMAGE = 'Canonical:0001-com-ubuntu-confidential-vm-jammy:22_04-lts-cvm:latest'
#   $env:AZCLIPS_CVM_SIZE = 'Standard_DC2as_v5'
#   $env:AZCLIPS_CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE = 'VMGuestStateOnly'
#
# To also test networking (three additional Standard_D2s_v3 VMs, no public IPs), set:
#   $env:AZCLIPS_ENABLE_EXISTING_NIC = '1'
#
# Cleanup: on exit, whether the run passed or failed, the resource group this script
# created is deleted with Remove-AzResourceGroup, which removes the VMs, public IPs,
# NICs, VNets and NSGs with it, including the pre-created public IP and NSG. A group
# named through $env:AZCLIPS_RESOURCE_GROUP may already hold other resources, so the
# script only removes its VMs there and leaves the public IPs and network resources,
# including the pre-created NSG, to you.
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
$cvmVm             = "vmc-cvm-$suffix"
$existingNicVm     = "vmc-nic-$suffix"
$subnetNameVm      = "vmc-sub-$suffix"
$subnetIdVm        = "vmc-sid-$suffix"
$missingSubnetVm  = "vmc-msu-$suffix"
$existingNicVnet   = "vmc-nic-vnet-$suffix"
$existingNicSubnet = 'default'
$existingNicName   = "vmc-existing-nic-$suffix"
$existingNicId     = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkInterfaces/$existingNicName"
$secondNicName     = "vmc-second-nic-$suffix"
$secondNicId       = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkInterfaces/$secondNicName"
$existingNicSubnetId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/virtualNetworks/$existingNicVnet/subnets/$existingNicSubnet"
$namedPublicIp     = "azclips-vmcreate-pip-$suffix"
$reusePublicIp     = "azclips-vmcreate-existing-pip-$suffix"
$missingPublicIpId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$suffix"
$cvmEnabled        = $env:AZCLIPS_ENABLE_CVM -eq '1'
$existingNicEnabled = $env:AZCLIPS_ENABLE_EXISTING_NIC -eq '1'
$cvmImage          = $env:AZCLIPS_CVM_IMAGE
$cvmSize           = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_CVM_SIZE)) { 'Standard_DC2as_v5' } else { $env:AZCLIPS_CVM_SIZE }
$cvmEncryptionType = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE)) { 'VMGuestStateOnly' } else { $env:AZCLIPS_CVM_OS_DISK_SECURITY_ENCRYPTION_TYPE }
$existingNsg       = "azclips-vmcreate-existing-nsg-$suffix"
$existingNsgId     = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkSecurityGroups/$existingNsg"
$defaultNsgId      = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/networkSecurityGroups/${defaultVm}NSG"

# Rules as name/protocol/source address/source port/destination address/destination port/access/direction/priority; see Format-NsgRule.
$defaultNsgRule    = 'rdp/Tcp/*/*/*/3389/Allow/Inbound/1000'
$existingNsgRule   = 'smoke-allow-https/Tcp/*/*/*/443/Allow/Inbound/1100'

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

# ARM URL of a network resource in this script's resource group, e.g. -Type publicIPAddresses.
function Get-NetworkUrl([string]$Type, [string]$Name)
{
    "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/$Type/${Name}?api-version=2025-05-01"
}

function Get-NetworkResource([string]$Type, [string]$Name)
{
    $response = Invoke-AzRestMethod -Method GET -Uri (Get-NetworkUrl -Type $Type -Name $Name)
    $response.Content | ConvertFrom-Json
}

# Polls a network resource until its provisioningState is Succeeded; $What names it in messages.
function Wait-ResourceSucceeded([string]$Label, [string]$Type, [string]$Name, [string]$What)
{
    $lastResult = $null
    for ($attempt = 0; $attempt -lt 24; $attempt++)
    {
        try
        {
            $resource = Get-NetworkResource -Type $Type -Name $Name
            $lastResult = $resource.properties.provisioningState
            if ($lastResult -eq 'Succeeded')
            {
                Write-Host "OK: ${Label}: $What provisioningState is Succeeded"
                return $true
            }
            Write-Host "== waiting for ${What}: provisioningState='$lastResult' =="
        }
        catch
        {
            $lastResult = $_.Exception.Message
            Write-Host "== waiting for ${What}: GET failed: $lastResult =="
        }
        Start-Sleep -Seconds 5
    }

    Add-Failure "${Label}: $What did not reach provisioningState Succeeded after about 2 minutes (last result: $lastResult)"
    $false
}

# New-AzVM with per-VM network resource names; $Extra is splatted on top. Unless $Extra
# sets SecurityGroupName, the VM gets its default <vm>NSG.
function New-SmokeVM([string]$Name, [hashtable]$Extra = @{})
{
    $parameters = @{
        SubscriptionId        = $subscriptionId
        ResourceGroupName     = $resourceGroup
        Name                  = $Name
        Location              = $location
        VmSize                = 'Standard_D2s_v3'
        AdminUsername         = 'Artemis'
        AdminPassword         = $env:AZCLIPS_VM_ADMIN_PASSWORD
        NetworkInterfaceName  = "$Name-nic"
        VirtualNetworkName    = "$Name-vnet"
        AddressPrefixes       = '10.0.0.0/16'
        SubnetNames           = "$Name-subnet"
        SubnetAddressPrefixes = '10.0.2.0/24'
    }
    foreach ($key in $Extra.Keys)
    {
        $parameters[$key] = $Extra[$key]
    }
    New-AzVM @parameters
}

function New-ConfidentialSmokeVM([string]$Name)
{
    New-SmokeVM -Name $Name -Extra @{
        Image = $cvmImage
        VmSize = $cvmSize
        SecurityType = 'ConfidentialVM'
        OSDiskSecurityEncryptionType = $cvmEncryptionType
        PublicIpAddressName = ''
    }
}

function New-NetworkSmokeVM([string]$Name, [hashtable]$Extra)
{
    $parameters = @{
        SubscriptionId = $subscriptionId
        ResourceGroupName = $resourceGroup
        Name = $Name
        Location = $location
        VmSize = 'Standard_D2s_v3'
        AdminUsername = 'Artemis'
        AdminPassword = $env:AZCLIPS_VM_ADMIN_PASSWORD
    }
    foreach ($key in $Extra.Keys)
    {
        $parameters[$key] = $Extra[$key]
    }
    $createdVms.Add($Name)
    New-AzVM @parameters
}

function Get-SmokeVMResource([string]$Name)
{
    $url = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines/${Name}?api-version=2025-11-01"
    (Invoke-AzRestMethod -Method GET -Uri $url).Content | ConvertFrom-Json
}

function Test-NoNetworkSideEffects([string]$Name)
{
    foreach ($type in @('networkInterfaces', 'networkSecurityGroups', 'publicIPAddresses', 'virtualNetworks'))
    {
        $url = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/${type}?api-version=2025-05-01"
        $resources = (Invoke-AzRestMethod -Method GET -Uri $url).Content | ConvertFrom-Json
        foreach ($resource in $resources.value)
        {
            if ($resource.name -in @("$Name-nic", "${Name}NSG", "${Name}PublicIP", "$Name-vnet"))
            {
                Add-Failure "${Name}: unexpected generated resource '$($resource.name)' in $type"
            }
        }
    }
}

function Test-PublicIpResource([string]$Label, [string]$ExpectedName, [string]$ExpectedAddress, [string]$ExpectedSmokeTag = '')
{
    try
    {
        $publicIp = Get-NetworkResource -Type 'publicIPAddresses' -Name $ExpectedName
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

# A rule as name/protocol/source address/source port/destination address/destination port/access/direction/priority, so whole rules compare as one string.
function Format-NsgRule($Rule)
{
    $p = $Rule.properties
    "$($Rule.name)/$($p.protocol)/$($p.sourceAddressPrefix)/$($p.sourcePortRange)/$($p.destinationAddressPrefix)/$($p.destinationPortRange)/$($p.access)/$($p.direction)/$($p.priority)"
}

# The NIC <vm>-nic must reference the NSG $ExpectedId; -ne compares case-insensitively, which
# matters because ARM can return the resource group in another case. That NSG must hold exactly
# one rule, equal to $ExpectedRule, and carry the azclips-smoke tag $ExpectedSmokeTag when given.
function Test-Nsg([string]$Label, [string]$VmName, [string]$ExpectedId, [string]$ExpectedRule, [string]$ExpectedSmokeTag = '')
{
    $nsgName = ($ExpectedId -split '/')[-1]
    try
    {
        $nic = Get-NetworkResource -Type 'networkInterfaces' -Name "$VmName-nic"
        $nsg = Get-NetworkResource -Type 'networkSecurityGroups' -Name $nsgName
    }
    catch
    {
        Add-Failure "${Label}: could not read NIC '$VmName-nic' or NSG '$nsgName': $($_.Exception.Message)"
        return
    }

    if ($nic.properties.networkSecurityGroup.id -ne $ExpectedId)
    {
        Add-Failure "${Label}: NIC '$VmName-nic' references NSG '$($nic.properties.networkSecurityGroup.id)', expected '$ExpectedId'"
        return
    }

    $rules = @($nsg.properties.securityRules | Where-Object { $_ })
    $actualRule = if ($rules.Count -gt 0) { Format-NsgRule $rules[0] } else { '' }
    if ($rules.Count -ne 1 -or $actualRule -ne $ExpectedRule)
    {
        Add-Failure "${Label}: NSG '$nsgName' has $($rules.Count) rule(s), the first '$actualRule'; expected only '$ExpectedRule'"
        return
    }

    if ($ExpectedSmokeTag -and $nsg.tags.'azclips-smoke' -ne $ExpectedSmokeTag)
    {
        Add-Failure "${Label}: NSG '$nsgName' azclips-smoke tag '$($nsg.tags.'azclips-smoke')' did not match '$ExpectedSmokeTag'"
        return
    }

    Write-Host "OK: ${Label}: NIC '$VmName-nic' uses NSG '$nsgName', whose only rule is '$ExpectedRule'"
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

    Write-Host "== 1. New-AzVM -SecurityType TrustedLaunch (default public IP ${defaultVm}PublicIP) =="
    Write-Host "== pre-create NSG $existingNsg (attached by cases 3 and 4) =="
    $nsgBody = @{
        location = $location
        tags = @{ 'azclips-smoke' = 'reuse-nsg' }
        properties = @{
            securityRules = @(
                @{
                    name = 'smoke-allow-https'
                    properties = @{
                        protocol                 = 'Tcp'
                        sourcePortRange          = '*'
                        destinationPortRange     = '443'
                        sourceAddressPrefix      = '*'
                        destinationAddressPrefix = '*'
                        access                   = 'Allow'
                        priority                 = 1100
                        direction                = 'Inbound'
                    }
                }
            )
        }
    }
    # Without a ready NSG, cases 3 and 4 still run their public IP checks, just without it.
    $existingNsgReady = $false
    try
    {
        $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'networkSecurityGroups' -Name $existingNsg) -Payload ($nsgBody | ConvertTo-Json -Depth 6 -Compress)
        $existingNsgReady = Wait-ResourceSucceeded -Label 'existing nsg' -Type 'networkSecurityGroups' -Name $existingNsg -What "NSG '$existingNsg'"
    }
    catch
    {
        Add-Failure "existing nsg: failed to pre-create NSG '$existingNsg': $($_.Exception.Message)"
    }
    Write-Host "== 1. New-AzVM -SecurityType TrustedLaunch -EnableSecureBoot -EnableVtpm (default public IP ${defaultVm}PublicIP, default NSG ${defaultVm}NSG) =="
    $createdVms.Add($defaultVm)
    $defaultResult = New-SmokeVM -Name $defaultVm -Extra @{ SecurityType = 'TrustedLaunch'; EnableSecureBoot = $true; EnableVtpm = $true }
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule

    Write-Host "== 2. New-AzVM -SecurityType Standard -PublicIpAddressName $namedPublicIp (new name) =="
    $createdVms.Add($namedVm)
    $namedResult = New-SmokeVM -Name $namedVm -Extra @{ SecurityType = 'Standard'; PublicIpAddressName = $namedPublicIp }
    Test-PublicIp -Label 'named' -Vm $namedResult -ExpectPublicIp $true -ExpectedPublicIpName $namedPublicIp

    Write-Host "== 3. New-AzVM -PublicIpAddressName $reusePublicIp -SecurityGroupName $existingNsg (existing names) =="
    $body = @{
        location = $location
        sku = @{ name = 'Standard' }
        tags = @{ 'azclips-smoke' = 'reuse' }
        properties = @{ publicIPAllocationMethod = 'Static' }
    }
    $reusePublicIpCreated = $false
    try
    {
        $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'publicIPAddresses' -Name $reusePublicIp) -Payload ($body | ConvertTo-Json -Depth 5 -Compress)
        $reusePublicIpCreated = $true
    }
    catch
    {
        Add-Failure "reuse: failed to pre-create public IP '$reusePublicIp': $($_.Exception.Message)"
    }

    if ($reusePublicIpCreated)
    {
        if (Wait-ResourceSucceeded -Label 'reuse' -Type 'publicIPAddresses' -Name $reusePublicIp -What "public IP '$reusePublicIp'")
        {
            $createdVms.Add($reuseVm)
            $reuseExtra = @{ PublicIpAddressName = $reusePublicIp }
            if ($existingNsgReady) { $reuseExtra.SecurityGroupName = $existingNsg }
            $reuseResult = New-SmokeVM -Name $reuseVm -Extra $reuseExtra
            Test-PublicIp -Label 'reuse' -Vm $reuseResult -ExpectPublicIp $true -ExpectedPublicIpName $reusePublicIp -ExpectedSmokeTag 'reuse'
            if ($existingNsgReady)
            {
                Test-Nsg -Label 'reuse' -VmName $reuseVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
            }
        }
        else
        {
            Write-Host '== reuse: skipping New-AzVM because public IP pre-create did not complete =='
        }
    }

    Write-Host "== 4. New-AzVM -PublicIpAddressName '' -SecurityGroupName <existing NSG id> (no public IP, existing NSG) =="
    $createdVms.Add($noneVm)
    $noneExtra = @{ PublicIpAddressName = '' }
    if ($existingNsgReady) { $noneExtra.SecurityGroupName = $existingNsgId }
    Test-PublicIp -Label 'none' -Vm (New-SmokeVM -Name $noneVm -Extra $noneExtra) -ExpectPublicIp $false
    if ($existingNsgReady)
    {
        Test-Nsg -Label 'none' -VmName $noneVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
    }

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

    if ($cvmEnabled)
    {
        Write-Host "== 7. New-AzVM -SecurityType ConfidentialVM -OSDiskSecurityEncryptionType $cvmEncryptionType =="
        if ([string]::IsNullOrWhiteSpace($cvmImage))
        {
            Add-Failure 'cvm: AZCLIPS_CVM_IMAGE must be set when AZCLIPS_ENABLE_CVM=1'
        }
        else
        {
            $createdVms.Add($cvmVm)
            Test-PublicIp -Label 'cvm' -Vm (New-ConfidentialSmokeVM -Name $cvmVm) -ExpectPublicIp $false
        }
    }
    else
    {
        Write-Host '== 7. skipping ConfidentialVM live create; set AZCLIPS_ENABLE_CVM=1 and AZCLIPS_CVM_IMAGE to enable =='
    }

    if ($existingNicEnabled)
    {
        Write-Host '== 8. subnet selection, preflight and existing-NIC networking =='
        try
        {
            $vnetBody = @{
                location = $location
                tags = @{ 'azclips-smoke' = 'network-selection' }
                properties = @{
                    addressSpace = @{ addressPrefixes = @('10.50.0.0/16') }
                    subnets = @(
                        @{
                            name = $existingNicSubnet
                            properties = @{ addressPrefix = '10.50.0.0/24' }
                        }
                    )
                }
            }
            $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'virtualNetworks' -Name $existingNicVnet) -Payload ($vnetBody | ConvertTo-Json -Depth 6 -Compress)
            if (-not (Wait-ResourceSucceeded -Label 'existing nic' -Type 'virtualNetworks' -Name $existingNicVnet -What "VNet '$existingNicVnet'"))
            {
                throw "VNet '$existingNicVnet' did not finish provisioning."
            }

            $nicBody = @{
                location = $location
                properties = @{
                    ipConfigurations = @(
                        @{
                            name = 'ipconfig1'
                            properties = @{
                                privateIPAllocationMethod = 'Dynamic'
                                subnet = @{ id = $existingNicSubnetId }
                            }
                        }
                    )
                }
            }
            $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'networkInterfaces' -Name $existingNicName) -Payload ($nicBody | ConvertTo-Json -Depth 7 -Compress)
            if (-not (Wait-ResourceSucceeded -Label 'existing nic' -Type 'networkInterfaces' -Name $existingNicName -What "NIC '$existingNicName'"))
            {
                throw "NIC '$existingNicName' did not finish provisioning."
            }

            foreach ($name in @($subnetNameVm, $subnetIdVm))
            {
                $extra = @{
                    NetworkInterfaceName = "$name-nic"
                    SubnetName = $existingNicSubnetId
                    PublicIpAddressName = ''
                    NetworkInterfaceDeleteOption = 'Delete'
                }
                if ($name -eq $subnetNameVm)
                {
                    $extra.SubnetName = $existingNicSubnet
                    $extra.VirtualNetworkName = $existingNicVnet
                }
                $null = New-NetworkSmokeVM -Name $name -Extra $extra
                $nic = Get-NetworkResource -Type 'networkInterfaces' -Name "$name-nic"
                if ($nic.properties.ipConfigurations[0].properties.subnet.id -ne $existingNicSubnetId)
                {
                    Add-Failure "${name}: created NIC references the wrong subnet"
                }
                $vm = Get-SmokeVMResource -Name $name
                $references = @($vm.properties.networkProfile.networkInterfaces)
                if ($references.Count -ne 1 -or $references[0].id -ne $nic.id -or
                    $references[0].properties.deleteOption -ne 'Delete')
                {
                    Add-Failure "${name}: expected one created NIC with delete option Delete"
                }
            }

            $vnet = Get-NetworkResource -Type 'virtualNetworks' -Name $existingNicVnet
            if ($vnet.tags.'azclips-smoke' -ne 'network-selection')
            {
                Add-Failure 'subnet selection: existing VNet was overwritten'
            }

            $missingSubnetId = "$existingNicSubnetId-missing"
            try
            {
                $null = New-NetworkSmokeVM -Name $missingSubnetVm -Extra @{
                    NetworkInterfaceName = "$missingSubnetVm-nic"
                    SubnetName = $missingSubnetId
                }
                Add-Failure 'missing subnet: expected failure'
            }
            catch
            {
                if (-not $_.Exception.Message.Contains("Subnet '$missingSubnetId' was not found."))
                {
                    Add-Failure "missing subnet: unexpected error: $($_.Exception.Message)"
                }
            }
            Test-NoNetworkSideEffects -Name $missingSubnetVm
            $vmListUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01"
            $vms = (Invoke-AzRestMethod -Method GET -Uri $vmListUrl).Content | ConvertFrom-Json
            if ($missingSubnetVm -in @($vms.value.name))
            {
                Add-Failure 'missing subnet: VM was unexpectedly created'
            }

            $null = Invoke-AzRestMethod -Method PUT -Uri (Get-NetworkUrl -Type 'networkInterfaces' -Name $secondNicName) -Payload ($nicBody | ConvertTo-Json -Depth 7 -Compress)
            if (-not (Wait-ResourceSucceeded -Label 'existing nic' -Type 'networkInterfaces' -Name $secondNicName -What "NIC '$secondNicName'"))
            {
                throw "NIC '$secondNicName' did not finish provisioning."
            }
            $null = New-NetworkSmokeVM -Name $existingNicVm -Extra @{
                NetworkInterfaceId = @($existingNicId, $secondNicId)
                NetworkInterfaceDeleteOption = 'Detach'
            }
            $vm = Get-SmokeVMResource -Name $existingNicVm
            $references = @($vm.properties.networkProfile.networkInterfaces)
            if ($references.Count -ne 2)
            {
                Add-Failure 'existing nic: expected exactly two NIC references'
            }
            else
            {
                for ($index = 0; $index -lt 2; $index++)
                {
                    $expectedId = @($existingNicId, $secondNicId)[$index]
                    if ($references[$index].id -ne $expectedId -or
                        $references[$index].properties.primary -cne ($index -eq 0) -or
                        $references[$index].properties.deleteOption -ne 'Detach')
                    {
                        Add-Failure "existing nic: incorrect ID, primary flag or delete option at index $index"
                    }
                }
            }
            Test-NoNetworkSideEffects -Name $existingNicVm
        }
        catch
        {
            Add-Failure "existing nic: $($_.Exception.Message)"
        }
    }
    else
    {
        Write-Host '== 8. skipping existing-NIC live create; set AZCLIPS_ENABLE_EXISTING_NIC=1 to enable =='
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
