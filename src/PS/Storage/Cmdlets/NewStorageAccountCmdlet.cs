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

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string SkuName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Kind { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? Tags { get; set; }

    [Parameter(Mandatory = false)]
    public string? AccessTier { get; set; }

    [Parameter(Mandatory = false)]
    public string? MinimumTlsVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false)]
    public string? AllowedCopyScope { get; set; }

    [Parameter(Mandatory = false)]
    public string? LargeFileSharesState { get; set; }

    [Parameter(Mandatory = false)]
    public string? DnsEndpointType { get; set; }

    [Parameter(Mandatory = false)]
    public bool? SupportsHttpsTrafficOnly { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowBlobPublicAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowSharedKeyAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool? IsHnsEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? IsSftpEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? IsLocalUserEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableExtendedGroups { get; set; }

    [Parameter(Mandatory = false)]
    public bool? IsNfsV3Enabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowCrossTenantReplication { get; set; }

    [Parameter(Mandatory = false)]
    public bool? DefaultToOAuthAuthentication { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    public string? IdentityType { get; set; }

    [Parameter(Mandatory = false)]
    public string? UserIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Zones { get; set; }

    [Parameter(Mandatory = false)]
    public string? ZonePlacementPolicy { get; set; }

    [Parameter(Mandatory = false)]
    public string? RoutingChoice { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishMicrosoftEndpoints { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishInternetEndpoints { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishIpv6Endpoint { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableBlobGeoPriorityReplication { get; set; }

    [Parameter(Mandatory = false)]
    public int? KeyExpirationPeriodInDays { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasExpirationPeriod { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasExpirationAction { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableAlw { get; set; }

    [Parameter(Mandatory = false)]
    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImmutabilityPolicyState { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowProtectedAppendWrites { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Bypass { get; set; }

    [Parameter(Mandatory = false)]
    public string? DefaultAction { get; set; }

    [Parameter(Mandatory = false)]
    public string? Subnet { get; set; }

    [Parameter(Mandatory = false)]
    public string? VnetName { get; set; }

    [Parameter(Mandatory = false)]
    public string? Action { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpAddress { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Ipv6Address { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableFilesAadds { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableFilesAadkerb { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableFilesAdds { get; set; }

    [Parameter(Mandatory = false)]
    public string? DomainName { get; set; }

    [Parameter(Mandatory = false)]
    public string? NetBiosDomainName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ForestName { get; set; }

    [Parameter(Mandatory = false)]
    public string? DomainGuid { get; set; }

    [Parameter(Mandatory = false)]
    public string? DomainSid { get; set; }

    [Parameter(Mandatory = false)]
    public string? AzureStorageSid { get; set; }

    [Parameter(Mandatory = false)]
    public string? SamAccountName { get; set; }

    [Parameter(Mandatory = false)]
    public string? AccountType { get; set; }

    [Parameter(Mandatory = false)]
    public string? DefaultSharePermission { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableSmbOauth { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeySource { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyVault { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyName { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyVersion { get; set; }

    [Parameter(Mandatory = false)]
    public bool? RequireInfrastructureEncryption { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultUserIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultFederatedClientId { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyTypeForTable { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionKeyTypeForQueue { get; set; }

    [Parameter(Mandatory = false)]
    public string? CustomDomainName { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Storage account '{AccountName}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId); }
        parameters.Add("ResourceGroupName", ResourceGroupName);
        parameters.Add("AccountName", AccountName);
        parameters.Add("Location", Location);
        parameters.Add("SkuName", SkuName);
        parameters.Add("Kind", Kind);
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
