# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Storage account - PowerShell (New-/Get-/Remove-AzStorageAccount)
# Equivalent of: New-AzStorageAccount, Get-AzStorageAccount, Remove-AzStorageAccount
#
# Shared test context:
#   SubscriptionId: resolved from the active profile (no -SubscriptionId needed)
#   ResourceGroupName: a per-run azclips-storage-account-* group (region: australiaeast),
#                      removed with every account in it when the script ends, even after a
#                      failure or Ctrl+C
#
# Import the modules first. Storage loads Accounts; Resources provides the group cmdlets:
#   Import-Module ./src/PS/Resources/Resources.psd1
#   Import-Module ./src/PS/Storage/Storage.psd1
#
# Account names must be 3-24 lowercase alphanumerics and are GLOBALLY unique, so each
# case below appends a random suffix. The zone case needs a region with availability
# zones; australiasoutheast has none and the service rejects zone pinning there.

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# The negative cases below are EXPECTED to fail, and the script must continue past them.
$ErrorActionPreference = 'Continue'

$Rg = "azclips-storage-account-$(Get-Date -Format 'yyyyMMddHHmmss')-$PID-$(Get-Random)"
$Loc = 'australiaeast'
function New-Suffix { Get-Random -Minimum 10000 -Maximum 99999 }

try
{
    New-AzResourceGroup -Name $Rg -Location $Loc

    # Happy path: the minimum required set. The request body must carry only sku, kind and
    # location so every unspecified parameter keeps its service default.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsmin$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2

    # Scalar parameters and tags. Tri-state switches take an explicit $true or $false; leaving
    # one out keeps the service default rather than sending false.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsopt$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -AccessTier Cool `
        -MinimumTlsVersion TLS1_2 `
        -EnableHttpsTrafficOnly $true `
        -AllowBlobPublicAccess $false `
        -AllowSharedKeyAccess $true `
        -PublicNetworkAccess Enabled `
        -Tag @{ env = 'dev'; owner = 'platform' }

    # Managed identity plus SAS and key policies. The response should include an identity
    # with a PrincipalId, and a SasPolicy whose ExpirationAction defaulted to Log.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsid$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -IdentityType SystemAssigned `
        -SasExpirationPeriod 90.00:00:00 `
        -KeyExpirationPeriodInDay 60

    # Network rules. Bypass is sent as a comma-separated string and each IP rule uses the
    # "value" wire name.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsnet$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -DefaultAction Deny `
        -Bypass Logging,Metrics,AzureServices `
        -IpAddress '13.64.0.0/16','40.90.0.0/16'

    # Account-level immutability with a policy, plus a routing preference. Immutability can
    # only be enabled at creation time.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsalw$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -EnableAccountLevelImmutability `
        -ImmutabilityPeriod 30 `
        -ImmutabilityPolicyState Unlocked `
        -AllowProtectedAppendWrites $true `
        -RoutingChoice MicrosoftRouting `
        -PublishMicrosoftEndpoint $true

    # Hierarchical namespace with SFTP. SFTP requires a hierarchical namespace, so the two
    # are set together.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipshns$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -EnableHierarchicalNamespace $true `
        -EnableSftp $true

    # Service-scoped encryption keys. Requesting a key type for table and queue replaces the
    # default blob services entry in the request.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsenc$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -EncryptionKeyTypeForTable Account `
        -EncryptionKeyTypeForQueue Account `
        -RequireInfrastructureEncryption

    # Azure Files share permission without an authentication enabler. The service requires
    # DirectoryServiceOptions, so the request carries it as None.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsfiles$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -DefaultSharePermission StorageFileDataSmbShareReader

    # Zone pinning. Requires a region with availability zones; expect the service to reject
    # this in a region without them.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipszone$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_GRS `
        -Kind StorageV2 `
        -ZonePlacementPolicy Any `
        -EnableBlobGeoPriorityReplication $true

    # Negative case: invalid -SkuName value. Expected result: the command fails fast listing
    # the allowed SKU names and does NOT create an account.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_BOGUS `
        -Kind StorageV2

    # Negative case: -SasExpirationAction without -SasExpirationPeriod. Both members are
    # required by the service, so this is rejected locally.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -SasExpirationAction Block

    # Negative case: a network rule without -DefaultAction, which the service requires.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -Bypass Logging,Metrics

    # Negative case: a subnet NAME without -VnetName. A name cannot be composed into a
    # resource id without the virtual network it belongs to.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -DefaultAction Deny `
        -Subnet mysubnet

    # Negative case: immutability policy parameters without -EnableAccountLevelImmutability.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -ImmutabilityPolicyState Locked

    # Negative case: two Azure Files authentication enablers. They map onto a single
    # DirectoryServiceOptions value, so at most one may be true.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -EnableAzureActiveDirectoryDomainServicesForFile $true `
        -EnableActiveDirectoryDomainServicesForFile $true

    # Negative case: -EnableActiveDirectoryDomainServicesForFile without the full Active Directory property set.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -EnableActiveDirectoryDomainServicesForFile $true `
        -ActiveDirectoryDomainName contoso.com

    # Negative case: a key vault URI without a key name. Both are required together.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -KeyVaultUri https://myvault.vault.azure.net/

    # Negative case: a tag name containing '='. The cmdlet rejects it before sending, since the
    # name would otherwise be cut short at the '='.
    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name "azclipsbad$(New-Suffix)" `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2 `
        -Tag @{ 'a=b' = 'c' }

    # ── Get / Remove ──
    #
    # These reuse one account so the read cases have something stable to act on, and the
    # Remove at the end doubles as the cleanup this script used to leave to the portal.
    $CrudName = "azclipscrud$(New-Suffix)"

    New-AzStorageAccount `
        -ResourceGroupName $Rg `
        -Name $CrudName `
        -Location $Loc `
        -SkuName Standard_LRS `
        -Kind StorageV2

    # AccountName parameter set: one account.
    Get-AzStorageAccount -ResourceGroupName $Rg -Name $CrudName

    # Geo-replication stats are only returned when asked for; Standard_LRS is not geo-redundant,
    # so expect the expansion to come back empty rather than populated.
    Get-AzStorageAccount -ResourceGroupName $Rg -Name $CrudName -IncludeGeoReplicationStats

    # ResourceGroup parameter set, scoped to the group then to the whole subscription. The
    # second form takes the other API operation, so it is worth running both.
    Get-AzStorageAccount -ResourceGroupName $Rg

    Get-AzStorageAccount

    # The results enumerate, so the pipeline should see individual accounts rather than one array.
    Get-AzStorageAccount -ResourceGroupName $Rg | Select-Object -ExpandProperty Name

    # -WhatIf must not delete anything, even alongside -Force.
    Remove-AzStorageAccount -ResourceGroupName $Rg -Name $CrudName -WhatIf
    Remove-AzStorageAccount -ResourceGroupName $Rg -Name $CrudName -WhatIf -Force

    # High confirm impact means this prompts unless -Force or -Confirm:$false is passed.
    Remove-AzStorageAccount -ResourceGroupName $Rg -Name $CrudName -Force

    # Negative case: get an account that does not exist.
    Get-AzStorageAccount -ResourceGroupName $Rg -Name 'azclipsdoesnotexist'
}
finally
{
    # The group name is unique per run, so removing it can only delete what this run created.
    Remove-AzResourceGroup -Name $Rg -Force -ErrorAction SilentlyContinue | Out-Null
}
