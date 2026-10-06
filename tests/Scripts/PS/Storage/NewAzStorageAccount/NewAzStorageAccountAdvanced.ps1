# Storage account advanced create live test for the Azclips PowerShell surface.
# Exercises the shared Storage account create request-shaping behavior selected in storage-account-create.
#
# Upstream evidence from remote main:
#   Azure PowerShell: azure-powershell/src/Storage/Storage.Management.Test/ScenarioTests/StorageAccountTests.ps1
#     - Test-SetAzureRmStorageAccountKeySource
#     - Test-NewAzureStorageAccountQueueTableEncrytionKeyType
#     - Test-NewSetAzStorageAccountFileAADKERB
#     - Test-StorageAccountEnableSmbOauth
#     - Test-NetworkRule
#     - Test-NewSetAzStorageAccount_RoutingPreference
#     - Test-AzureStorageAccountKeySASPolicy
#     - Test-NewSetAzureStorageAccountAllowSharedKeyAccess
#     - Test-AzureStorageAccountWorm
#     - Test-NewSetAzureStorageAccount_LargeFileShare
#   Azure CLI: azure-cli/src/azure-cli/azure/cli/command_modules/storage/tests/latest/test_storage_account_scenarios.py
#     - test_create_storage_account_with_assigned_identity
#     - test_storage_create_with_encryption_key_type
#     - test_create_storage_account_with_double_encryption
#     - test_create_storage_account_with_files_aadkerb
#     - test_storage_account_smb_oauth
#     - test_storage_account_service_endpoints
#     - test_storage_account_with_routing_preference
#     - test_storage_account_allowed_copy_scope
#     - test_storage_account_sas_expiration_policy
#     - test_storage_account_with_alw
#     - test_create_storage_account
#
# Prereqs:
#   1. Build and stage modules: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID                         - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP                    - resource group name; defaults to timestamped test group
#   AZCLIPS_LOCATION                          - Azure region; defaults to eastus
#   AZCLIPS_STORAGE_ACCOUNT_PREFIX            - lowercase account-name prefix, 3-20 chars; defaults to timestamped value
#   AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID   - optional subnet resource ID to include in network ACL creation
#   AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE      - optional tenantId=...;resourceId=... network ACL resource access rule
#   AZCLIPS_STORAGE_CMK_KEY_NAME              - optional Key Vault key name for CMK create coverage
#   AZCLIPS_STORAGE_CMK_KEY_VAULT_URI         - optional Key Vault URI for CMK create coverage
#   AZCLIPS_STORAGE_CMK_KEY_VERSION           - optional Key Vault key version for CMK create coverage
#   AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID - optional user-assigned identity resource ID for CMK coverage
#   AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID   - optional federated identity client ID for CMK coverage
#   AZCLIPS_KEEP_STORAGE                      - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG                        - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\Storage\NewAzStorageAccount\NewAzStorageAccountAdvanced.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Storage\Storage.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'eastus' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyMMddHHmmss'
$suffix = Get-Random -Minimum 10 -Maximum 99
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-storage-create-it-$stamp-$suffix" } else { $env:AZCLIPS_RESOURCE_GROUP }
$accountPrefix = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_ACCOUNT_PREFIX)) { "azcsa$stamp$suffix" } else { $env:AZCLIPS_STORAGE_ACCOUNT_PREFIX }
$skuName = 'Standard_LRS'
$kind = 'StorageV2'
$sasExpirationPeriodText = '1.02:03:04'
$createdAccounts = [System.Collections.Generic.List[string]]::new()

if ($accountPrefix -notmatch '^[a-z0-9]{3,20}$') {
    throw 'AZCLIPS_STORAGE_ACCOUNT_PREFIX must be 3-20 lowercase alphanumeric characters.'
}

