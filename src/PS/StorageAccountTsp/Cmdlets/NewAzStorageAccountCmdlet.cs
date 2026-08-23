// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated cmdlet for the StorageAccountTsp extension.
// Source: TypeSpec (specification/storage/Storage.Management)
using System.Collections;
using System.Management.Automation;
using System.Security;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.StorageAccountTsp;

/// <summary>
/// Invokes <c>New-AzStorageAccount</c> for the StorageAccount resource.
/// </summary>
/// <example>
/// <code>New-AzStorageAccount -SubscriptionId &lt;subscription-id&gt; -ResourceGroupName &lt;resource-group&gt; -Name &lt;name&gt;</code>
/// </example>
[Cmdlet(VerbsCommon.New, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class NewAzStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = false)] public string? Profile { get; set; }
    [Parameter(Mandatory = true)]  public string SubscriptionId { get; set; } = "";
    [Parameter(Mandatory = true)]  public string ResourceGroupName { get; set; } = "";
    [Parameter(Mandatory = true)]  public string Name { get; set; } = "";
    [Parameter(Mandatory = true)]  public string SkuName { get; set; } = "";
    [Parameter(Mandatory = true)]  public string Kind { get; set; } = "";
    [Parameter(Mandatory = true)]  public string Location { get; set; } = "";
    [Parameter(Mandatory = false)] public List<string>? Zones { get; set; }
    [Parameter(Mandatory = false)] public string? PlacementZonePlacementPolicy { get; set; }
    [Alias("Tag")]
    [Parameter(Mandatory = false)] public Hashtable? Tags { get; set; }
    [Parameter(Mandatory = false)] public string? AllowedCopyScope { get; set; }
    [Parameter(Mandatory = false)] public string? PublicNetworkAccess { get; set; }
    [Parameter(Mandatory = false)] public string? SasPolicySasExpirationPeriod { get; set; }
    [Parameter(Mandatory = false)] public string? SasPolicyExpirationAction { get; set; }
    [Parameter(Mandatory = false)] public int? KeyPolicyKeyExpirationPeriodInDays { get; set; }
    [Parameter(Mandatory = false)] public bool? CustomDomainUseSubDomainName { get; set; }
    [Parameter(Mandatory = false)] public bool? EncryptionServicesBlobEnabled { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionServicesBlobKeyType { get; set; }
    [Parameter(Mandatory = false)] public bool? EncryptionServicesFileEnabled { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionServicesFileKeyType { get; set; }
    [Parameter(Mandatory = false)] public bool? EncryptionServicesTableEnabled { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionServicesTableKeyType { get; set; }
    [Parameter(Mandatory = false)] public bool? EncryptionServicesQueueEnabled { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionServicesQueueKeyType { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionKeySource { get; set; }
    [Parameter(Mandatory = false)] public bool? EncryptionRequireInfrastructureEncryption { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionKeyVaultPropertiesKeyName { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionKeyVaultPropertiesKeyVersion { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionKeyVaultPropertiesKeyVaultUri { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionEncryptionIdentityEncryptionUserAssignedIdentity { get; set; }
    [Parameter(Mandatory = false)] public string? EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId { get; set; }
    [Parameter(Mandatory = false)] public string? NetworkRuleSetBypass { get; set; }
    [Parameter(Mandatory = false)] public string? NetworkRuleSetDefaultAction { get; set; }
    [Parameter(Mandatory = false)] public string? AccessTier { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType { get; set; }
    [Parameter(Mandatory = false)] public string? AzureFilesIdentityBasedAuthenticationDefaultSharePermission { get; set; }
    [Parameter(Mandatory = false)] public bool? AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? EnableHttpsTrafficOnly { get; set; }
    [Parameter(Mandatory = false)] public bool? IsSftpEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? IsLocalUserEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? EnableExtendedGroups { get; set; }
    [Parameter(Mandatory = false)] public bool? IsHnsEnabled { get; set; }
    [Parameter(Mandatory = false)] public string? LargeFileSharesState { get; set; }
    [Parameter(Mandatory = false)] public string? RoutingPreferenceRoutingChoice { get; set; }
    [Parameter(Mandatory = false)] public bool? RoutingPreferencePublishMicrosoftEndpoints { get; set; }
    [Parameter(Mandatory = false)] public bool? RoutingPreferencePublishInternetEndpoints { get; set; }
    [Parameter(Mandatory = false)] public bool? DualStackEndpointPreferencePublishIpv6Endpoint { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowBlobPublicAccess { get; set; }
    [Parameter(Mandatory = false)] public string? MinimumTlsVersion { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowSharedKeyAccess { get; set; }
    [Parameter(Mandatory = false)] public bool? EnableNfsV3 { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowCrossTenantReplication { get; set; }
    [Parameter(Mandatory = false)] public bool? DefaultToOAuthAuthentication { get; set; }
    [Parameter(Mandatory = false)] public bool? ImmutableStorageWithVersioningEnabled { get; set; }
    [Parameter(Mandatory = false)] public int? ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays { get; set; }
    [Parameter(Mandatory = false)] public string? ImmutableStorageWithVersioningImmutabilityPolicyState { get; set; }
    [Parameter(Mandatory = false)] public bool? ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites { get; set; }
    [Parameter(Mandatory = false)] public string? DnsEndpointType { get; set; }
    [Parameter(Mandatory = false)] public bool? GeoPriorityReplicationStatusIsBlobEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowSharedKeyAccessForServicesBlobEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowSharedKeyAccessForServicesFileEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowSharedKeyAccessForServicesTableEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? AllowSharedKeyAccessForServicesQueueEnabled { get; set; }
    [Parameter(Mandatory = false)] public bool? DataCollaborationPolicyPropertiesAllowStorageConnectors { get; set; }
    [Parameter(Mandatory = false)] public bool? DataCollaborationPolicyPropertiesAllowStorageDataShares { get; set; }
    [Parameter(Mandatory = false)] public bool? DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter NoWait { get; set; }
    // Escape hatch: raw JSON deep-merged into the built body just before send.
    // Used for cases the tsp emitter can't cleanly flatten (array-of-model).
    [Parameter(Mandatory = false)] public string? BodyJsonPatch { get; set; }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"StorageTsp AzStorageAccount '{Name}' in resource group '{ResourceGroupName}'", "New")) return;

        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"]    = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"]              = Name,
            ["SkuName"] = SkuName,
            ["Kind"] = Kind,
            ["Location"] = Location,
        };
        parameters.AddIfBound(this, nameof(Zones), Zones);
        parameters.AddIfBound(this, nameof(PlacementZonePlacementPolicy), PlacementZonePlacementPolicy);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags)) && Tags is not null)
            parameters[nameof(Tags)] = Tags.ToStringDictionary();
        parameters.AddIfBound(this, nameof(AllowedCopyScope), AllowedCopyScope);
        parameters.AddIfBound(this, nameof(PublicNetworkAccess), PublicNetworkAccess);
        parameters.AddIfBound(this, nameof(SasPolicySasExpirationPeriod), SasPolicySasExpirationPeriod);
        parameters.AddIfBound(this, nameof(SasPolicyExpirationAction), SasPolicyExpirationAction);
        parameters.AddIfBound(this, nameof(KeyPolicyKeyExpirationPeriodInDays), KeyPolicyKeyExpirationPeriodInDays);
        parameters.AddIfBound(this, nameof(CustomDomainUseSubDomainName), CustomDomainUseSubDomainName);
        parameters.AddIfBound(this, nameof(EncryptionServicesBlobEnabled), EncryptionServicesBlobEnabled);
        parameters.AddIfBound(this, nameof(EncryptionServicesBlobKeyType), EncryptionServicesBlobKeyType);
        parameters.AddIfBound(this, nameof(EncryptionServicesFileEnabled), EncryptionServicesFileEnabled);
        parameters.AddIfBound(this, nameof(EncryptionServicesFileKeyType), EncryptionServicesFileKeyType);
        parameters.AddIfBound(this, nameof(EncryptionServicesTableEnabled), EncryptionServicesTableEnabled);
        parameters.AddIfBound(this, nameof(EncryptionServicesTableKeyType), EncryptionServicesTableKeyType);
        parameters.AddIfBound(this, nameof(EncryptionServicesQueueEnabled), EncryptionServicesQueueEnabled);
        parameters.AddIfBound(this, nameof(EncryptionServicesQueueKeyType), EncryptionServicesQueueKeyType);
        parameters.AddIfBound(this, nameof(EncryptionKeySource), EncryptionKeySource);
        parameters.AddIfBound(this, nameof(EncryptionRequireInfrastructureEncryption), EncryptionRequireInfrastructureEncryption);
        parameters.AddIfBound(this, nameof(EncryptionKeyVaultPropertiesKeyName), EncryptionKeyVaultPropertiesKeyName);
        parameters.AddIfBound(this, nameof(EncryptionKeyVaultPropertiesKeyVersion), EncryptionKeyVaultPropertiesKeyVersion);
        parameters.AddIfBound(this, nameof(EncryptionKeyVaultPropertiesKeyVaultUri), EncryptionKeyVaultPropertiesKeyVaultUri);
        parameters.AddIfBound(this, nameof(EncryptionEncryptionIdentityEncryptionUserAssignedIdentity), EncryptionEncryptionIdentityEncryptionUserAssignedIdentity);
        parameters.AddIfBound(this, nameof(EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId), EncryptionEncryptionIdentityEncryptionFederatedIdentityClientId);
        parameters.AddIfBound(this, nameof(NetworkRuleSetBypass), NetworkRuleSetBypass);
        parameters.AddIfBound(this, nameof(NetworkRuleSetDefaultAction), NetworkRuleSetDefaultAction);
        parameters.AddIfBound(this, nameof(AccessTier), AccessTier);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions), AzureFilesIdentityBasedAuthenticationDirectoryServiceOptions);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainName);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesNetBiosDomainName);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesForestName);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainGuid);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesDomainSid);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAzureStorageSid);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesSamAccountName);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType), AzureFilesIdentityBasedAuthenticationActiveDirectoryPropertiesAccountType);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationDefaultSharePermission), AzureFilesIdentityBasedAuthenticationDefaultSharePermission);
        parameters.AddIfBound(this, nameof(AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled), AzureFilesIdentityBasedAuthenticationSmbOAuthSettingsIsSmbOAuthEnabled);
        parameters.AddIfBound(this, nameof(EnableHttpsTrafficOnly), EnableHttpsTrafficOnly);
        parameters.AddIfBound(this, nameof(IsSftpEnabled), IsSftpEnabled);
        parameters.AddIfBound(this, nameof(IsLocalUserEnabled), IsLocalUserEnabled);
        parameters.AddIfBound(this, nameof(EnableExtendedGroups), EnableExtendedGroups);
        parameters.AddIfBound(this, nameof(IsHnsEnabled), IsHnsEnabled);
        parameters.AddIfBound(this, nameof(LargeFileSharesState), LargeFileSharesState);
        parameters.AddIfBound(this, nameof(RoutingPreferenceRoutingChoice), RoutingPreferenceRoutingChoice);
        parameters.AddIfBound(this, nameof(RoutingPreferencePublishMicrosoftEndpoints), RoutingPreferencePublishMicrosoftEndpoints);
        parameters.AddIfBound(this, nameof(RoutingPreferencePublishInternetEndpoints), RoutingPreferencePublishInternetEndpoints);
        parameters.AddIfBound(this, nameof(DualStackEndpointPreferencePublishIpv6Endpoint), DualStackEndpointPreferencePublishIpv6Endpoint);
        parameters.AddIfBound(this, nameof(AllowBlobPublicAccess), AllowBlobPublicAccess);
        parameters.AddIfBound(this, nameof(MinimumTlsVersion), MinimumTlsVersion);
        parameters.AddIfBound(this, nameof(AllowSharedKeyAccess), AllowSharedKeyAccess);
        parameters.AddIfBound(this, nameof(EnableNfsV3), EnableNfsV3);
        parameters.AddIfBound(this, nameof(AllowCrossTenantReplication), AllowCrossTenantReplication);
        parameters.AddIfBound(this, nameof(DefaultToOAuthAuthentication), DefaultToOAuthAuthentication);
        parameters.AddIfBound(this, nameof(ImmutableStorageWithVersioningEnabled), ImmutableStorageWithVersioningEnabled);
        parameters.AddIfBound(this, nameof(ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays), ImmutableStorageWithVersioningImmutabilityPolicyImmutabilityPeriodSinceCreationInDays);
        parameters.AddIfBound(this, nameof(ImmutableStorageWithVersioningImmutabilityPolicyState), ImmutableStorageWithVersioningImmutabilityPolicyState);
        parameters.AddIfBound(this, nameof(ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites), ImmutableStorageWithVersioningImmutabilityPolicyAllowProtectedAppendWrites);
        parameters.AddIfBound(this, nameof(DnsEndpointType), DnsEndpointType);
        parameters.AddIfBound(this, nameof(GeoPriorityReplicationStatusIsBlobEnabled), GeoPriorityReplicationStatusIsBlobEnabled);
        parameters.AddIfBound(this, nameof(AllowSharedKeyAccessForServicesBlobEnabled), AllowSharedKeyAccessForServicesBlobEnabled);
        parameters.AddIfBound(this, nameof(AllowSharedKeyAccessForServicesFileEnabled), AllowSharedKeyAccessForServicesFileEnabled);
        parameters.AddIfBound(this, nameof(AllowSharedKeyAccessForServicesTableEnabled), AllowSharedKeyAccessForServicesTableEnabled);
        parameters.AddIfBound(this, nameof(AllowSharedKeyAccessForServicesQueueEnabled), AllowSharedKeyAccessForServicesQueueEnabled);
        parameters.AddIfBound(this, nameof(DataCollaborationPolicyPropertiesAllowStorageConnectors), DataCollaborationPolicyPropertiesAllowStorageConnectors);
        parameters.AddIfBound(this, nameof(DataCollaborationPolicyPropertiesAllowStorageDataShares), DataCollaborationPolicyPropertiesAllowStorageDataShares);
        parameters.AddIfBound(this, nameof(DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing), DataCollaborationPolicyPropertiesAllowCrossTenantDataSharing);
        parameters.AddIfBound(this, nameof(Profile), Profile);
        parameters.AddIfBound(this, nameof(BodyJsonPatch), BodyJsonPatch);

        CmdletEngineRunner.Run(this, "StorageAccountTsp", "New-AzStorageAccount", "StorageAccountTspStorageAccountError", parameters, _cts.Token, noWait: NoWait.IsPresent);
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
