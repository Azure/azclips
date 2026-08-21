---
external help file: Az.StorageAccount-Help.xml
Module Name: Az.StorageAccount
online version: https://learn.microsoft.com/powershell/module/az.storageaccount/update-azstorageaccount
schema: 2.0.0
---

# Update-AzStorageAccount

## SYNOPSIS

Update an existing AzStorageAccount.

## SYNTAX

```
Update-AzStorageAccount
    -SubscriptionId <string>
    -ResourceGroupName <string>
    -Name <string>
    [-Profile <string?>]
    [-SkuName <string?>]
    [-Tags <Hashtable?>]
    [-CustomDomainUseSubDomainName <bool?>]
    [-EncryptionServicesBlobEnabled <bool?>]
    [-EncryptionServicesBlobKeyType <string?>]
    [-EncryptionServicesFileEnabled <bool?>]
    [-EncryptionServicesFileKeyType <string?>]
    [-EncryptionServicesTableEnabled <bool?>]
    [-EncryptionServicesTableKeyType <string?>]
    [-EncryptionServicesQueueEnabled <bool?>]
    [-EncryptionServicesQueueKeyType <string?>]
    [-EncryptionKeySource <string?>]
    [-EncryptionRequireInfrastructureEncryption <bool?>]
    [-EncryptionKeyVaultPropertiesKeyName <string?>]
    [-EncryptionKeyVaultPropertiesKeyVersion <string?>]
    [-EncryptionKeyVaultPropertiesKeyVaultUri <string?>]
    [-EncryptionEncryptionIdentityEncryptionUserAssignedIdentity <string?>]
    [-EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId <string?>]
    [-SasPolicySasExpirationPeriod <string?>]
    [-SasPolicyExpirationAction <string?>]
    [-KeyPolicyKeyExpirationPeriodInDays <int?>]
    [-AccessTier <string?>]
    [-AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName <string?>]
    [-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType <string?>]
    [-AzureFilesIdentityBasedAuthenticationDefaultSharePermission <string?>]
    [-AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled <bool?>]
    [-EnableHttpsTrafficOnly <bool?>]
    [-IsSftpEnabled <bool?>]
    [-IsLocalUserEnabled <bool?>]
    [-EnableExtendedGroups <bool?>]
    [-NetworkRuleSetBypass <string?>]
    [-NetworkRuleSetDefaultAction <string?>]
    [-LargeFileSharesState <string?>]
    [-RoutingPreferenceRoutingChoice <string?>]
    [-RoutingPreferencePublishMicrosoftEndpoints <bool?>]
    [-RoutingPreferencePublishInternetEndpoints <bool?>]
    [-DualStackEndpointPreferencePublishIpv6Endpoint <bool?>]
    [-AllowBlobPublicAccess <bool?>]
    [-MinimumTlsVersion <string?>]
    [-AllowSharedKeyAccess <bool?>]
    [-AllowCrossTenantReplication <bool?>]
    [-DefaultToOAuthAuthentication <bool?>]
    [-PublicNetworkAccess <string?>]
    [-ImmutableStorageWithVersioningEnabled <bool?>]
    [-ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays <int?>]
    [-ImmutableStorageWithVersioningImmutabilityPolicyState <string?>]
    [-ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites <bool?>]
    [-AllowedCopyScope <string?>]
    [-DnsEndpointType <string?>]
    [-GeoPriorityReplicationStatusIsBlobEnabled <bool?>]
    [-AllowSharedKeyAccessForServicesBlobEnabled <bool?>]
    [-AllowSharedKeyAccessForServicesFileEnabled <bool?>]
    [-AllowSharedKeyAccessForServicesTableEnabled <bool?>]
    [-AllowSharedKeyAccessForServicesQueueEnabled <bool?>]
    [-DataCollaborationPolicyPropertiesAllowStorageConnectors <bool?>]
    [-DataCollaborationPolicyPropertiesAllowStorageDataShares <bool?>]
    [-DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing <bool?>]
    [-Kind <string?>]
    [-Zones <List<string>?>]
    [-PlacementZonePlacementPolicy <string?>]
    [-BodyJsonPatch <string?>]
    [<CommonParameters>]
```

## DESCRIPTION

The **Update-AzStorageAccount** cmdlet applies a PATCH to an existing AzStorageAccount.

## EXAMPLES

### Example 1: Update tags on an existing AzStorageAccount

```powershell
Update-AzStorageAccount `
    -SubscriptionId <sub> `
    -ResourceGroupName <rg> `
    -Name <name> `
    -Tags @{ env = "prod"; owner = "alex" }
```

## PARAMETERS

### -Profile

Use the named profile for this command instead of the active profile.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubscriptionId

Azure subscription ID (GUID) that owns the resource.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceGroupName

Name of the resource group that contains the resource.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

The name of the AzStorageAccount.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkuName

The **-SkuName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tags

Hashtable of tag name/value pairs to assign to the resource.

