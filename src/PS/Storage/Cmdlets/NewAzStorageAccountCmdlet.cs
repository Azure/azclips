using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageAccount")]
public class NewAzStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string SkuName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Kind { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? AccessTier { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableHttpsTrafficOnly { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowBlobPublicAccess { get; set; }

    [Parameter(Mandatory = false)]
    public string? MinimumTlsVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableHierarchicalNamespace { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableSftp { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableLocalUser { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableNfsV3 { get; set; }

    [Parameter(Mandatory = false)]
    public string? DnsEndpointType { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Zone { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? EncryptionService { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeySource { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyName { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVersion { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AssignIdentity { get; set; }

    [Parameter(Mandatory = false)]
    public string? IdentityType { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? UserAssignedIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultUserAssignedIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultFederatedClientId { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyTypeForTable { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyTypeForQueue { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter RequireInfrastructureEncryption { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableAzureActiveDirectoryDomainServicesForFile { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableActiveDirectoryDomainServicesForFile { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableAzureActiveDirectoryKerberosForFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryDomainName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryNetBiosDomainName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryForestName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryDomainGuid { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryDomainSid { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryAzureStorageSid { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectorySamAccountName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ActiveDirectoryAccountType { get; set; }

    [Parameter(Mandatory = false)]
    public string? DefaultSharePermission { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableSmbOAuth { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? NetworkBypass { get; set; }

    [Parameter(Mandatory = false)]
    public string? DefaultAction { get; set; }

    [Parameter(Mandatory = false)]
    public string? Subnet { get; set; }

    [Parameter(Mandatory = false)]
    public string? VirtualNetworkName { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? VirtualNetworkRule { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpRule { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ResourceAccessRule { get; set; }

    [Parameter(Mandatory = false)]
    public string? NetworkRuleAction { get; set; }

    [Parameter(Mandatory = false)]
    public string? RoutingChoice { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishMicrosoftEndpoint { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishInternetEndpoint { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    public string? ZonePlacementPolicy { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableBlobGeoPriorityReplication { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishIpv6Endpoint { get; set; }

    [Parameter(Mandatory = false)]
    public string? AllowedCopyScope { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowSharedKeyAccess { get; set; }

    [Parameter(Mandatory = false)]
    public TimeSpan? SasExpirationPeriod { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasExpirationAction { get; set; }

    [Parameter(Mandatory = false)]
    public int? KeyExpirationPeriodInDay { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowCrossTenantReplication { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableAccountLevelImmutability { get; set; }

    [Parameter(Mandatory = false)]
    public int? ImmutabilityPeriod { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImmutabilityPolicyState { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowProtectedAppendWrites { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableLargeFileShare { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageAccountError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"storage account '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "AccountName", Name },
            { nameof(ResourceGroupName), ResourceGroupName },
            { nameof(Location), Location },
            { nameof(SkuName), SkuName },
            { nameof(Kind), Kind },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add(nameof(SubscriptionId), SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag))) { parameters.Add("Tags", StorageCmdletArgumentConverter.ToStringDictionary(Tag!)); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AccessTier))) { parameters.Add(nameof(AccessTier), AccessTier!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableHttpsTrafficOnly))) { parameters.Add(nameof(EnableHttpsTrafficOnly), EnableHttpsTrafficOnly!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowBlobPublicAccess))) { parameters.Add(nameof(AllowBlobPublicAccess), AllowBlobPublicAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(MinimumTlsVersion))) { parameters.Add(nameof(MinimumTlsVersion), MinimumTlsVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicNetworkAccess))) { parameters.Add(nameof(PublicNetworkAccess), PublicNetworkAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableHierarchicalNamespace))) { parameters.Add(nameof(EnableHierarchicalNamespace), EnableHierarchicalNamespace!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableSftp))) { parameters.Add(nameof(EnableSftp), EnableSftp!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableLocalUser))) { parameters.Add(nameof(EnableLocalUser), EnableLocalUser!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableNfsV3))) { parameters.Add(nameof(EnableNfsV3), EnableNfsV3!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DnsEndpointType))) { parameters.Add(nameof(DnsEndpointType), DnsEndpointType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Zone))) { parameters.Add("Zones", Zone!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionService))) { parameters.Add("EncryptionServices", EncryptionService!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeySource))) { parameters.Add(nameof(EncryptionKeySource), EncryptionKeySource!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyName))) { parameters.Add("EncryptionKeyName", KeyName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVaultUri))) { parameters.Add("EncryptionKeyVault", KeyVaultUri!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVersion))) { parameters.Add("EncryptionKeyVersion", KeyVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AssignIdentity))) { parameters.Add(nameof(AssignIdentity), AssignIdentity.IsPresent); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IdentityType))) { parameters.Add(nameof(IdentityType), IdentityType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserAssignedIdentityId))) { parameters.Add("UserAssignedIdentityIds", UserAssignedIdentityId!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVaultUserAssignedIdentityId))) { parameters.Add(nameof(KeyVaultUserAssignedIdentityId), KeyVaultUserAssignedIdentityId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyVaultFederatedClientId))) { parameters.Add(nameof(KeyVaultFederatedClientId), KeyVaultFederatedClientId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyTypeForTable))) { parameters.Add(nameof(EncryptionKeyTypeForTable), EncryptionKeyTypeForTable!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeyTypeForQueue))) { parameters.Add(nameof(EncryptionKeyTypeForQueue), EncryptionKeyTypeForQueue!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RequireInfrastructureEncryption))) { parameters.Add(nameof(RequireInfrastructureEncryption), RequireInfrastructureEncryption.IsPresent); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableAzureActiveDirectoryDomainServicesForFile))) { parameters.Add("EnableFilesAadds", EnableAzureActiveDirectoryDomainServicesForFile!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableActiveDirectoryDomainServicesForFile))) { parameters.Add("EnableFilesAdds", EnableActiveDirectoryDomainServicesForFile!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableAzureActiveDirectoryKerberosForFile))) { parameters.Add("EnableFilesAadkerb", EnableAzureActiveDirectoryKerberosForFile!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryDomainName))) { parameters.Add("DomainName", ActiveDirectoryDomainName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryNetBiosDomainName))) { parameters.Add("NetBiosDomainName", ActiveDirectoryNetBiosDomainName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryForestName))) { parameters.Add("ForestName", ActiveDirectoryForestName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryDomainGuid))) { parameters.Add("DomainGuid", ActiveDirectoryDomainGuid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryDomainSid))) { parameters.Add("DomainSid", ActiveDirectoryDomainSid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryAzureStorageSid))) { parameters.Add("AzureStorageSid", ActiveDirectoryAzureStorageSid!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectorySamAccountName))) { parameters.Add("SamAccountName", ActiveDirectorySamAccountName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ActiveDirectoryAccountType))) { parameters.Add("AccountType", ActiveDirectoryAccountType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultSharePermission))) { parameters.Add(nameof(DefaultSharePermission), DefaultSharePermission!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableSmbOAuth))) { parameters.Add(nameof(EnableSmbOAuth), EnableSmbOAuth!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkBypass))) { parameters.Add("Bypass", NetworkBypass!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultAction))) { parameters.Add(nameof(DefaultAction), DefaultAction!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Subnet))) { parameters.Add(nameof(Subnet), Subnet!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VirtualNetworkName))) { parameters.Add(nameof(VirtualNetworkName), VirtualNetworkName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VirtualNetworkRule))) { parameters.Add("VirtualNetworkRules", VirtualNetworkRule!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IpRule))) { parameters.Add("IpRules", IpRule!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceAccessRule))) { parameters.Add("ResourceAccessRules", ResourceAccessRule!.ToList()); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NetworkRuleAction))) { parameters.Add(nameof(NetworkRuleAction), NetworkRuleAction!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RoutingChoice))) { parameters.Add(nameof(RoutingChoice), RoutingChoice!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishMicrosoftEndpoint))) { parameters.Add("PublishMicrosoftEndpoints", PublishMicrosoftEndpoint!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishInternetEndpoint))) { parameters.Add("PublishInternetEndpoints", PublishInternetEndpoint!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EdgeZone))) { parameters.Add(nameof(EdgeZone), EdgeZone!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ZonePlacementPolicy))) { parameters.Add(nameof(ZonePlacementPolicy), ZonePlacementPolicy!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableBlobGeoPriorityReplication))) { parameters.Add(nameof(EnableBlobGeoPriorityReplication), EnableBlobGeoPriorityReplication!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublishIpv6Endpoint))) { parameters.Add(nameof(PublishIpv6Endpoint), PublishIpv6Endpoint!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowedCopyScope))) { parameters.Add(nameof(AllowedCopyScope), AllowedCopyScope!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowSharedKeyAccess))) { parameters.Add(nameof(AllowSharedKeyAccess), AllowSharedKeyAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasExpirationPeriod))) { parameters.Add(nameof(SasExpirationPeriod), SasExpirationPeriod!.Value.ToString(@"d\.hh\:mm\:ss")); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasExpirationAction))) { parameters.Add(nameof(SasExpirationAction), SasExpirationAction!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeyExpirationPeriodInDay))) { parameters.Add("KeyExpirationPeriodInDays", KeyExpirationPeriodInDay!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowCrossTenantReplication))) { parameters.Add(nameof(AllowCrossTenantReplication), AllowCrossTenantReplication!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableAccountLevelImmutability))) { parameters.Add(nameof(EnableAccountLevelImmutability), EnableAccountLevelImmutability.IsPresent); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImmutabilityPeriod))) { parameters.Add("ImmutabilityPeriodSinceCreationInDays", ImmutabilityPeriod!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImmutabilityPolicyState))) { parameters.Add(nameof(ImmutabilityPolicyState), ImmutabilityPolicyState!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowProtectedAppendWrites))) { parameters.Add(nameof(AllowProtectedAppendWrites), AllowProtectedAppendWrites!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableLargeFileShare))) { parameters.Add(nameof(EnableLargeFileShare), EnableLargeFileShare.IsPresent); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add(nameof(Profile), Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "New-AzStorageAccount", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}