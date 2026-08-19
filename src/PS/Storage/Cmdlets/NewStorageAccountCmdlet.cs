// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class NewStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false, HelpMessage = "Use the named profile for this command (overrides the active profile).")]
    public string? Profile { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Azure Subscription Id. Defaults to the subscription of the active profile.")]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = true, HelpMessage = "The resource group name.")]
    public string? ResourceGroupName { get; set; }

    [Parameter(Mandatory = true, HelpMessage = "The name of the storage account. Must be between 3 and 24 characters in length and use numbers and lower-case letters only.")]
    public string? AccountName { get; set; }

    [Parameter(Mandatory = true, HelpMessage = "The Azure region in which to create the storage account.")]
    public string? Location { get; set; }

    [Parameter(Mandatory = true, HelpMessage = "The SKU name of the storage account.")]
    public string? SkuName { get; set; }

    [Parameter(Mandatory = true, HelpMessage = "The type of storage account.")]
    public string? Kind { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Resource tags in KEY=VALUE form (space-separated).")]
    public string[]? Tags { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The access tier used for billing. Required when kind is BlobStorage.")]
    public string? AccessTier { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The minimum TLS version permitted on requests to storage. The service default is TLS1_0.")]
    public string? MinimumTlsVersion { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow, disallow, or let Network Security Perimeter configuration evaluate public network access.")]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Restrict copy to and from storage accounts within an AAD tenant or with private links to the same VNet.")]
    public string? AllowedCopyScope { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow large file shares. Cannot be disabled once enabled.")]
    public string? LargeFileSharesState { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The type of endpoint. AzureDnsZone allows creating a large number of accounts in a single subscription.")]
    public string? DnsEndpointType { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow https traffic only to the storage service. The service default is true.")]
    public bool? SupportsHttpsTrafficOnly { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow or disallow public access to all blobs or containers. The service default is false.")]
    public bool? AllowBlobPublicAccess { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether the account permits requests authorized with the account access key. The service default is true.")]
    public bool? AllowSharedKeyAccess { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable a hierarchical namespace (Azure Data Lake Storage Gen2).")]
    public bool? IsHnsEnabled { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable Secure File Transfer Protocol.")]
    public bool? IsSftpEnabled { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable the local users feature.")]
    public bool? IsLocalUserEnabled { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable extended group support with the local users feature.")]
    public bool? EnableExtendedGroups { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable NFS 3.0 protocol support.")]
    public bool? IsNfsV3Enabled { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow or disallow cross AAD tenant object replication. The service default is false.")]
    public bool? AllowCrossTenantReplication { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether the default authentication is OAuth. The service default is false.")]
    public bool? DefaultToOAuthAuthentication { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the edge zone to create the account in.")]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The managed identity type for the account.")]
    public string? IdentityType { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The ARM resource identifier of the user-assigned identity. Only one is permitted.")]
    public string? UserIdentityId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The availability zones to pin the account to (space-separated).")]
    public string[]? Zones { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The availability zone pinning policy for the account.")]
    public string? ZonePlacementPolicy { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The kind of network routing used to deliver user data.")]
    public string? RoutingChoice { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether Microsoft routing storage endpoints are published.")]
    public bool? PublishMicrosoftEndpoints { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether internet routing storage endpoints are published.")]
    public bool? PublishInternetEndpoints { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether IPv6 storage endpoints are published.")]
    public bool? PublishIpv6Endpoint { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether Blob Geo Priority Replication is enabled for the account.")]
    public bool? EnableBlobGeoPriorityReplication { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Expiration period in days of the key policy assigned to the account.")]
    public int? KeyExpirationPeriodInDays { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Expiration period of the SAS policy assigned to the account, in DD.HH:MM:SS form.")]
    public string? SasExpirationPeriod { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The action performed when the SAS expiration period is violated. Requires --sas-expiration-period. Defaults to Log.")]
    public string? SasExpirationAction { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable account-level immutability. Can only be set at creation time.")]
    public bool? EnableAlw { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The immutability period for blobs since policy creation, in days (1-146000). Requires --enable-alw true.")]
    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The mode of the account-level immutability policy. Requires --enable-alw true.")]
    public string? ImmutabilityPolicyState { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Allow new blocks to be written to an append blob while maintaining immutability protection. Requires --enable-alw true.")]
    public bool? AllowProtectedAppendWrites { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Traffic to bypass the network rules (space-separated): None, Logging, Metrics, AzureServices. Requires --default-action.")]
    public string[]? Bypass { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The action to apply when no network rule matches. Required when any other network rule flag is used.")]
    public string? DefaultAction { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Name or resource id of a subnet. If a name is given, --vnet-name is required.")]
    public string? Subnet { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Name of the virtual network containing --subnet.")]
    public string? VnetName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The action applied to the subnet and IP rules. Defaults to Allow.")]
    public string? Action { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "IPv4 addresses or CIDR ranges to allow (space-separated). Requires --default-action.")]
    public string[]? IpAddress { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "IPv6 addresses or CIDR ranges to allow (space-separated). Requires --default-action.")]
    public string[]? Ipv6Address { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable Microsoft Entra Domain Services authentication for Azure Files.")]
    public bool? EnableFilesAadds { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable Microsoft Entra Kerberos authentication for Azure Files.")]
    public bool? EnableFilesAadkerb { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable Active Directory Domain Services authentication for Azure Files. Requires all Active Directory properties.")]
    public bool? EnableFilesAdds { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The primary domain the AD DNS server is authoritative for.")]
    public string? DomainName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The NetBIOS domain name.")]
    public string? NetBiosDomainName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The Active Directory forest to get.")]
    public string? ForestName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The domain GUID.")]
    public string? DomainGuid { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The security identifier (SID) of the AD domain.")]
    public string? DomainSid { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The security identifier (SID) for Azure Storage.")]
    public string? AzureStorageSid { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The Active Directory SAMAccountName for Azure Storage.")]
    public string? SamAccountName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The Active Directory account type for Azure Storage.")]
    public string? AccountType { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The default share permission for users using Kerberos authentication when no RBAC role is assigned.")]
    public string? DefaultSharePermission { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether managed identities can access SMB shares using OAuth.")]
    public bool? EnableSmbOauth { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The encryption key source. Inferred as Microsoft.Keyvault when key vault options are supplied.")]
    public string? EncryptionKeySource { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The URI of the key vault. Must be given together with --encryption-key-name.")]
    public string? EncryptionKeyVault { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the key vault key. Must be given together with --encryption-key-vault.")]
    public string? EncryptionKeyName { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The version of the key vault key. Opts out of implicit key rotation.")]
    public string? EncryptionKeyVersion { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether the service applies a secondary layer of encryption with platform managed keys.")]
    public bool? RequireInfrastructureEncryption { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Resource id of the user-assigned identity used for server-side encryption.")]
    public string? KeyVaultUserIdentityId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Client id of the multi-tenant application used for cross-tenant customer-managed-key encryption.")]
    public string? KeyVaultFederatedClientId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The encryption key type for the Table service.")]
    public string? EncryptionKeyTypeForTable { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The encryption key type for the Queue service.")]
    public string? EncryptionKeyTypeForQueue { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The custom domain name assigned to the account. Name is the CNAME source.")]
    public string? CustomDomainName { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId!); }
        parameters.Add("ResourceGroupName", ResourceGroupName!);
        parameters.Add("AccountName", AccountName!);
        parameters.Add("Location", Location!);
        parameters.Add("SkuName", SkuName!);
        parameters.Add("Kind", Kind!);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags))) { parameters.Add("Tags", Tags!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AccessTier))) { parameters.Add("AccessTier", AccessTier!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(MinimumTlsVersion))) { parameters.Add("MinimumTlsVersion", MinimumTlsVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicNetworkAccess))) { parameters.Add("PublicNetworkAccess", PublicNetworkAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowedCopyScope))) { parameters.Add("AllowedCopyScope", AllowedCopyScope!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(LargeFileSharesState))) { parameters.Add("LargeFileSharesState", LargeFileSharesState!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DnsEndpointType))) { parameters.Add("DnsEndpointType", DnsEndpointType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SupportsHttpsTrafficOnly))) { parameters.Add("SupportsHttpsTrafficOnly", SupportsHttpsTrafficOnly!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowBlobPublicAccess))) { parameters.Add("AllowBlobPublicAccess", AllowBlobPublicAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowSharedKeyAccess))) { parameters.Add("AllowSharedKeyAccess", AllowSharedKeyAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IsHnsEnabled))) { parameters.Add("IsHnsEnabled", IsHnsEnabled!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IsSftpEnabled))) { parameters.Add("IsSftpEnabled", IsSftpEnabled!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IsLocalUserEnabled))) { parameters.Add("IsLocalUserEnabled", IsLocalUserEnabled!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableExtendedGroups))) { parameters.Add("EnableExtendedGroups", EnableExtendedGroups!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IsNfsV3Enabled))) { parameters.Add("IsNfsV3Enabled", IsNfsV3Enabled!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowCrossTenantReplication))) { parameters.Add("AllowCrossTenantReplication", AllowCrossTenantReplication!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultToOAuthAuthentication))) { parameters.Add("DefaultToOAuthAuthentication", DefaultToOAuthAuthentication!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EdgeZone))) { parameters.Add("EdgeZone", EdgeZone!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IdentityType))) { parameters.Add("IdentityType", IdentityType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserIdentityId))) { parameters.Add("UserIdentityId", UserIdentityId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Zones))) { parameters.Add("Zones", Zones!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ZonePlacementPolicy))) { parameters.Add("ZonePlacementPolicy", ZonePlacementPolicy!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RoutingChoice))) { parameters.Add("RoutingChoice", RoutingChoice!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishMicrosoftEndpoints))) { parameters.Add("PublishMicrosoftEndpoints", PublishMicrosoftEndpoints!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishInternetEndpoints))) { parameters.Add("PublishInternetEndpoints", PublishInternetEndpoints!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishIpv6Endpoint))) { parameters.Add("PublishIpv6Endpoint", PublishIpv6Endpoint!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableBlobGeoPriorityReplication))) { parameters.Add("EnableBlobGeoPriorityReplication", EnableBlobGeoPriorityReplication!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyExpirationPeriodInDays))) { parameters.Add("KeyExpirationPeriodInDays", KeyExpirationPeriodInDays!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasExpirationPeriod))) { parameters.Add("SasExpirationPeriod", SasExpirationPeriod!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasExpirationAction))) { parameters.Add("SasExpirationAction", SasExpirationAction!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableAlw))) { parameters.Add("EnableAlw", EnableAlw!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImmutabilityPeriodSinceCreationInDays))) { parameters.Add("ImmutabilityPeriodSinceCreationInDays", ImmutabilityPeriodSinceCreationInDays!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImmutabilityPolicyState))) { parameters.Add("ImmutabilityPolicyState", ImmutabilityPolicyState!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowProtectedAppendWrites))) { parameters.Add("AllowProtectedAppendWrites", AllowProtectedAppendWrites!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Bypass))) { parameters.Add("Bypass", Bypass!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultAction))) { parameters.Add("DefaultAction", DefaultAction!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Subnet))) { parameters.Add("Subnet", Subnet!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VnetName))) { parameters.Add("VnetName", VnetName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Action))) { parameters.Add("Action", Action!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IpAddress))) { parameters.Add("IpAddress", IpAddress!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Ipv6Address))) { parameters.Add("Ipv6Address", Ipv6Address!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableFilesAadds))) { parameters.Add("EnableFilesAadds", EnableFilesAadds!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableFilesAadkerb))) { parameters.Add("EnableFilesAadkerb", EnableFilesAadkerb!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableFilesAdds))) { parameters.Add("EnableFilesAdds", EnableFilesAdds!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DomainName))) { parameters.Add("DomainName", DomainName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetBiosDomainName))) { parameters.Add("NetBiosDomainName", NetBiosDomainName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForestName))) { parameters.Add("ForestName", ForestName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DomainGuid))) { parameters.Add("DomainGuid", DomainGuid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DomainSid))) { parameters.Add("DomainSid", DomainSid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AzureStorageSid))) { parameters.Add("AzureStorageSid", AzureStorageSid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SamAccountName))) { parameters.Add("SamAccountName", SamAccountName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AccountType))) { parameters.Add("AccountType", AccountType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultSharePermission))) { parameters.Add("DefaultSharePermission", DefaultSharePermission!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableSmbOauth))) { parameters.Add("EnableSmbOauth", EnableSmbOauth!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeySource))) { parameters.Add("EncryptionKeySource", EncryptionKeySource!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyVault))) { parameters.Add("EncryptionKeyVault", EncryptionKeyVault!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyName))) { parameters.Add("EncryptionKeyName", EncryptionKeyName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyVersion))) { parameters.Add("EncryptionKeyVersion", EncryptionKeyVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RequireInfrastructureEncryption))) { parameters.Add("RequireInfrastructureEncryption", RequireInfrastructureEncryption!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVaultUserIdentityId))) { parameters.Add("KeyVaultUserIdentityId", KeyVaultUserIdentityId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVaultFederatedClientId))) { parameters.Add("KeyVaultFederatedClientId", KeyVaultFederatedClientId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyTypeForTable))) { parameters.Add("EncryptionKeyTypeForTable", EncryptionKeyTypeForTable!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyTypeForQueue))) { parameters.Add("EncryptionKeyTypeForQueue", EncryptionKeyTypeForQueue!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(CustomDomainName))) { parameters.Add("CustomDomainName", CustomDomainName!); }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "New-AzStorageAccount", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