```yaml
Type: Hashtable?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CustomDomainUseSubDomainName

The **-CustomDomainUseSubDomainName** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesBlobEnabled

The **-EncryptionServicesBlobEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesBlobKeyType

The **-EncryptionServicesBlobKeyType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesFileEnabled

The **-EncryptionServicesFileEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesFileKeyType

The **-EncryptionServicesFileKeyType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesTableEnabled

The **-EncryptionServicesTableEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesTableKeyType

The **-EncryptionServicesTableKeyType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesQueueEnabled

The **-EncryptionServicesQueueEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionServicesQueueKeyType

The **-EncryptionServicesQueueKeyType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionKeySource

The **-EncryptionKeySource** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionRequireInfrastructureEncryption

The **-EncryptionRequireInfrastructureEncryption** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionKeyVaultPropertiesKeyName

The **-EncryptionKeyVaultPropertiesKeyName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionKeyVaultPropertiesKeyVersion

The **-EncryptionKeyVaultPropertiesKeyVersion** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionKeyVaultPropertiesKeyVaultUri

The **-EncryptionKeyVaultPropertiesKeyVaultUri** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionEncryptionIdentityEncryptionUserAssignedIdentity

The **-EncryptionEncryptionIdentityEncryptionUserAssignedIdentity** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId

The **-EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SasPolicySasExpirationPeriod

The **-SasPolicySasExpirationPeriod** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SasPolicyExpirationAction

The **-SasPolicyExpirationAction** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -KeyPolicyKeyExpirationPeriodInDays

The **-KeyPolicyKeyExpirationPeriodInDays** parameter.

```yaml
Type: int?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AccessTier

The **-AccessTier** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions

The **-AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType

The **-AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationDefaultSharePermission

The **-AzureFilesIdentityBasedAuthenticationDefaultSharePermission** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled

The **-AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EnableHttpsTrafficOnly

The **-EnableHttpsTrafficOnly** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IsSftpEnabled

The **-IsSftpEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IsLocalUserEnabled

The **-IsLocalUserEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EnableExtendedGroups

The **-EnableExtendedGroups** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NetworkRuleSetBypass

The **-NetworkRuleSetBypass** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NetworkRuleSetDefaultAction

The **-NetworkRuleSetDefaultAction** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LargeFileSharesState

The **-LargeFileSharesState** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RoutingPreferenceRoutingChoice

The **-RoutingPreferenceRoutingChoice** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RoutingPreferencePublishMicrosoftEndpoints

The **-RoutingPreferencePublishMicrosoftEndpoints** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RoutingPreferencePublishInternetEndpoints

The **-RoutingPreferencePublishInternetEndpoints** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DualStackEndpointPreferencePublishIpv6Endpoint

The **-DualStackEndpointPreferencePublishIpv6Endpoint** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowBlobPublicAccess

The **-AllowBlobPublicAccess** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimumTlsVersion

The **-MinimumTlsVersion** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowSharedKeyAccess

The **-AllowSharedKeyAccess** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowCrossTenantReplication

The **-AllowCrossTenantReplication** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefaultToOAuthAuthentication

The **-DefaultToOAuthAuthentication** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PublicNetworkAccess

The **-PublicNetworkAccess** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImmutableStorageWithVersioningEnabled

The **-ImmutableStorageWithVersioningEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays

The **-ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays** parameter.

```yaml
Type: int?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImmutableStorageWithVersioningImmutabilityPolicyState

The **-ImmutableStorageWithVersioningImmutabilityPolicyState** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites

The **-ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowedCopyScope

The **-AllowedCopyScope** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsEndpointType

The **-DnsEndpointType** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoPriorityReplicationStatusIsBlobEnabled

The **-GeoPriorityReplicationStatusIsBlobEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowSharedKeyAccessForServicesBlobEnabled

The **-AllowSharedKeyAccessForServicesBlobEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowSharedKeyAccessForServicesFileEnabled

The **-AllowSharedKeyAccessForServicesFileEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowSharedKeyAccessForServicesTableEnabled

The **-AllowSharedKeyAccessForServicesTableEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowSharedKeyAccessForServicesQueueEnabled

The **-AllowSharedKeyAccessForServicesQueueEnabled** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DataCollaborationPolicyPropertiesAllowStorageConnectors

The **-DataCollaborationPolicyPropertiesAllowStorageConnectors** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DataCollaborationPolicyPropertiesAllowStorageDataShares

The **-DataCollaborationPolicyPropertiesAllowStorageDataShares** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing

The **-DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing** parameter.

```yaml
Type: bool?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind

The **-Kind** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Zones

The **-Zones** parameter.

```yaml
Type: List<string>?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PlacementZonePlacementPolicy

The **-PlacementZonePlacementPolicy** parameter.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BodyJsonPatch

Raw JSON deep-merged into the request body after flat parameters. Escape hatch for shapes the tsp emitter cannot flatten (e.g. arrays of nested models).

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable, -ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

- [Az.StorageAccount Module](Az.StorageAccount.md)
