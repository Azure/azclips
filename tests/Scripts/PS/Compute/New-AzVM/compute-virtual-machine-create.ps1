# Live test for selected compute-virtual-machine-create shared PowerShell behavior.
# Prereqs: build Azclips, sign in, and set AZCLIPS_SUBSCRIPTION_ID.

$ErrorActionPreference = 'Stop'

function Get-RequiredEnv {
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

function Assert-True {
    param(
        [bool] $Condition,
        [Parameter(Mandatory)][string] $Message
    )

    if (-not $Condition) {
        throw "FAIL: $Message"
    }
}

function Get-RestJson {
    param([Parameter(Mandatory)] $Response)

    Assert-Equal 200 $Response.StatusCode 'REST GET should return HTTP 200'
    $Response.Content | ConvertFrom-Json
}

function Assert-Absent {
    param(
        [object] $Value,
        [Parameter(Mandatory)][string] $Message
    )

    if ($null -ne $Value) {
        throw "FAIL: $Message"
    }
}

$ScriptPath = Split-Path -Parent $PSCommandPath
$RepoRoot = Resolve-Path (Join-Path $ScriptPath '../../../../..')
Import-Module (Join-Path $RepoRoot 'artifacts/Debug/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $RepoRoot 'artifacts/Debug/PS/Resources/Resources.psd1') -Force
Import-Module (Join-Path $RepoRoot 'artifacts/Debug/PS/Compute/Compute.psd1') -Force

$SubscriptionId = Get-RequiredEnv AZCLIPS_SUBSCRIPTION_ID
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'eastus' }
$RunIdRaw = if ($env:AZCLIPS_LIVE_TEST_RUN_ID) { $env:AZCLIPS_LIVE_TEST_RUN_ID } else { [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString() }
$RunId = ($RunIdRaw.ToLowerInvariant() -replace '[^a-z0-9]', '')
if ($RunId.Length -gt 8) { $RunId = $RunId.Substring(0, 8) }
if ([string]::IsNullOrWhiteSpace($RunId)) { $RunId = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString().Substring(0, 8) }

$ResourceGroupName = "azclips-vm-create-ps-$RunId"
$AdminUsername = 'azclipstest'
$AdminPassword = "Azc$RunId!Pw9x"
$StorageAccountName = "azcdiagps$RunId"
$BootDiagnosticsUri = "https://$StorageAccountName.blob.core.windows.net/"
$IdentityName = "azc$($RunId)id"
$IdentityId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IdentityName"

$TrustedLaunchVmName = "azc$($RunId)a"
$TrustedLaunchNicId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkInterfaces/$TrustedLaunchVmName-nic"
$TrustedLaunchNsgId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkSecurityGroups/$TrustedLaunchVmName-nsg"
$PublicIpName = "azc$($RunId)pip"
$PublicIpId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/publicIPAddresses/$PublicIpName"
$DnsLabel = $PublicIpName

$StandardVmName = "azc$($RunId)b"
$StandardNicId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkInterfaces/$StandardVmName-nic"

try {
    New-AzResourceGroup `
        -SubscriptionId $SubscriptionId `
        -Name $ResourceGroupName `
        -Location $Location | Out-Null

    $storageBody = @{
        sku = @{ name = 'Standard_LRS' }
        kind = 'StorageV2'
        location = $Location
        properties = @{
            minimumTlsVersion = 'TLS1_2'
            allowBlobPublicAccess = $false
        }
    } | ConvertTo-Json -Depth 10 -Compress

    $storageResponse = Invoke-AzRestMethod `
        -Method PUT `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/${StorageAccountName}?api-version=2023-01-01" `
        -Payload $storageBody
    Assert-True (@(200, 201, 202) -contains $storageResponse.StatusCode) 'storage account setup should be accepted for boot diagnostics'

    $identityBody = @{
        location = $Location
    } | ConvertTo-Json -Depth 5 -Compress
    $identityResponse = Invoke-AzRestMethod `
        -Method PUT `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.ManagedIdentity/userAssignedIdentities/${IdentityName}?api-version=2024-11-30" `
        -Payload $identityBody
    Assert-True (@(200, 201) -contains $identityResponse.StatusCode) 'user-assigned identity setup should be accepted'

    $createdVm = New-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $TrustedLaunchVmName `
        -Location $Location `
        -VmSize Standard_D2s_v3 `
        -AdminUsername $AdminUsername `
        -AdminPassword $AdminPassword `
        -PublicIPAddressName $PublicIpName `
        -PublicIPAddressDomainNameLabel $DnsLabel `
        -BootDiagnosticsEnabled $true `
        -BootDiagnosticsStorageUri $BootDiagnosticsUri `
        -EnableAcceleratedNetworking $true `
        -SystemAssignedIdentity $true `
        -UserAssignedIdentityIds $IdentityId

    Assert-Equal $TrustedLaunchVmName $createdVm.Name 'New-AzVM should return the created TrustedLaunch VM name'
    Assert-NotNullOrEmpty $createdVm.Id 'New-AzVM should return the created TrustedLaunch VM id'

    $vmResponse = Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Compute/virtualMachines/${TrustedLaunchVmName}?api-version=2025-11-01"
    $vm = Get-RestJson $vmResponse
    Assert-Equal 'MicrosoftWindowsServer' $vm.properties.storageProfile.imageReference.publisher 'default image publisher should be applied'
    Assert-Equal 'WindowsServer' $vm.properties.storageProfile.imageReference.offer 'default image offer should be applied'
    Assert-Equal '2022-datacenter-azure-edition' $vm.properties.storageProfile.imageReference.sku 'default image SKU should be applied'
    Assert-Equal 'latest' $vm.properties.storageProfile.imageReference.version 'default image version should be applied'
    Assert-Equal 'TrustedLaunch' $vm.properties.securityProfile.securityType 'default security type should be TrustedLaunch'
    Assert-Equal $true $vm.properties.securityProfile.uefiSettings.secureBootEnabled 'secure boot should be enabled by default'
    Assert-Equal $true $vm.properties.securityProfile.uefiSettings.vTpmEnabled 'vTPM should be enabled by default'
    Assert-Equal $true $vm.properties.diagnosticsProfile.bootDiagnostics.enabled 'boot diagnostics should be enabled when requested'
    Assert-Equal $BootDiagnosticsUri $vm.properties.diagnosticsProfile.bootDiagnostics.storageUri 'boot diagnostics should use the requested storage URI'
    Assert-Equal $TrustedLaunchNicId.ToLowerInvariant() ([string] $vm.properties.networkProfile.networkInterfaces[0].id).ToLowerInvariant() 'VM should reference the derived NIC id'
    Assert-True (([string] $vm.identity.type).ToLowerInvariant().Contains('systemassigned')) 'VM identity type should include SystemAssigned'
    Assert-True (([string] $vm.identity.type).ToLowerInvariant().Contains('userassigned')) 'VM identity type should include UserAssigned'
    Assert-True ($vm.identity.userAssignedIdentities.PSObject.Properties.Name -contains $IdentityId) 'VM identity should include the requested user-assigned identity id'

    $nicResponse = Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkInterfaces/$TrustedLaunchVmName-nic?api-version=2025-05-01"
    $nic = Get-RestJson $nicResponse
    Assert-Equal $TrustedLaunchNsgId.ToLowerInvariant() ([string] $nic.properties.networkSecurityGroup.id).ToLowerInvariant() 'derived NIC should reference the derived NSG'
    Assert-Equal $PublicIpId.ToLowerInvariant() ([string] $nic.properties.ipConfigurations[0].properties.publicIPAddress.id).ToLowerInvariant() 'derived NIC should reference the requested public IP'
    Assert-Equal $true $nic.properties.enableAcceleratedNetworking 'NIC should enable accelerated networking when requested'

    $publicIpResponse = Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/publicIPAddresses/${PublicIpName}?api-version=2025-05-01"
    $publicIp = Get-RestJson $publicIpResponse
    Assert-Equal 'Standard' $publicIp.sku.name 'orchestrated public IP should default to Standard SKU'
    Assert-Equal 'Static' $publicIp.properties.publicIPAllocationMethod 'orchestrated public IP should default to static allocation'
    Assert-Equal $DnsLabel $publicIp.properties.dnsSettings.domainNameLabel 'orchestrated public IP should use the requested DNS label'

    $extensionMissing = $false
    try {
        Invoke-AzRestMethod `
            -Method GET `
            -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Compute/virtualMachines/$TrustedLaunchVmName/extensions/GuestAttestation?api-version=2025-11-01" | Out-Null
    } catch {
        $extensionMissing = $true
    }
    Assert-True $extensionMissing 'PowerShell New-AzVM should not create the CLI-only GuestAttestation extension'

    $standardCreatedVm = New-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $StandardVmName `
        -Location $Location `
        -VmSize Standard_D2s_v3 `
        -AdminUsername $AdminUsername `
        -AdminPassword $AdminPassword `
        -SecurityType Standard `
        -NoPublicIPAddress $true `
        -DisableAcceleratedNetworking $true `
        -DisableBootDiagnostics $true
    Assert-Equal $StandardVmName $standardCreatedVm.Name 'New-AzVM should return the created Standard VM name'

    $standardVmResponse = Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Compute/virtualMachines/${StandardVmName}?api-version=2025-11-01"
    $standardVm = Get-RestJson $standardVmResponse
    Assert-Equal 'MicrosoftWindowsServer' $standardVm.properties.storageProfile.imageReference.publisher 'Standard security should preserve default image publisher'
    Assert-Equal 'Standard' $standardVm.properties.securityProfile.securityType 'Standard security should be effective'
    Assert-Equal $false $standardVm.properties.diagnosticsProfile.bootDiagnostics.enabled 'disable boot diagnostics should set boot diagnostics false'
    Assert-Equal $StandardNicId.ToLowerInvariant() ([string] $standardVm.properties.networkProfile.networkInterfaces[0].id).ToLowerInvariant() 'Standard VM should reference its derived NIC id'

    $standardNicResponse = Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkInterfaces/$StandardVmName-nic?api-version=2025-05-01"
    $standardNic = Get-RestJson $standardNicResponse
    Assert-Absent $standardNic.properties.ipConfigurations[0].properties.publicIPAddress 'no-public-IP should omit the NIC public IP reference'
    Assert-Equal $false $standardNic.properties.enableAcceleratedNetworking 'disable accelerated networking should set the NIC property false'

    Write-Host 'All compute-virtual-machine-create PowerShell live-test assertions passed.'
} finally {
    if ($env:AZCLIPS_KEEP_RESOURCES -ne '1') {
        try {
            Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "/subscriptions/$SubscriptionId/resourcegroups/${ResourceGroupName}?api-version=2025-04-01" | Out-Null
        } catch {
            Write-Warning "Resource group cleanup request failed: $($_.Exception.Message)"
        }
    }
}