// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated dispatch tables for the StorageAccountTsp extension.
// Hand-curated subset of StorageTsp: `az storage account` CRUD only
// (create/show/update/delete/list/list-by-subscription/wait), trimmed
// from the full StorageTsp codegen output (which also covers blob/queue/
// table/file services and other Storage sub-resources).
//
//   Provider:    Microsoft.Storage
//   API version: 2026-04-01
//   Operations:  7 (account CRUD + wait) of 121 total across 27 resource type(s)
//   Source:      TypeSpec (specification/storage/Storage.Management)

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp
{
    [ExtensionVerb("storage")]
    public partial class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        // ── PS Command Metadata (StorageAccount CRUD only) ──
        private static readonly CommandMetadata s_GetStorageAccountProcessor_PS = new(
            Name: "GetStorageAccountProcessor",
            InvocationName: "Get-AzStorageAccount_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.GetStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.GetStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The accountName..", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Expand"] = new("Expand", "Expand", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_NewStorageAccountProcessor_PS = new(
            Name: "NewStorageAccountProcessor",
            InvocationName: "New-AzStorageAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.NewStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.NewStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The accountName..", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SkuName"] = new("SkuName", "SkuName", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Kind"] = new("Kind", "Kind", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Zones"] = new("Zones", "Zones", typeof(List<string>), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PlacementZonePlacementPolicy"] = new("PlacementZonePlacementPolicy", "PlacementZonePlacementPolicy", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tags"] = new("Tags", "Tags", typeof(object), "Resource tags.", IsRequired: false, Aliases: ["Tag"], AllowedValues: []),
                ["AllowedCopyScope"] = new("AllowedCopyScope", "AllowedCopyScope", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicNetworkAccess"] = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasPolicySasExpirationPeriod"] = new("SasPolicySasExpirationPeriod", "SasPolicySasExpirationPeriod", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasPolicyExpirationAction"] = new("SasPolicyExpirationAction", "SasPolicyExpirationAction", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["KeyPolicyKeyExpirationPeriodInDays"] = new("KeyPolicyKeyExpirationPeriodInDays", "KeyPolicyKeyExpirationPeriodInDays", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["CustomDomainUseSubDomainName"] = new("CustomDomainUseSubDomainName", "CustomDomainUseSubDomainName", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesBlobEnabled"] = new("EncryptionServicesBlobEnabled", "EncryptionServicesBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesBlobKeyType"] = new("EncryptionServicesBlobKeyType", "EncryptionServicesBlobKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesFileEnabled"] = new("EncryptionServicesFileEnabled", "EncryptionServicesFileEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesFileKeyType"] = new("EncryptionServicesFileKeyType", "EncryptionServicesFileKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesTableEnabled"] = new("EncryptionServicesTableEnabled", "EncryptionServicesTableEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesTableKeyType"] = new("EncryptionServicesTableKeyType", "EncryptionServicesTableKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesQueueEnabled"] = new("EncryptionServicesQueueEnabled", "EncryptionServicesQueueEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesQueueKeyType"] = new("EncryptionServicesQueueKeyType", "EncryptionServicesQueueKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeySource"] = new("EncryptionKeySource", "EncryptionKeySource", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionRequireInfrastructureEncryption"] = new("EncryptionRequireInfrastructureEncryption", "EncryptionRequireInfrastructureEncryption", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyName"] = new("EncryptionKeyVaultPropertiesKeyName", "EncryptionKeyVaultPropertiesKeyName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyVersion"] = new("EncryptionKeyVaultPropertiesKeyVersion", "EncryptionKeyVaultPropertiesKeyVersion", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyVaultUri"] = new("EncryptionKeyVaultPropertiesKeyVaultUri", "EncryptionKeyVaultPropertiesKeyVaultUri", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEncryptionIdentityEncryptionUserAssignedIdentity"] = new("EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", "EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId"] = new("EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", "EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkRuleSetBypass"] = new("NetworkRuleSetBypass", "NetworkRuleSetBypass", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkRuleSetDefaultAction"] = new("NetworkRuleSetDefaultAction", "NetworkRuleSetDefaultAction", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AccessTier"] = new("AccessTier", "AccessTier", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions"] = new("AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", "AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationDefaultSharePermission"] = new("AzureFilesIdentityBasedAuthenticationDefaultSharePermission", "AzureFilesIdentityBasedAuthenticationDefaultSharePermission", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled"] = new("AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", "AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableHttpsTrafficOnly"] = new("EnableHttpsTrafficOnly", "EnableHttpsTrafficOnly", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsSftpEnabled"] = new("IsSftpEnabled", "IsSftpEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsLocalUserEnabled"] = new("IsLocalUserEnabled", "IsLocalUserEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableExtendedGroups"] = new("EnableExtendedGroups", "EnableExtendedGroups", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsHnsEnabled"] = new("IsHnsEnabled", "IsHnsEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["LargeFileSharesState"] = new("LargeFileSharesState", "LargeFileSharesState", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferenceRoutingChoice"] = new("RoutingPreferenceRoutingChoice", "RoutingPreferenceRoutingChoice", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferencePublishMicrosoftEndpoints"] = new("RoutingPreferencePublishMicrosoftEndpoints", "RoutingPreferencePublishMicrosoftEndpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferencePublishInternetEndpoints"] = new("RoutingPreferencePublishInternetEndpoints", "RoutingPreferencePublishInternetEndpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DualStackEndpointPreferencePublishIpv6Endpoint"] = new("DualStackEndpointPreferencePublishIpv6Endpoint", "DualStackEndpointPreferencePublishIpv6Endpoint", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess", "AllowBlobPublicAccess", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["MinimumTlsVersion"] = new("MinimumTlsVersion", "MinimumTlsVersion", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccess"] = new("AllowSharedKeyAccess", "AllowSharedKeyAccess", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableNfsV3"] = new("EnableNfsV3", "EnableNfsV3", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowCrossTenantReplication"] = new("AllowCrossTenantReplication", "AllowCrossTenantReplication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DefaultToOAuthAuthentication"] = new("DefaultToOAuthAuthentication", "DefaultToOAuthAuthentication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningEnabled"] = new("ImmutableStorageWithVersioningEnabled", "ImmutableStorageWithVersioningEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays"] = new("ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", "ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyState"] = new("ImmutableStorageWithVersioningImmutabilityPolicyState", "ImmutableStorageWithVersioningImmutabilityPolicyState", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites"] = new("ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", "ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsEndpointType"] = new("DnsEndpointType", "DnsEndpointType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["GeoPriorityReplicationStatusIsBlobEnabled"] = new("GeoPriorityReplicationStatusIsBlobEnabled", "GeoPriorityReplicationStatusIsBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesBlobEnabled"] = new("AllowSharedKeyAccessForServicesBlobEnabled", "AllowSharedKeyAccessForServicesBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesFileEnabled"] = new("AllowSharedKeyAccessForServicesFileEnabled", "AllowSharedKeyAccessForServicesFileEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesTableEnabled"] = new("AllowSharedKeyAccessForServicesTableEnabled", "AllowSharedKeyAccessForServicesTableEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesQueueEnabled"] = new("AllowSharedKeyAccessForServicesQueueEnabled", "AllowSharedKeyAccessForServicesQueueEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowStorageConnectors"] = new("DataCollaborationPolicyPropertiesAllowStorageConnectors", "DataCollaborationPolicyPropertiesAllowStorageConnectors", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowStorageDataShares"] = new("DataCollaborationPolicyPropertiesAllowStorageDataShares", "DataCollaborationPolicyPropertiesAllowStorageDataShares", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing"] = new("DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", "DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NoWait"]            = new("NoWait", "NoWait", typeof(bool), "Do not wait for the long-running operation to complete; return the initial response immediately.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_UpdateStorageAccountProcessor_PS = new(
            Name: "UpdateStorageAccountProcessor",
            InvocationName: "Update-AzStorageAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.UpdateStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.UpdateStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The accountName..", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SkuName"] = new("SkuName", "SkuName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tags"] = new("Tags", "Tags", typeof(object), "Resource tags.", IsRequired: false, Aliases: ["Tag"], AllowedValues: []),
                ["CustomDomainUseSubDomainName"] = new("CustomDomainUseSubDomainName", "CustomDomainUseSubDomainName", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesBlobEnabled"] = new("EncryptionServicesBlobEnabled", "EncryptionServicesBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesBlobKeyType"] = new("EncryptionServicesBlobKeyType", "EncryptionServicesBlobKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesFileEnabled"] = new("EncryptionServicesFileEnabled", "EncryptionServicesFileEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesFileKeyType"] = new("EncryptionServicesFileKeyType", "EncryptionServicesFileKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesTableEnabled"] = new("EncryptionServicesTableEnabled", "EncryptionServicesTableEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesTableKeyType"] = new("EncryptionServicesTableKeyType", "EncryptionServicesTableKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesQueueEnabled"] = new("EncryptionServicesQueueEnabled", "EncryptionServicesQueueEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionServicesQueueKeyType"] = new("EncryptionServicesQueueKeyType", "EncryptionServicesQueueKeyType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeySource"] = new("EncryptionKeySource", "EncryptionKeySource", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionRequireInfrastructureEncryption"] = new("EncryptionRequireInfrastructureEncryption", "EncryptionRequireInfrastructureEncryption", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyName"] = new("EncryptionKeyVaultPropertiesKeyName", "EncryptionKeyVaultPropertiesKeyName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyVersion"] = new("EncryptionKeyVaultPropertiesKeyVersion", "EncryptionKeyVaultPropertiesKeyVersion", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVaultPropertiesKeyVaultUri"] = new("EncryptionKeyVaultPropertiesKeyVaultUri", "EncryptionKeyVaultPropertiesKeyVaultUri", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEncryptionIdentityEncryptionUserAssignedIdentity"] = new("EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", "EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId"] = new("EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", "EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasPolicySasExpirationPeriod"] = new("SasPolicySasExpirationPeriod", "SasPolicySasExpirationPeriod", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasPolicyExpirationAction"] = new("SasPolicyExpirationAction", "SasPolicyExpirationAction", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["KeyPolicyKeyExpirationPeriodInDays"] = new("KeyPolicyKeyExpirationPeriodInDays", "KeyPolicyKeyExpirationPeriodInDays", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AccessTier"] = new("AccessTier", "AccessTier", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions"] = new("AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", "AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", "AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationDefaultSharePermission"] = new("AzureFilesIdentityBasedAuthenticationDefaultSharePermission", "AzureFilesIdentityBasedAuthenticationDefaultSharePermission", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled"] = new("AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", "AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableHttpsTrafficOnly"] = new("EnableHttpsTrafficOnly", "EnableHttpsTrafficOnly", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsSftpEnabled"] = new("IsSftpEnabled", "IsSftpEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsLocalUserEnabled"] = new("IsLocalUserEnabled", "IsLocalUserEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableExtendedGroups"] = new("EnableExtendedGroups", "EnableExtendedGroups", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkRuleSetBypass"] = new("NetworkRuleSetBypass", "NetworkRuleSetBypass", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetworkRuleSetDefaultAction"] = new("NetworkRuleSetDefaultAction", "NetworkRuleSetDefaultAction", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["LargeFileSharesState"] = new("LargeFileSharesState", "LargeFileSharesState", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferenceRoutingChoice"] = new("RoutingPreferenceRoutingChoice", "RoutingPreferenceRoutingChoice", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferencePublishMicrosoftEndpoints"] = new("RoutingPreferencePublishMicrosoftEndpoints", "RoutingPreferencePublishMicrosoftEndpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoutingPreferencePublishInternetEndpoints"] = new("RoutingPreferencePublishInternetEndpoints", "RoutingPreferencePublishInternetEndpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DualStackEndpointPreferencePublishIpv6Endpoint"] = new("DualStackEndpointPreferencePublishIpv6Endpoint", "DualStackEndpointPreferencePublishIpv6Endpoint", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess", "AllowBlobPublicAccess", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["MinimumTlsVersion"] = new("MinimumTlsVersion", "MinimumTlsVersion", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccess"] = new("AllowSharedKeyAccess", "AllowSharedKeyAccess", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowCrossTenantReplication"] = new("AllowCrossTenantReplication", "AllowCrossTenantReplication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DefaultToOAuthAuthentication"] = new("DefaultToOAuthAuthentication", "DefaultToOAuthAuthentication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PublicNetworkAccess"] = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningEnabled"] = new("ImmutableStorageWithVersioningEnabled", "ImmutableStorageWithVersioningEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays"] = new("ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", "ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyState"] = new("ImmutableStorageWithVersioningImmutabilityPolicyState", "ImmutableStorageWithVersioningImmutabilityPolicyState", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites"] = new("ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", "ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowedCopyScope"] = new("AllowedCopyScope", "AllowedCopyScope", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DnsEndpointType"] = new("DnsEndpointType", "DnsEndpointType", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["GeoPriorityReplicationStatusIsBlobEnabled"] = new("GeoPriorityReplicationStatusIsBlobEnabled", "GeoPriorityReplicationStatusIsBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesBlobEnabled"] = new("AllowSharedKeyAccessForServicesBlobEnabled", "AllowSharedKeyAccessForServicesBlobEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesFileEnabled"] = new("AllowSharedKeyAccessForServicesFileEnabled", "AllowSharedKeyAccessForServicesFileEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesTableEnabled"] = new("AllowSharedKeyAccessForServicesTableEnabled", "AllowSharedKeyAccessForServicesTableEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowSharedKeyAccessForServicesQueueEnabled"] = new("AllowSharedKeyAccessForServicesQueueEnabled", "AllowSharedKeyAccessForServicesQueueEnabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowStorageConnectors"] = new("DataCollaborationPolicyPropertiesAllowStorageConnectors", "DataCollaborationPolicyPropertiesAllowStorageConnectors", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowStorageDataShares"] = new("DataCollaborationPolicyPropertiesAllowStorageDataShares", "DataCollaborationPolicyPropertiesAllowStorageDataShares", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing"] = new("DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", "DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Kind"] = new("Kind", "Kind", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Zones"] = new("Zones", "Zones", typeof(List<string>), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["PlacementZonePlacementPolicy"] = new("PlacementZonePlacementPolicy", "PlacementZonePlacementPolicy", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_DeleteStorageAccountProcessor_PS = new(
            Name: "DeleteStorageAccountProcessor",
            InvocationName: "Remove-AzStorageAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.DeleteStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.DeleteStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The accountName..", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Force"] = new("Force", "Force", typeof(bool), "Suppress the confirmation prompt.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_ListStorageAccountProcessor_PS = new(
            Name: "ListStorageAccountProcessor",
            InvocationName: "Get-AzStorageAccount_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.ListStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.ListStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_ListBySubscriptionStorageAccountProcessor_PS = new(
            Name: "ListBySubscriptionStorageAccountProcessor",
            InvocationName: "Get-AzStorageAccount_ListBySubscription",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.ListBySubscriptionStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.ListBySubscriptionStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_GetStorageAccountProcessor_CLI_storage_account_show = new(
            Name: "GetStorageAccountProcessor",
            InvocationName: "storage account show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.GetStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.GetStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The accountName..", IsRequired: true, Aliases: ["-n"], AllowedValues: []),
                ["expand"] = new("Expand", "expand", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body-json-patch"] = new("BodyJsonPatch", "body-json-patch", typeof(string), "Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_NewStorageAccountProcessor_CLI_storage_account_create = new(
            Name: "NewStorageAccountProcessor",
            InvocationName: "storage account create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.NewStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.NewStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The accountName..", IsRequired: true, Aliases: ["-n"], AllowedValues: []),
                ["sku-name"] = new("SkuName", "sku-name", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["kind"] = new("Kind", "kind", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"] = new("Location", "location", typeof(string), "", IsRequired: true, Aliases: [], AllowedValues: []),
                ["zones"] = new("Zones", "zones", typeof(List<string>), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["placement-zone-placement-policy"] = new("PlacementZonePlacementPolicy", "placement-zone-placement-policy", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("Tags", "tags", typeof(object), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allowed-copy-scope"] = new("AllowedCopyScope", "allowed-copy-scope", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-network-access"] = new("PublicNetworkAccess", "public-network-access", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-policy-sas-expiration-period"] = new("SasPolicySasExpirationPeriod", "sas-policy-sas-expiration-period", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-policy-expiration-action"] = new("SasPolicyExpirationAction", "sas-policy-expiration-action", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["key-policy-key-expiration-period-in-days"] = new("KeyPolicyKeyExpirationPeriodInDays", "key-policy-key-expiration-period-in-days", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["custom-domain-use-sub-domain-name"] = new("CustomDomainUseSubDomainName", "custom-domain-use-sub-domain-name", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-blob-enabled"] = new("EncryptionServicesBlobEnabled", "encryption-services-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-blob-key-type"] = new("EncryptionServicesBlobKeyType", "encryption-services-blob-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-file-enabled"] = new("EncryptionServicesFileEnabled", "encryption-services-file-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-file-key-type"] = new("EncryptionServicesFileKeyType", "encryption-services-file-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-table-enabled"] = new("EncryptionServicesTableEnabled", "encryption-services-table-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-table-key-type"] = new("EncryptionServicesTableKeyType", "encryption-services-table-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-queue-enabled"] = new("EncryptionServicesQueueEnabled", "encryption-services-queue-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-queue-key-type"] = new("EncryptionServicesQueueKeyType", "encryption-services-queue-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-source"] = new("EncryptionKeySource", "encryption-key-source", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-require-infrastructure-encryption"] = new("EncryptionRequireInfrastructureEncryption", "encryption-require-infrastructure-encryption", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-name"] = new("EncryptionKeyVaultPropertiesKeyName", "encryption-key-vault-properties-key-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-version"] = new("EncryptionKeyVaultPropertiesKeyVersion", "encryption-key-vault-properties-key-version", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-vault-uri"] = new("EncryptionKeyVaultPropertiesKeyVaultUri", "encryption-key-vault-properties-key-vault-uri", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-encryption-identity-encryption-user-assigned-identity"] = new("EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", "encryption-encryption-identity-encryption-user-assigned-identity", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-encryption-identity-encryption-federated-identity-client-id"] = new("EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", "encryption-encryption-identity-encryption-federated-identity-client-id", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-rule-set-bypass"] = new("NetworkRuleSetBypass", "network-rule-set-bypass", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-rule-set-default-action"] = new("NetworkRuleSetDefaultAction", "network-rule-set-default-action", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-directory-service-options"] = new("AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", "azure-files-identity-based-authentication-directory-service-options", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", "azure-files-identity-based-authentication-active-directory-properties-domain-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", "azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-forest-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", "azure-files-identity-based-authentication-active-directory-properties-forest-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-guid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", "azure-files-identity-based-authentication-active-directory-properties-domain-guid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-sid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", "azure-files-identity-based-authentication-active-directory-properties-domain-sid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", "azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-sam-account-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", "azure-files-identity-based-authentication-active-directory-properties-sam-account-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-account-type"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", "azure-files-identity-based-authentication-active-directory-properties-account-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-default-share-permission"] = new("AzureFilesIdentityBasedAuthenticationDefaultSharePermission", "azure-files-identity-based-authentication-default-share-permission", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled"] = new("AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", "azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-https-traffic-only"] = new("EnableHttpsTrafficOnly", "enable-https-traffic-only", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["is-sftp-enabled"] = new("IsSftpEnabled", "is-sftp-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["is-local-user-enabled"] = new("IsLocalUserEnabled", "is-local-user-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-extended-groups"] = new("EnableExtendedGroups", "enable-extended-groups", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["is-hns-enabled"] = new("IsHnsEnabled", "is-hns-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["large-file-shares-state"] = new("LargeFileSharesState", "large-file-shares-state", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-routing-choice"] = new("RoutingPreferenceRoutingChoice", "routing-preference-routing-choice", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-publish-microsoft-endpoints"] = new("RoutingPreferencePublishMicrosoftEndpoints", "routing-preference-publish-microsoft-endpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-publish-internet-endpoints"] = new("RoutingPreferencePublishInternetEndpoints", "routing-preference-publish-internet-endpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dual-stack-endpoint-preference-publish-ipv6-endpoint"] = new("DualStackEndpointPreferencePublishIpv6Endpoint", "dual-stack-endpoint-preference-publish-ipv6-endpoint", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-blob-public-access"] = new("AllowBlobPublicAccess", "allow-blob-public-access", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["minimum-tls-version"] = new("MinimumTlsVersion", "minimum-tls-version", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access"] = new("AllowSharedKeyAccess", "allow-shared-key-access", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-nfs-v3"] = new("EnableNfsV3", "enable-nfs-v3", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-cross-tenant-replication"] = new("AllowCrossTenantReplication", "allow-cross-tenant-replication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["default-to-o-auth-authentication"] = new("DefaultToOAuthAuthentication", "default-to-o-auth-authentication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-enabled"] = new("ImmutableStorageWithVersioningEnabled", "immutable-storage-with-versioning-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days"] = new("ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", "immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-state"] = new("ImmutableStorageWithVersioningImmutabilityPolicyState", "immutable-storage-with-versioning-immutability-policy-state", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes"] = new("ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", "immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-endpoint-type"] = new("DnsEndpointType", "dns-endpoint-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["geo-priority-replication-status-is-blob-enabled"] = new("GeoPriorityReplicationStatusIsBlobEnabled", "geo-priority-replication-status-is-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-blob-enabled"] = new("AllowSharedKeyAccessForServicesBlobEnabled", "allow-shared-key-access-for-services-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-file-enabled"] = new("AllowSharedKeyAccessForServicesFileEnabled", "allow-shared-key-access-for-services-file-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-table-enabled"] = new("AllowSharedKeyAccessForServicesTableEnabled", "allow-shared-key-access-for-services-table-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-queue-enabled"] = new("AllowSharedKeyAccessForServicesQueueEnabled", "allow-shared-key-access-for-services-queue-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-storage-connectors"] = new("DataCollaborationPolicyPropertiesAllowStorageConnectors", "data-collaboration-policy-properties-allow-storage-connectors", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-storage-data-shares"] = new("DataCollaborationPolicyPropertiesAllowStorageDataShares", "data-collaboration-policy-properties-allow-storage-data-shares", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-cross-tenant-data-sharing"] = new("DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", "data-collaboration-policy-properties-allow-cross-tenant-data-sharing", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-wait"]         = new("NoWait", "no-wait", typeof(bool), "Do not wait for the long-running operation to complete; return the initial response immediately.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body-json-patch"] = new("BodyJsonPatch", "body-json-patch", typeof(string), "Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_UpdateStorageAccountProcessor_CLI_storage_account_update = new(
            Name: "UpdateStorageAccountProcessor",
            InvocationName: "storage account update",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.UpdateStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.UpdateStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The accountName..", IsRequired: true, Aliases: ["-n"], AllowedValues: []),
                ["sku-name"] = new("SkuName", "sku-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("Tags", "tags", typeof(object), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["custom-domain-use-sub-domain-name"] = new("CustomDomainUseSubDomainName", "custom-domain-use-sub-domain-name", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-blob-enabled"] = new("EncryptionServicesBlobEnabled", "encryption-services-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-blob-key-type"] = new("EncryptionServicesBlobKeyType", "encryption-services-blob-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-file-enabled"] = new("EncryptionServicesFileEnabled", "encryption-services-file-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-file-key-type"] = new("EncryptionServicesFileKeyType", "encryption-services-file-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-table-enabled"] = new("EncryptionServicesTableEnabled", "encryption-services-table-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-table-key-type"] = new("EncryptionServicesTableKeyType", "encryption-services-table-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-queue-enabled"] = new("EncryptionServicesQueueEnabled", "encryption-services-queue-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-services-queue-key-type"] = new("EncryptionServicesQueueKeyType", "encryption-services-queue-key-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-source"] = new("EncryptionKeySource", "encryption-key-source", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-require-infrastructure-encryption"] = new("EncryptionRequireInfrastructureEncryption", "encryption-require-infrastructure-encryption", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-name"] = new("EncryptionKeyVaultPropertiesKeyName", "encryption-key-vault-properties-key-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-version"] = new("EncryptionKeyVaultPropertiesKeyVersion", "encryption-key-vault-properties-key-version", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-vault-properties-key-vault-uri"] = new("EncryptionKeyVaultPropertiesKeyVaultUri", "encryption-key-vault-properties-key-vault-uri", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-encryption-identity-encryption-user-assigned-identity"] = new("EncryptionEncryptionIdentityEncryptionUserAssignedIdentity", "encryption-encryption-identity-encryption-user-assigned-identity", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-encryption-identity-encryption-federated-identity-client-id"] = new("EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId", "encryption-encryption-identity-encryption-federated-identity-client-id", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-policy-sas-expiration-period"] = new("SasPolicySasExpirationPeriod", "sas-policy-sas-expiration-period", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-policy-expiration-action"] = new("SasPolicyExpirationAction", "sas-policy-expiration-action", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["key-policy-key-expiration-period-in-days"] = new("KeyPolicyKeyExpirationPeriodInDays", "key-policy-key-expiration-period-in-days", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-directory-service-options"] = new("AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions", "azure-files-identity-based-authentication-directory-service-options", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName", "azure-files-identity-based-authentication-active-directory-properties-domain-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName", "azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-forest-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName", "azure-files-identity-based-authentication-active-directory-properties-forest-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-guid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid", "azure-files-identity-based-authentication-active-directory-properties-domain-guid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-domain-sid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid", "azure-files-identity-based-authentication-active-directory-properties-domain-sid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid", "azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-sam-account-name"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName", "azure-files-identity-based-authentication-active-directory-properties-sam-account-name", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-active-directory-properties-account-type"] = new("AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType", "azure-files-identity-based-authentication-active-directory-properties-account-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-default-share-permission"] = new("AzureFilesIdentityBasedAuthenticationDefaultSharePermission", "azure-files-identity-based-authentication-default-share-permission", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled"] = new("AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled", "azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-https-traffic-only"] = new("EnableHttpsTrafficOnly", "enable-https-traffic-only", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["is-sftp-enabled"] = new("IsSftpEnabled", "is-sftp-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["is-local-user-enabled"] = new("IsLocalUserEnabled", "is-local-user-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-extended-groups"] = new("EnableExtendedGroups", "enable-extended-groups", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-rule-set-bypass"] = new("NetworkRuleSetBypass", "network-rule-set-bypass", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["network-rule-set-default-action"] = new("NetworkRuleSetDefaultAction", "network-rule-set-default-action", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["large-file-shares-state"] = new("LargeFileSharesState", "large-file-shares-state", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-routing-choice"] = new("RoutingPreferenceRoutingChoice", "routing-preference-routing-choice", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-publish-microsoft-endpoints"] = new("RoutingPreferencePublishMicrosoftEndpoints", "routing-preference-publish-microsoft-endpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["routing-preference-publish-internet-endpoints"] = new("RoutingPreferencePublishInternetEndpoints", "routing-preference-publish-internet-endpoints", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dual-stack-endpoint-preference-publish-ipv6-endpoint"] = new("DualStackEndpointPreferencePublishIpv6Endpoint", "dual-stack-endpoint-preference-publish-ipv6-endpoint", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-blob-public-access"] = new("AllowBlobPublicAccess", "allow-blob-public-access", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["minimum-tls-version"] = new("MinimumTlsVersion", "minimum-tls-version", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access"] = new("AllowSharedKeyAccess", "allow-shared-key-access", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-cross-tenant-replication"] = new("AllowCrossTenantReplication", "allow-cross-tenant-replication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["default-to-o-auth-authentication"] = new("DefaultToOAuthAuthentication", "default-to-o-auth-authentication", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["public-network-access"] = new("PublicNetworkAccess", "public-network-access", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-enabled"] = new("ImmutableStorageWithVersioningEnabled", "immutable-storage-with-versioning-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days"] = new("ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays", "immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days", typeof(int), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-state"] = new("ImmutableStorageWithVersioningImmutabilityPolicyState", "immutable-storage-with-versioning-immutability-policy-state", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes"] = new("ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites", "immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allowed-copy-scope"] = new("AllowedCopyScope", "allowed-copy-scope", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dns-endpoint-type"] = new("DnsEndpointType", "dns-endpoint-type", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["geo-priority-replication-status-is-blob-enabled"] = new("GeoPriorityReplicationStatusIsBlobEnabled", "geo-priority-replication-status-is-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-blob-enabled"] = new("AllowSharedKeyAccessForServicesBlobEnabled", "allow-shared-key-access-for-services-blob-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-file-enabled"] = new("AllowSharedKeyAccessForServicesFileEnabled", "allow-shared-key-access-for-services-file-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-table-enabled"] = new("AllowSharedKeyAccessForServicesTableEnabled", "allow-shared-key-access-for-services-table-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["allow-shared-key-access-for-services-queue-enabled"] = new("AllowSharedKeyAccessForServicesQueueEnabled", "allow-shared-key-access-for-services-queue-enabled", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-storage-connectors"] = new("DataCollaborationPolicyPropertiesAllowStorageConnectors", "data-collaboration-policy-properties-allow-storage-connectors", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-storage-data-shares"] = new("DataCollaborationPolicyPropertiesAllowStorageDataShares", "data-collaboration-policy-properties-allow-storage-data-shares", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["data-collaboration-policy-properties-allow-cross-tenant-data-sharing"] = new("DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing", "data-collaboration-policy-properties-allow-cross-tenant-data-sharing", typeof(bool), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["kind"] = new("Kind", "kind", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["zones"] = new("Zones", "zones", typeof(List<string>), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["placement-zone-placement-policy"] = new("PlacementZonePlacementPolicy", "placement-zone-placement-policy", typeof(string), "", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body-json-patch"] = new("BodyJsonPatch", "body-json-patch", typeof(string), "Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_DeleteStorageAccountProcessor_CLI_storage_account_delete = new(
            Name: "DeleteStorageAccountProcessor",
            InvocationName: "storage account delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.DeleteStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.DeleteStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The accountName..", IsRequired: true, Aliases: ["-n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_ListStorageAccountProcessor_CLI_storage_account_list = new(
            Name: "ListStorageAccountProcessor",
            InvocationName: "storage account list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.ListStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.ListStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_ListBySubscriptionStorageAccountProcessor_CLI_storage_account_list_by_subscription = new(
            Name: "ListBySubscriptionStorageAccountProcessor",
            InvocationName: "storage account list-by-subscription",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.ListBySubscriptionStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.ListBySubscriptionStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_GetStorageAccountProcessor_CLI_storage_account_wait = new(
            Name: "GetStorageAccountProcessor",
            InvocationName: "storage account wait",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors.GetStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.StorageAccountTsp",
            ProcessorType: typeof(Processors.GetStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["-g"], AllowedValues: []),
                ["name"] = new("Name", "name", typeof(string), "The accountName..", IsRequired: true, Aliases: ["-n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Get-AzStorageAccount_Get"] = s_GetStorageAccountProcessor_PS,
            ["New-AzStorageAccount"] = s_NewStorageAccountProcessor_PS,
            ["Update-AzStorageAccount"] = s_UpdateStorageAccountProcessor_PS,
            ["Remove-AzStorageAccount"] = s_DeleteStorageAccountProcessor_PS,
            ["Get-AzStorageAccount_List"] = s_ListStorageAccountProcessor_PS,
            ["Get-AzStorageAccount_ListBySubscription"] = s_ListBySubscriptionStorageAccountProcessor_PS,
            ["Wait-AzStorageAccount"] = s_GetStorageAccountProcessor_PS,
        };

        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["storage account show"] = s_GetStorageAccountProcessor_CLI_storage_account_show,
            ["storage account create"] = s_NewStorageAccountProcessor_CLI_storage_account_create,
            ["storage account update"] = s_UpdateStorageAccountProcessor_CLI_storage_account_update,
            ["storage account delete"] = s_DeleteStorageAccountProcessor_CLI_storage_account_delete,
            ["storage account list"] = s_ListStorageAccountProcessor_CLI_storage_account_list,
            ["storage account list-by-subscription"] = s_ListBySubscriptionStorageAccountProcessor_CLI_storage_account_list_by_subscription,
            ["storage account wait"] = s_GetStorageAccountProcessor_CLI_storage_account_wait,
        };


        // Inject the shared --profile / -Profile parameter into every command.
        // Also fold in any custom command metadata registered by hand-written
        // partial classes under this Extension's Custom/ folder — see
        // Custom/README.md for the extension pattern.
        static ExtensionIndex()
        {
            RegisterCustomCommands();
            foreach (var metadata in s_cliTable.Values)
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
            foreach (var metadata in s_psTable.Values)
            {
                metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
        }

        // Partial hook — implement in Custom/ to add extra commands, aliases, or param
        // overrides after the generated tables are populated. Default no-op if no
        // hand-written partial provides one.
        static partial void RegisterCustomCommands();

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            var table = clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
            return table.TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Values,
                ClientType.CLI => s_cliTable.Values,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Keys,
                ClientType.CLI => s_cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
    }
}