$identityAccount = "${accountPrefix}id"
$filesAccount = "${accountPrefix}fl"
$networkAccount = "${accountPrefix}nw"
$policyAccount = "${accountPrefix}pl"
$immutableAccount = "${accountPrefix}im"
$largeFileAccount = "${accountPrefix}lf"
$cmkAccount = "${accountPrefix}cm"

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Normalize-Location($value) {
    if ($null -eq $value) {
        return ''
    }

    return $value.ToString().ToLowerInvariant().Replace(' ', '')
}

function Register-StorageAccount($accountName) {
    $createdAccounts.Add($accountName) | Out-Null
}

function Assert-Equal($expected, $actual, $message) {
    if ($expected -ne $actual) {
        throw "ASSERTION FAILED: $message. Expected '$expected', got '$actual'."
    }
}

function Assert-NotNull($value, $message) {
    if ($null -eq $value) {
        throw "ASSERTION FAILED: $message."
    }
}

function Assert-StorageAccountMatches($account, $expectedName, $message) {
    Assert-NotNull $account "$message returned no storage account"
    Assert-Equal $expectedName $account.Name "$message name"
    Assert-Equal (Normalize-Location $location) (Normalize-Location $account.Location) "$message location"
    Assert-Equal $skuName $account.Sku.Name "$message SKU"
    Assert-Equal $kind $account.Kind "$message kind"
}

function Convert-StorageProperties($account) {
    Assert-NotNull $account.Properties 'Storage account properties were not returned'
    if ($account.Properties -is [System.Text.Json.JsonElement] -or $null -ne $account.Properties.PSObject.Methods['GetRawText']) {
        return $account.Properties.GetRawText() | ConvertFrom-Json -Depth 64
    }

    return $account.Properties
}

function Convert-JsonElementValue($value) {
    if ($null -eq $value) {
        return $null
    }

    if ($value -is [System.Text.Json.JsonElement] -or $null -ne $value.PSObject.Methods['GetRawText']) {
        return $value.GetRawText() | ConvertFrom-Json -Depth 64
    }

    return $value
}

function Get-AdditionalPropertyObject($account, [string] $propertyName) {
    if ($null -eq $account.AdditionalProperties -or -not $account.AdditionalProperties.ContainsKey($propertyName)) {
        return $null
    }

    return Convert-JsonElementValue $account.AdditionalProperties[$propertyName]
}

function Get-ObjectPathValue($object, [string] $path) {
    $current = $object
    foreach ($part in $path.Split('.')) {
        if ([string]::IsNullOrWhiteSpace($part)) {
            continue
        }

        if ($null -eq $current) {
            return $null
        }

        if ($part -match '^\d+$') {
            if ($current -is [System.Collections.IList]) {
                $current = $current[[int]$part]
                continue
            }

            return $null
        }

        $property = $current.PSObject.Properties[$part]
        if ($null -eq $property) {
            return $null
        }

        $current = $property.Value
    }

    return $current
}

function Assert-PropertyEqual($properties, [string] $path, $expected) {
    $actual = Get-ObjectPathValue $properties $path
    Assert-Equal $expected $actual "properties.$path"
}

function Assert-PropertyNotEmpty($properties, [string] $path) {
    $actual = Get-ObjectPathValue $properties $path
    if ($null -eq $actual -or [string]::IsNullOrWhiteSpace($actual.ToString())) {
        throw "ASSERTION FAILED: expected properties.$path to be populated."
    }
}

