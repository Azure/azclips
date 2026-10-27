# Azclips PowerShell live test template.
# Custom-code integration PowerShell scripts belong under tests/Scripts/PS/<ModuleName>/<CommandName>/<testFileName>.ps1.
# Use a filesystem-safe Azclips cmdlet name for <CommandName>.
# Replace this VM example with verified cmdlets, parameters, output properties, and assertions for the selected behavior.

$ErrorActionPreference = 'Stop'

function GetRequiredEnv {
    param([Parameter(Mandatory)][string] $Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is not set."
    }
    $value
}

function Assert-Equal {
    param(
        [object] $Expected,
        [object] $Actual,
        [Parameter(Mandatory)][string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "FAIL: $Message. Expected '$Expected', got '$Actual'."
    }
}

function Assert-NotNullOrEmpty {
    param(
        [object] $Value,
        [Parameter(Mandatory)][string] $Message
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string] $Value)) {
        throw "FAIL: $Message. Value was null or empty."
    }
}

$SubscriptionId = GetRequiredEnv AZCLIPS_SUBSCRIPTION_ID
$AdminPassword = GetRequiredEnv AZCLIPS_VM_ADMIN_PASSWORD
$RunId = if ($env:AZCLIPS_LIVE_TEST_RUN_ID) { $env:AZCLIPS_LIVE_TEST_RUN_ID } else { [Guid]::NewGuid().ToString('N').Substring(0, 8) }
$NamePrefix = 'azclips-vm'

$ResourceGroupName = "$NamePrefix-rg-$RunId"
$Location = 'eastus'
$VmName = "$NamePrefix-vm-$RunId"
$NetworkInterfaceName = "$NamePrefix-nic-$RunId"
$VirtualNetworkName = "$NamePrefix-vnet-$RunId"
$SubnetName = "$NamePrefix-subnet-$RunId"
$NetworkSecurityGroupName = "$NamePrefix-nsg-$RunId"

$createdVm = New-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName `
    -Location $Location `
    -VmSize Standard_D2s_v3 `
    -AdminUsername azclipsadmin `
    -AdminPassword $AdminPassword `
    -NetworkInterfaceName $NetworkInterfaceName `
    -VirtualNetworkName $VirtualNetworkName `
    -AddressPrefixes "10.0.0.0/16" `
    -SubnetNames $SubnetName `
    -SubnetAddressPrefixes "10.0.2.0/24" `
    -NetworkSecurityGroupName $NetworkSecurityGroupName `
    -RuleNames rdp `
    -RuleProtocols tcp `
    -RuleDirections Inbound `
    -RulePriorities 1001 `
    -RuleSourceAddressPrefixes '*' `
    -RuleSourcePortRanges '*' `
    -RuleDestinationPortRanges 3389 `
    -RuleDestinationAddressPrefixes '*' `
    -RuleAccesses Allow

Assert-Equal $VmName $createdVm.Name 'New-AzVM should return the created VM name'
Assert-Equal $Location $createdVm.Location 'New-AzVM should return the requested location'
Assert-NotNullOrEmpty $createdVm.Id 'New-AzVM should return a resource id'

$shownVm = Get-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName

Assert-Equal $VmName $shownVm.Name 'Get-AzVM should return the created VM name'
Assert-NotNullOrEmpty $shownVm.Properties.NetworkProfile.NetworkInterfaces[0].Id 'Get-AzVM should expose the NIC reference'

Remove-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName `
    -Force

$deleted = $false
try {
    Get-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $VmName | Out-Null
} catch {
    $deleted = $true
}

if (-not $deleted) {
    throw 'FAIL: Get-AzVM should fail after Remove-AzVM.'
}

Write-Host 'All assertions passed.'