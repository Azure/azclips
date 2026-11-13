# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Focused live smoke test for New-AzVM SSH key-only authentication.
#
# The script creates one Ubuntu VM, passes an existing public key file through
# -SshKeyValues, and verifies the ARM VM model has password authentication disabled
# and the expected authorized_keys entry. By default it deletes its generated resource
# group. Use -KeepResources to retain the VM and connect to it manually.
#
# Prereqs:
#   1. dotnet build azclips.slnx
#   2. Import/connect with the AzCLIPS Accounts module, for example:
#        Import-Module ./src/PS/Accounts/Accounts.psd1
#        Connect-AzAccount
#   3. Set AZCLIPS_SUBSCRIPTION_ID and provide -SshPublicKeyPath or set
#      AZCLIPS_SSH_PUBLIC_KEY_PATH.
#
# Example:
#   $env:AZCLIPS_SUBSCRIPTION_ID = '<subscription-id>'
#   ./tests/Scripts/PS/VMCreateSsh.ps1 -SshPublicKeyPath $HOME/.ssh/id_ed25519.pub
#
# Retain the VM for a manual SSH connection:
#   ./tests/Scripts/PS/VMCreateSsh.ps1 `
#       -SshPublicKeyPath $HOME/.ssh/id_ed25519.pub `
#       -SshPrivateKeyPath $HOME/.ssh/id_ed25519 `
#       -KeepResources

[CmdletBinding()]
param(
    [string] $SubscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID,

    [string] $SshPublicKeyPath = $env:AZCLIPS_SSH_PUBLIC_KEY_PATH,

    [string] $SshPrivateKeyPath = $env:AZCLIPS_SSH_PRIVATE_KEY_PATH,

    [string] $Location = 'westus2',

    [string] $VmSize = 'Standard_D2s_v3',

    [string] $Image = 'Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest',

    [string] $ResourceGroupName,

    [switch] $KeepResources
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    throw 'Set AZCLIPS_SUBSCRIPTION_ID or pass -SubscriptionId.'
}
if ([string]::IsNullOrWhiteSpace($SshPublicKeyPath)) {
    throw 'Set AZCLIPS_SSH_PUBLIC_KEY_PATH or pass -SshPublicKeyPath.'
}