function Assert-PropertyCsvContains($properties, [string] $path, [string] $expected) {
    $actual = Get-ObjectPathValue $properties $path
    $parts = @($actual.ToString().Split(',') | ForEach-Object { $_.Trim() })
    if ($parts -notcontains $expected) {
        throw "ASSERTION FAILED: expected properties.$path value '$actual' to contain '$expected'."
    }
}

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "New-AzResourceGroup $resourceGroup"
    New-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $resourceGroup `
        -Location $location | Out-Host

    Section 'New-AzStorageAccount identity and encryption'
    Register-StorageAccount $identityAccount
    $identityOutput = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityAccount `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind `
        -AssignIdentity `
        -EncryptionService Blob, File `
        -EncryptionKeyTypeForTable Account `
        -EncryptionKeyTypeForQueue Account `
        -RequireInfrastructureEncryption
    Assert-StorageAccountMatches $identityOutput $identityAccount 'identity create'
    $identityTopLevel = Get-AdditionalPropertyObject $identityOutput 'identity'
    Assert-Equal 'SystemAssigned' $identityTopLevel.type 'identity create identity type'
    Assert-NotNull $identityTopLevel.principalId 'identity create principal id'
    $identityProperties = Convert-StorageProperties $identityOutput
    Assert-PropertyEqual $identityProperties 'encryption.services.blob.enabled' $true
    Assert-PropertyEqual $identityProperties 'encryption.services.file.enabled' $true
    Assert-PropertyEqual $identityProperties 'encryption.services.table.keyType' 'Account'
    Assert-PropertyEqual $identityProperties 'encryption.services.queue.keyType' 'Account'
    Assert-PropertyEqual $identityProperties 'encryption.requireInfrastructureEncryption' $true

    Section 'New-AzStorageAccount Azure Files identity authentication'
    Register-StorageAccount $filesAccount
    $filesOutput = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $filesAccount `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind `
        -EnableAzureActiveDirectoryKerberosForFile $true `
        -ActiveDirectoryDomainName mydomain.com `
        -ActiveDirectoryDomainGuid 12345678-1234-1234-1234-123456789012 `
        -DefaultSharePermission StorageFileDataSmbShareReader `
        -EnableSmbOAuth $true
    Assert-StorageAccountMatches $filesOutput $filesAccount 'files auth create'
    $filesProperties = Convert-StorageProperties $filesOutput
    Assert-PropertyEqual $filesProperties 'azureFilesIdentityBasedAuthentication.directoryServiceOptions' 'AADKERB'
    Assert-PropertyEqual $filesProperties 'azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainName' 'mydomain.com'
    Assert-PropertyEqual $filesProperties 'azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainGuid' '12345678-1234-1234-1234-123456789012'
    Assert-PropertyEqual $filesProperties 'azureFilesIdentityBasedAuthentication.defaultSharePermission' 'StorageFileDataSmbShareReader'
    Assert-PropertyEqual $filesProperties 'azureFilesIdentityBasedAuthentication.smbOAuthSettings.isSmbOAuthEnabled' $true

    Section 'New-AzStorageAccount network, routing, and copy scope'
    Register-StorageAccount $networkAccount
    $networkParameters = @{
        SubscriptionId = $subscriptionId
        ResourceGroupName = $resourceGroup
        Name = $networkAccount
        Location = $location
        SkuName = $skuName
        Kind = $kind
        NetworkBypass = @('Logging', 'Metrics', 'AzureServices')
        DefaultAction = 'Deny'
        IpRule = @('20.11.0.0/16')
        NetworkRuleAction = 'Allow'
        RoutingChoice = 'MicrosoftRouting'
        PublishMicrosoftEndpoint = $true
        PublishInternetEndpoint = $false
        AllowedCopyScope = 'PrivateLink'
    }
    if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID)) {
        $networkParameters.VirtualNetworkRule = @($env:AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE)) {
        $networkParameters.ResourceAccessRule = @($env:AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE)
    }
    $networkOutput = New-AzStorageAccount @networkParameters
    Assert-StorageAccountMatches $networkOutput $networkAccount 'network create'
    $networkProperties = Convert-StorageProperties $networkOutput
    Assert-PropertyCsvContains $networkProperties 'networkAcls.bypass' 'Logging'
    Assert-PropertyCsvContains $networkProperties 'networkAcls.bypass' 'Metrics'
    Assert-PropertyCsvContains $networkProperties 'networkAcls.bypass' 'AzureServices'
    Assert-PropertyEqual $networkProperties 'networkAcls.defaultAction' 'Deny'
    Assert-PropertyEqual $networkProperties 'networkAcls.ipRules.0.value' '20.11.0.0/16'
    Assert-PropertyEqual $networkProperties 'networkAcls.ipRules.0.action' 'Allow'
    Assert-PropertyEqual $networkProperties 'routingPreference.routingChoice' 'MicrosoftRouting'
    Assert-PropertyEqual $networkProperties 'routingPreference.publishMicrosoftEndpoints' $true
    Assert-PropertyEqual $networkProperties 'routingPreference.publishInternetEndpoints' $false
    Assert-PropertyEqual $networkProperties 'allowedCopyScope' 'PrivateLink'
    if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID)) {
        Assert-PropertyEqual $networkProperties 'networkAcls.virtualNetworkRules.0.id' $env:AZCLIPS_STORAGE_VIRTUAL_NETWORK_RULE_ID
    }
    if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_RESOURCE_ACCESS_RULE)) {
        Assert-PropertyNotEmpty $networkProperties 'networkAcls.resourceAccessRules.0.resourceId'
    }

    Section 'New-AzStorageAccount key, SAS, and cross-tenant policies'
    Register-StorageAccount $policyAccount
    $policyOutput = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $policyAccount `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind `
        -AllowSharedKeyAccess $false `
        -SasExpirationPeriod ([TimeSpan]::Parse($sasExpirationPeriodText)) `
        -KeyExpirationPeriodInDay 5 `
        -AllowCrossTenantReplication $false
    Assert-StorageAccountMatches $policyOutput $policyAccount 'policy create'
    $policyProperties = Convert-StorageProperties $policyOutput
    Assert-PropertyEqual $policyProperties 'allowSharedKeyAccess' $false
    Assert-PropertyEqual $policyProperties 'sasPolicy.sasExpirationPeriod' $sasExpirationPeriodText
    Assert-PropertyEqual $policyProperties 'sasPolicy.expirationAction' 'Log'
    Assert-PropertyEqual $policyProperties 'keyPolicy.keyExpirationPeriodInDays' 5
    Assert-PropertyEqual $policyProperties 'allowCrossTenantReplication' $false

    Section 'New-AzStorageAccount account-level immutability'
    Register-StorageAccount $immutableAccount
    $immutableOutput = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $immutableAccount `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind `
        -EnableAccountLevelImmutability `
        -ImmutabilityPeriod 1 `
        -ImmutabilityPolicyState Disabled `
        -AllowProtectedAppendWrites $true
    Assert-StorageAccountMatches $immutableOutput $immutableAccount 'immutability create'
    $immutableProperties = Convert-StorageProperties $immutableOutput
    Assert-PropertyEqual $immutableProperties 'immutableStorageWithVersioning.enabled' $true
    Assert-PropertyEqual $immutableProperties 'immutableStorageWithVersioning.immutabilityPolicy.immutabilityPeriodSinceCreationInDays' 1
    Assert-PropertyEqual $immutableProperties 'immutableStorageWithVersioning.immutabilityPolicy.state' 'Disabled'
    Assert-PropertyEqual $immutableProperties 'immutableStorageWithVersioning.immutabilityPolicy.allowProtectedAppendWrites' $true

    Section 'New-AzStorageAccount large file shares'
    Register-StorageAccount $largeFileAccount
    $largeFileOutput = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $largeFileAccount `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind `
        -EnableLargeFileShare
    Assert-StorageAccountMatches $largeFileOutput $largeFileAccount 'large file share create'
    $largeFileProperties = Convert-StorageProperties $largeFileOutput
    Assert-PropertyEqual $largeFileProperties 'largeFileSharesState' 'Enabled'

    if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_KEY_NAME) -and -not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_KEY_VAULT_URI)) {
        Section 'New-AzStorageAccount customer-managed key'
        Register-StorageAccount $cmkAccount
        $cmkParameters = @{
            SubscriptionId = $subscriptionId
            ResourceGroupName = $resourceGroup
            Name = $cmkAccount
            Location = $location
            SkuName = $skuName
            Kind = $kind
            EncryptionKeySource = 'Microsoft.Keyvault'
            KeyName = $env:AZCLIPS_STORAGE_CMK_KEY_NAME
            KeyVaultUri = $env:AZCLIPS_STORAGE_CMK_KEY_VAULT_URI
        }
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_KEY_VERSION)) {
            $cmkParameters.KeyVersion = $env:AZCLIPS_STORAGE_CMK_KEY_VERSION
        }
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID)) {
            $cmkParameters.IdentityType = 'UserAssigned'
            $cmkParameters.UserAssignedIdentityId = @($env:AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID)
            $cmkParameters.KeyVaultUserAssignedIdentityId = $env:AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID
        }
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID)) {
            $cmkParameters.KeyVaultFederatedClientId = $env:AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID
        }
        $cmkOutput = New-AzStorageAccount @cmkParameters
        Assert-StorageAccountMatches $cmkOutput $cmkAccount 'CMK create'
        $cmkProperties = Convert-StorageProperties $cmkOutput
        Assert-PropertyEqual $cmkProperties 'encryption.keySource' 'Microsoft.Keyvault'
        Assert-PropertyEqual $cmkProperties 'encryption.keyVaultProperties.keyName' $env:AZCLIPS_STORAGE_CMK_KEY_NAME
        Assert-PropertyNotEmpty $cmkProperties 'encryption.keyVaultProperties.keyVaultUri'
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_KEY_VERSION)) {
            Assert-PropertyEqual $cmkProperties 'encryption.keyVaultProperties.keyVersion' $env:AZCLIPS_STORAGE_CMK_KEY_VERSION
        }
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID)) {
            $cmkIdentityTopLevel = Get-AdditionalPropertyObject $cmkOutput 'identity'
            Assert-Equal 'UserAssigned' $cmkIdentityTopLevel.type 'CMK create identity type'
            Assert-PropertyEqual $cmkProperties 'encryption.encryptionIdentity.encryptionUserAssignedIdentity' $env:AZCLIPS_STORAGE_CMK_USER_ASSIGNED_IDENTITY_ID
        }
        if (-not [string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID)) {
            Assert-PropertyEqual $cmkProperties 'encryption.encryptionIdentity.encryptionFederatedIdentityClientId' $env:AZCLIPS_STORAGE_CMK_FEDERATED_CLIENT_ID
        }
    }
    else {
        Write-Host 'Skipping customer-managed key create coverage; set AZCLIPS_STORAGE_CMK_KEY_NAME and AZCLIPS_STORAGE_CMK_KEY_VAULT_URI to enable it.'
    }

    Write-Host ''
    Write-Host 'All advanced storage account create PowerShell checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_STORAGE -ne '1') {
        foreach ($accountName in $createdAccounts) {
            Section "Remove-AzStorageAccount cleanup $accountName"
            try {
                Remove-AzStorageAccount `
                    -SubscriptionId $subscriptionId `
                    -ResourceGroupName $resourceGroup `
                    -Name $accountName `
                    -Confirm:$false `
                    -ErrorAction Stop
            }
            catch {
                Write-Warning "Storage account cleanup failed for '$accountName': $($_.Exception.Message)"
            }
        }
    }

    if ($env:AZCLIPS_CLEANUP_RG -eq '1') {
        Section 'Invoke-AzRestMethod cleanup resource group'
        try {
            Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "/subscriptions/$subscriptionId/resourcegroups/$resourceGroup?api-version=2025-04-01" `
                -ErrorAction Stop | Out-Host
        }
        catch {
            Write-Warning "Resource group cleanup failed: $($_.Exception.Message)"
        }
    }
}