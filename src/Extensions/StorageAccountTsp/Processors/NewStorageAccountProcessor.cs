// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated from swagger operation StorageAccounts_Create
//   PUT /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{Name}
//   Kind: create_or_update   |   LRO final-state-via: location

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;
using _Response = Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models.StorageAccount;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors;

public class NewStorageAccountProcessor : StorageTspProcessorBase<_Response>
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Body parameters (flattened from ).
    // Optional escape hatch for cases where the tsp emitter can't cleanly flatten
    // (e.g. array-of-model like VMSS networkProfile.networkInterfaceConfigurations).
    // Deep-merged into the built body just before the request goes out.
    public string? BodyJsonPatch { get; set; }
    public string SkuName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string>? Zones { get; set; }
    public string? PlacementZonePlacementPolicy { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? AllowedCopyScope { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public string? SasPolicySasExpirationPeriod { get; set; }
    public string? SasPolicyExpirationAction { get; set; }
    public int? KeyPolicyKeyExpirationPeriodInDays { get; set; }
    public bool? CustomDomainUseSubDomainName { get; set; }
    public bool? EncryptionServicesBlobEnabled { get; set; }
    public string? EncryptionServicesBlobKeyType { get; set; }
    public bool? EncryptionServicesFileEnabled { get; set; }
    public string? EncryptionServicesFileKeyType { get; set; }
    public bool? EncryptionServicesTableEnabled { get; set; }
    public string? EncryptionServicesTableKeyType { get; set; }
    public bool? EncryptionServicesQueueEnabled { get; set; }
    public string? EncryptionServicesQueueKeyType { get; set; }
    public string? EncryptionKeySource { get; set; }
    public bool? EncryptionRequireInfrastructureEncryption { get; set; }
    public string? EncryptionKeyVaultPropertiesKeyName { get; set; }
    public string? EncryptionKeyVaultPropertiesKeyVersion { get; set; }
    public string? EncryptionKeyVaultPropertiesKeyVaultUri { get; set; }
    public string? EncryptionEncryptionIdentityEncryptionUserAssignedIdentity { get; set; }
    public string? EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId { get; set; }
    public string? NetworkRuleSetBypass { get; set; }
    public string? NetworkRuleSetDefaultAction { get; set; }
    public string? AccessTier { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType { get; set; }
    public string? AzureFilesIdentityBasedAuthenticationDefaultSharePermission { get; set; }
    public bool? AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled { get; set; }
    public bool? EnableHttpsTrafficOnly { get; set; }
    public bool? IsSftpEnabled { get; set; }
    public bool? IsLocalUserEnabled { get; set; }
    public bool? EnableExtendedGroups { get; set; }
    public bool? IsHnsEnabled { get; set; }
    public string? LargeFileSharesState { get; set; }
    public string? RoutingPreferenceRoutingChoice { get; set; }
    public bool? RoutingPreferencePublishMicrosoftEndpoints { get; set; }
    public bool? RoutingPreferencePublishInternetEndpoints { get; set; }
    public bool? DualStackEndpointPreferencePublishIpv6Endpoint { get; set; }
    public bool? AllowBlobPublicAccess { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public bool? AllowSharedKeyAccess { get; set; }
    public bool? EnableNfsV3 { get; set; }
    public bool? AllowCrossTenantReplication { get; set; }
    public bool? DefaultToOAuthAuthentication { get; set; }
    public bool? ImmutableStorageWithVersioningEnabled { get; set; }
    public int? ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays { get; set; }
    public string? ImmutableStorageWithVersioningImmutabilityPolicyState { get; set; }
    public bool? ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites { get; set; }
    public string? DnsEndpointType { get; set; }
    public bool? GeoPriorityReplicationStatusIsBlobEnabled { get; set; }
    public bool? AllowSharedKeyAccessForServicesBlobEnabled { get; set; }
    public bool? AllowSharedKeyAccessForServicesFileEnabled { get; set; }
    public bool? AllowSharedKeyAccessForServicesTableEnabled { get; set; }
    public bool? AllowSharedKeyAccessForServicesQueueEnabled { get; set; }
    public bool? DataCollaborationPolicyPropertiesAllowStorageConnectors { get; set; }
    public bool? DataCollaborationPolicyPropertiesAllowStorageDataShares { get; set; }
    public bool? DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing { get; set; }

    /// <summary>
    /// Copies the generated command parameter surface to an atomic child
    /// processor created by an orchestrator.
    /// </summary>
    protected void CopyParametersTo(NewStorageAccountProcessor target)
    {
        target.SubscriptionId = SubscriptionId;
        target.Profile = Profile;
        target.NoWait = NoWait;
        target.ResourceGroupName = ResourceGroupName;
        target.Name = Name;
        target.BodyJsonPatch = BodyJsonPatch;
        target.SkuName = SkuName;
        target.Kind = Kind;
        target.Location = Location;
        target.Zones = Zones;
        target.PlacementZonePlacementPolicy = PlacementZonePlacementPolicy;
        target.Tags = Tags;
        target.AllowedCopyScope = AllowedCopyScope;
        target.PublicNetworkAccess = PublicNetworkAccess;
        target.SasPolicySasExpirationPeriod = SasPolicySasExpirationPeriod;
        target.SasPolicyExpirationAction = SasPolicyExpirationAction;
        target.KeyPolicyKeyExpirationPeriodInDays = KeyPolicyKeyExpirationPeriodInDays;
        target.CustomDomainUseSubDomainName = CustomDomainUseSubDomainName;
        target.EncryptionServicesBlobEnabled = EncryptionServicesBlobEnabled;
        target.EncryptionServicesBlobKeyType = EncryptionServicesBlobKeyType;
        target.EncryptionServicesFileEnabled = EncryptionServicesFileEnabled;
        target.EncryptionServicesFileKeyType = EncryptionServicesFileKeyType;
        target.EncryptionServicesTableEnabled = EncryptionServicesTableEnabled;
        target.EncryptionServicesTableKeyType = EncryptionServicesTableKeyType;
        target.EncryptionServicesQueueEnabled = EncryptionServicesQueueEnabled;
        target.EncryptionServicesQueueKeyType = EncryptionServicesQueueKeyType;
        target.EncryptionKeySource = EncryptionKeySource;
        target.EncryptionRequireInfrastructureEncryption = EncryptionRequireInfrastructureEncryption;
        target.EncryptionKeyVaultPropertiesKeyName = EncryptionKeyVaultPropertiesKeyName;
        target.EncryptionKeyVaultPropertiesKeyVersion = EncryptionKeyVaultPropertiesKeyVersion;
        target.EncryptionKeyVaultPropertiesKeyVaultUri = EncryptionKeyVaultPropertiesKeyVaultUri;
        target.EncryptionEncryptionIdentityEncryptionUserAssignedIdentity = EncryptionEncryptionIdentityEncryptionUserAssignedIdentity;
        target.EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId = EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId;
        target.NetworkRuleSetBypass = NetworkRuleSetBypass;
        target.NetworkRuleSetDefaultAction = NetworkRuleSetDefaultAction;
        target.AccessTier = AccessTier;
        target.AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions = AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName;
        target.AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType = AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType;
        target.AzureFilesIdentityBasedAuthenticationDefaultSharePermission = AzureFilesIdentityBasedAuthenticationDefaultSharePermission;
        target.AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled = AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled;
        target.EnableHttpsTrafficOnly = EnableHttpsTrafficOnly;
        target.IsSftpEnabled = IsSftpEnabled;
        target.IsLocalUserEnabled = IsLocalUserEnabled;
        target.EnableExtendedGroups = EnableExtendedGroups;
        target.IsHnsEnabled = IsHnsEnabled;
        target.LargeFileSharesState = LargeFileSharesState;
        target.RoutingPreferenceRoutingChoice = RoutingPreferenceRoutingChoice;
        target.RoutingPreferencePublishMicrosoftEndpoints = RoutingPreferencePublishMicrosoftEndpoints;
        target.RoutingPreferencePublishInternetEndpoints = RoutingPreferencePublishInternetEndpoints;
        target.DualStackEndpointPreferencePublishIpv6Endpoint = DualStackEndpointPreferencePublishIpv6Endpoint;
        target.AllowBlobPublicAccess = AllowBlobPublicAccess;
        target.MinimumTlsVersion = MinimumTlsVersion;
        target.AllowSharedKeyAccess = AllowSharedKeyAccess;
        target.EnableNfsV3 = EnableNfsV3;
        target.AllowCrossTenantReplication = AllowCrossTenantReplication;
        target.DefaultToOAuthAuthentication = DefaultToOAuthAuthentication;
        target.ImmutableStorageWithVersioningEnabled = ImmutableStorageWithVersioningEnabled;
        target.ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays = ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays;
        target.ImmutableStorageWithVersioningImmutabilityPolicyState = ImmutableStorageWithVersioningImmutabilityPolicyState;
        target.ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites = ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites;
        target.DnsEndpointType = DnsEndpointType;
        target.GeoPriorityReplicationStatusIsBlobEnabled = GeoPriorityReplicationStatusIsBlobEnabled;
        target.AllowSharedKeyAccessForServicesBlobEnabled = AllowSharedKeyAccessForServicesBlobEnabled;
        target.AllowSharedKeyAccessForServicesFileEnabled = AllowSharedKeyAccessForServicesFileEnabled;
        target.AllowSharedKeyAccessForServicesTableEnabled = AllowSharedKeyAccessForServicesTableEnabled;
        target.AllowSharedKeyAccessForServicesQueueEnabled = AllowSharedKeyAccessForServicesQueueEnabled;
        target.DataCollaborationPolicyPropertiesAllowStorageConnectors = DataCollaborationPolicyPropertiesAllowStorageConnectors;
        target.DataCollaborationPolicyPropertiesAllowStorageDataShares = DataCollaborationPolicyPropertiesAllowStorageDataShares;
        target.DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing = DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        HttpContent bodyContent = BuildNestedBody(new (string, object?)[]
        {
            ("sku.name", (object?)SkuName),
            ("kind", (object?)Kind),
            ("location", (object?)Location),
            ("zones", (object?)Zones),
            ("placement.zonePlacementPolicy", (object?)PlacementZonePlacementPolicy),
            ("tags", (object?)Tags),
            ("properties.allowedCopyScope", (object?)AllowedCopyScope),
            ("properties.publicNetworkAccess", (object?)PublicNetworkAccess),
            ("properties.sasPolicy.sasExpirationPeriod", (object?)SasPolicySasExpirationPeriod),
            ("properties.sasPolicy.expirationAction", (object?)SasPolicyExpirationAction),
            ("properties.keyPolicy.keyExpirationPeriodInDays", (object?)KeyPolicyKeyExpirationPeriodInDays),
            ("properties.customDomain.useSubDomainName", (object?)CustomDomainUseSubDomainName),
            ("properties.encryption.services.blob.enabled", (object?)EncryptionServicesBlobEnabled),
            ("properties.encryption.services.blob.keyType", (object?)EncryptionServicesBlobKeyType),
            ("properties.encryption.services.file.enabled", (object?)EncryptionServicesFileEnabled),
            ("properties.encryption.services.file.keyType", (object?)EncryptionServicesFileKeyType),
            ("properties.encryption.services.table.enabled", (object?)EncryptionServicesTableEnabled),
            ("properties.encryption.services.table.keyType", (object?)EncryptionServicesTableKeyType),
            ("properties.encryption.services.queue.enabled", (object?)EncryptionServicesQueueEnabled),
            ("properties.encryption.services.queue.keyType", (object?)EncryptionServicesQueueKeyType),
            ("properties.encryption.keySource", (object?)EncryptionKeySource),
            ("properties.encryption.RequireInfrastructureEncryption", (object?)EncryptionRequireInfrastructureEncryption),
            ("properties.encryption.KeyVaultProperties.KeyName", (object?)EncryptionKeyVaultPropertiesKeyName),
            ("properties.encryption.KeyVaultProperties.KeyVersion", (object?)EncryptionKeyVaultPropertiesKeyVersion),
            ("properties.encryption.KeyVaultProperties.KeyVaultUri", (object?)EncryptionKeyVaultPropertiesKeyVaultUri),
            ("properties.encryption.EncryptionIdentity.EncryptionUserAssignedIdentity", (object?)EncryptionEncryptionIdentityEncryptionUserAssignedIdentity),
            ("properties.encryption.EncryptionIdentity.EncryptionFederatedIdentityClientId", (object?)EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId),
            ("properties.NetworkRuleSet.Bypass", (object?)NetworkRuleSetBypass),
            ("properties.NetworkRuleSet.defaultAction", (object?)NetworkRuleSetDefaultAction),
            ("properties.accessTier", (object?)AccessTier),
            ("properties.azureFilesIdentityBasedAuthentication.directoryServiceOptions", (object?)AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainName", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.netBiosDomainName", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.forestName", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainGuid", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.domainSid", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.azureStorageSid", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.samAccountName", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName),
            ("properties.azureFilesIdentityBasedAuthentication.activeDirectoryProperties.accountType", (object?)AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType),
            ("properties.azureFilesIdentityBasedAuthentication.defaultSharePermission", (object?)AzureFilesIdentityBasedAuthenticationDefaultSharePermission),
            ("properties.azureFilesIdentityBasedAuthentication.smbOAuthSettings.isSmbOAuthEnabled", (object?)AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled),
            ("properties.EnableHttpsTrafficOnly", (object?)EnableHttpsTrafficOnly),
            ("properties.IsSftpEnabled", (object?)IsSftpEnabled),
            ("properties.IsLocalUserEnabled", (object?)IsLocalUserEnabled),
            ("properties.EnableExtendedGroups", (object?)EnableExtendedGroups),
            ("properties.IsHnsEnabled", (object?)IsHnsEnabled),
            ("properties.largeFileSharesState", (object?)LargeFileSharesState),
            ("properties.RoutingPreference.routingChoice", (object?)RoutingPreferenceRoutingChoice),
            ("properties.RoutingPreference.publishMicrosoftEndpoints", (object?)RoutingPreferencePublishMicrosoftEndpoints),
            ("properties.RoutingPreference.publishInternetEndpoints", (object?)RoutingPreferencePublishInternetEndpoints),
            ("properties.dualStackEndpointPreference.publishIpv6Endpoint", (object?)DualStackEndpointPreferencePublishIpv6Endpoint),
            ("properties.AllowBlobPublicAccess", (object?)AllowBlobPublicAccess),
            ("properties.minimumTlsVersion", (object?)MinimumTlsVersion),
            ("properties.allowSharedKeyAccess", (object?)AllowSharedKeyAccess),
            ("properties.EnableNfsV3", (object?)EnableNfsV3),
            ("properties.allowCrossTenantReplication", (object?)AllowCrossTenantReplication),
            ("properties.defaultToOAuthAuthentication", (object?)DefaultToOAuthAuthentication),
            ("properties.ImmutableStorageWithVersioning.enabled", (object?)ImmutableStorageWithVersioningEnabled),
            ("properties.ImmutableStorageWithVersioning.immutabilityPolicy.immutabilityPeriodSinceCreationInDays", (object?)ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays),
            ("properties.ImmutableStorageWithVersioning.immutabilityPolicy.state", (object?)ImmutableStorageWithVersioningImmutabilityPolicyState),
            ("properties.ImmutableStorageWithVersioning.immutabilityPolicy.allowProtectedAppendWrites", (object?)ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites),
            ("properties.dnsEndpointType", (object?)DnsEndpointType),
            ("properties.geoPriorityReplicationStatus.isBlobEnabled", (object?)GeoPriorityReplicationStatusIsBlobEnabled),
            ("properties.allowSharedKeyAccessForServices.blob.enabled", (object?)AllowSharedKeyAccessForServicesBlobEnabled),
            ("properties.allowSharedKeyAccessForServices.file.enabled", (object?)AllowSharedKeyAccessForServicesFileEnabled),
            ("properties.allowSharedKeyAccessForServices.table.enabled", (object?)AllowSharedKeyAccessForServicesTableEnabled),
            ("properties.allowSharedKeyAccessForServices.queue.enabled", (object?)AllowSharedKeyAccessForServicesQueueEnabled),
            ("properties.dataCollaborationPolicyProperties.allowStorageConnectors", (object?)DataCollaborationPolicyPropertiesAllowStorageConnectors),
            ("properties.dataCollaborationPolicyProperties.allowStorageDataShares", (object?)DataCollaborationPolicyPropertiesAllowStorageDataShares),
            ("properties.dataCollaborationPolicyProperties.allowCrossTenantDataSharing", (object?)DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing),
        }, bodyJsonPatch: BodyJsonPatch);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, StorageAccountUri(ResourceGroupName, Name))
        {
            Content = bodyContent
        };
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