$resolvedPublicKeyPath = (Resolve-Path -LiteralPath $SshPublicKeyPath -ErrorAction Stop).Path
$publicKey = (Get-Content -LiteralPath $resolvedPublicKeyPath -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($publicKey)) {
    throw "SSH public key file '$resolvedPublicKeyPath' is empty."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$moduleManifests = @{
    Resources = Join-Path $repoRoot 'src\PS\Resources\Resources.psd1'
    Compute   = Join-Path $repoRoot 'src\PS\Compute\Compute.psd1'
}
foreach ($moduleName in $moduleManifests.Keys) {
    $manifest = $moduleManifests[$moduleName]
    if (-not (Test-Path -LiteralPath $manifest)) {
        throw "Module manifest '$manifest' was not found. Build first with: dotnet build azclips.slnx"
    }
    Import-Module $manifest -Force -ErrorAction Stop
}

$requiredCommands = @{
    'New-AzResourceGroup'    = 'Resources'
    'Remove-AzResourceGroup' = 'Resources'
    'Invoke-AzRestMethod'    = 'Resources'
    'New-AzVM'               = 'Compute'
}
foreach ($commandName in $requiredCommands.Keys) {
    $command = Get-Command $commandName -ErrorAction Stop
    if ($command.ModuleName -ne $requiredCommands[$commandName]) {
        throw "$commandName resolves to '$($command.ModuleName)', not the AzCLIPS '$($requiredCommands[$commandName])' module."
    }
}

$suffix = $PID % 1000000
$ownsResourceGroup = [string]::IsNullOrWhiteSpace($ResourceGroupName)
if ($ownsResourceGroup) {
    $ResourceGroupName = "azclips-vmssh-rg-$suffix"
}
$vmName = "vmssh-$suffix"
$adminUsername = 'azadmin'
$expectedKeyPath = "/home/$adminUsername/.ssh/authorized_keys"
$groupCreated = $false

try {
    Write-Host "== Creating resource group '$ResourceGroupName' in '$Location' =="
    $null = New-AzResourceGroup -SubscriptionId $SubscriptionId -Name $ResourceGroupName -Location $Location
    $groupCreated = $true

    Write-Host "== Creating Linux VM '$vmName' with SSH key-only authentication =="
    $vm = New-AzVM `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $vmName `
        -Location $Location `
        -VmSize $VmSize `
        -Image $Image `
        -SecurityType Standard `
        -AdminUsername $adminUsername `
        -SshKeyValues $resolvedPublicKeyPath `
        -VirtualNetworkName "$vmName-vnet" `
        -AddressPrefixes '10.0.0.0/16' `
        -SubnetNames "$vmName-subnet" `
        -SubnetAddressPrefixes '10.0.2.0/24'

    if ($vm.Properties.ProvisioningState -ne 'Succeeded') {
        throw "VM provisioning state was '$($vm.Properties.ProvisioningState)', not 'Succeeded'."
    }

    $vmUri = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Compute/virtualMachines/${vmName}?api-version=2025-11-01"
    $vmResource = (Invoke-AzRestMethod -Method GET -Uri $vmUri).Content | ConvertFrom-Json
    $linuxConfiguration = $vmResource.properties.osProfile.linuxConfiguration
    if ($null -eq $linuxConfiguration) {
        throw 'The VM ARM model did not contain linuxConfiguration.'
    }
    if ($linuxConfiguration.disablePasswordAuthentication -ne $true) {
        throw 'disablePasswordAuthentication was not true.'
    }

    $sshKeys = @($linuxConfiguration.ssh.publicKeys)
    $matchingKey = @($sshKeys | Where-Object { $_.path -eq $expectedKeyPath -and $_.keyData.Trim() -eq $publicKey })
    if ($matchingKey.Count -ne 1) {
        throw "The VM ARM model did not contain the expected SSH key at '$expectedKeyPath'."
    }

    $nsgName = "${vmName}NSG"
    $nsgUri = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Network/networkSecurityGroups/${nsgName}?api-version=2025-05-01"
    $nsg = (Invoke-AzRestMethod -Method GET -Uri $nsgUri).Content | ConvertFrom-Json
    $sshRules = @($nsg.properties.securityRules | Where-Object {
        $_.properties.direction -eq 'Inbound' -and
        $_.properties.access -eq 'Allow' -and
        $_.properties.destinationPortRange -eq '22'
    })
    if ($sshRules.Count -lt 1) {
        throw "Network security group '$nsgName' did not contain an inbound allow rule for TCP port 22."
    }

    Write-Host 'PASS: VM provisioning succeeded.'
    Write-Host 'PASS: Password authentication is disabled.'
    Write-Host "PASS: The public key file was installed at '$expectedKeyPath'."
    Write-Host "PASS: Network security group '$nsgName' allows inbound SSH on TCP port 22."

    $publicIp = [string]$vm.PublicIps
    if ([string]::IsNullOrWhiteSpace($publicIp)) {
        Write-Warning 'The VM output did not include a public IP address, so an SSH command cannot be printed.'
    }
    else {
        $privateKey = if ([string]::IsNullOrWhiteSpace($SshPrivateKeyPath)) { '<private-key-path>' } else { $SshPrivateKeyPath }
        Write-Host "Public IP: $publicIp"
        Write-Host "SSH command: ssh -i `"$privateKey`" $adminUsername@$publicIp"
    }

    if (-not $KeepResources) {
        Write-Host 'Tip: rerun with -KeepResources to retain the VM for an interactive SSH test.'
    }
}
finally {
    if ($groupCreated -and $ownsResourceGroup -and -not $KeepResources) {
        Write-Host "== Cleanup: deleting resource group '$ResourceGroupName' =="
        try {
            $null = Remove-AzResourceGroup -SubscriptionId $SubscriptionId -Name $ResourceGroupName -Force
        }
        catch {
            Write-Warning "Cleanup failed: $($_.Exception.Message)"
        }
    }
    elseif ($groupCreated) {
        Write-Host "Resources retained in resource group '$ResourceGroupName'."
        Write-Host "Cleanup command: Remove-AzResourceGroup -SubscriptionId '$SubscriptionId' -Name '$ResourceGroupName' -Force"
    }
}
