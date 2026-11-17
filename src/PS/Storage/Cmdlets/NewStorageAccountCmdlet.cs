// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Management.Models.StorageAccount")]
public class NewStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    [Alias("StorageAccountName", "AccountName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, Position = 3, ValueFromPipelineByPropertyName = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true, Position = 2, ValueFromPipelineByPropertyName = true)]
    [Alias("StorageAccountType", "AccountType", "Type")]
    [ValidateSet("Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Standard_ZRS", "Premium_LRS", "Premium_ZRS", "Standard_GZRS", "Standard_RAGZRS", "StandardV2_LRS", "StandardV2_GRS", "StandardV2_ZRS", "StandardV2_GZRS", "PremiumV2_LRS", "PremiumV2_ZRS", IgnoreCase = true)]
    public string SkuName { get; set; } = "";

    [Parameter(Mandatory = false)]
    [ValidateSet("Storage", "StorageV2", "BlobStorage", "FileStorage", "BlockBlobStorage", IgnoreCase = true)]
    [PSDefaultValue(Help = "StorageV2", Value = "StorageV2")]
    public string Kind { get; set; } = "StorageV2";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Hot", "Cool", "Premium", "Cold", "Smart", IgnoreCase = true)]
    public string? AccessTier { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("TLS1_0", "TLS1_1", "TLS1_2", "TLS1_3", IgnoreCase = true)]
    public string? MinimumTlsVersion { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Enabled", "Disabled", "SecuredByPerimeter", IgnoreCase = true)]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("PrivateLink", "AAD", "All", IgnoreCase = true)]
    public string? AllowedCopyScope { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableLargeFileShare { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Standard", "AzureDnsZone", IgnoreCase = true)]
    public string? DnsEndpointType { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableHttpsTrafficOnly { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowBlobPublicAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowSharedKeyAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableHierarchicalNamespace { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableSftp { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableLocalUser { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableExtendedGroups { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableNfsV3 { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowCrossTenantReplication { get; set; }

    [Parameter(Mandatory = false)]
    public bool? DefaultToOAuthAuthentication { get; set; }

    [Parameter(Mandatory = false)]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("SystemAssigned", "UserAssigned", "SystemAssignedUserAssigned", "None", IgnoreCase = true)]
    public string? IdentityType { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AssignIdentity { get; set; }

    [Parameter(Mandatory = false)]
    public string? UserAssignedIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Zone { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Any", "None", IgnoreCase = true)]
    public string? ZonePlacementPolicy { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("MicrosoftRouting", "InternetRouting", IgnoreCase = true)]
    public string? RoutingChoice { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishMicrosoftEndpoint { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishInternetEndpoint { get; set; }

    [Parameter(Mandatory = false)]
    public bool? PublishIpv6Endpoint { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableBlobGeoPriorityReplication { get; set; }

    [Parameter(Mandatory = false)]
    public int? KeyExpirationPeriodInDay { get; set; }

    [Parameter(Mandatory = false)]
    public TimeSpan? SasExpirationPeriod { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Log", "Block", IgnoreCase = true)]
    public string? SasExpirationAction { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter EnableAccountLevelImmutability { get; set; }

    [Parameter(Mandatory = false)]
    public int? ImmutabilityPeriod { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Unlocked", "Locked", "Disabled", IgnoreCase = true)]
    public string? ImmutabilityPolicyState { get; set; }

    [Parameter(Mandatory = false)]
    public bool? AllowProtectedAppendWrites { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Bypass { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Allow", "Deny", IgnoreCase = true)]
    public string? DefaultAction { get; set; }

    [Parameter(Mandatory = false)]
    public string? Subnet { get; set; }

    [Parameter(Mandatory = false)]
    public string? VnetName { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Allow", IgnoreCase = true)]
    public string? Action { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? IpAddress { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Ipv6Address { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableAzureActiveDirectoryDomainServicesForFile { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableAzureActiveDirectoryKerberosForFile { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableActiveDirectoryDomainServicesForFile { get; set; }

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
    [ValidateSet("User", "Computer", IgnoreCase = true)]
    public string? ActiveDirectoryAccountType { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("None", "StorageFileDataSmbShareReader", "StorageFileDataSmbShareContributor", "StorageFileDataSmbShareElevatedContributor", IgnoreCase = true)]
    public string? DefaultSharePermission { get; set; }

    [Parameter(Mandatory = false)]
    public bool? EnableSmbOAuth { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Microsoft.Storage", "Microsoft.Keyvault", IgnoreCase = true)]
    public string? EncryptionKeySource { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyName { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVersion { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter RequireInfrastructureEncryption { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultUserAssignedIdentityId { get; set; }

    [Parameter(Mandatory = false)]
    public string? KeyVaultFederatedClientId { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Account", "Service", IgnoreCase = true)]
    public string? EncryptionKeyTypeForTable { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Account", "Service", IgnoreCase = true)]
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
        if (!ShouldProcess($"Storage account '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = BuildInvocationParameters(MyInvocation.BoundParameters);

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "New-AzStorageAccount", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    /// <summary>
    /// Maps the Az.Storage-named parameters onto the arguments the storage processors bind, which
    /// keep the CLI names. Only bound parameters are sent, so unset options keep their service default.
    /// </summary>
    internal Dictionary<string, object> BuildInvocationParameters(IDictionary<string, object> boundParameters)
    {
        var parameters = new Dictionary<string, object>();
        parameters.Add("ResourceGroupName", ResourceGroupName);
        parameters.Add("AccountName", Name);
        parameters.Add("Location", Location);
        parameters.Add("SkuName", SkuName);
        parameters.Add("Kind", Kind);
        if (boundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId); }
        if (Tag is not null) { parameters.Add("Tags", StorageCmdletArgumentConverter.ToTagArguments(Tag)); }
        if (boundParameters.ContainsKey(nameof(AccessTier))) { parameters.Add("AccessTier", AccessTier!); }
        if (boundParameters.ContainsKey(nameof(MinimumTlsVersion))) { parameters.Add("MinimumTlsVersion", MinimumTlsVersion!); }
        if (boundParameters.ContainsKey(nameof(PublicNetworkAccess))) { parameters.Add("PublicNetworkAccess", PublicNetworkAccess!); }
        if (boundParameters.ContainsKey(nameof(AllowedCopyScope))) { parameters.Add("AllowedCopyScope", AllowedCopyScope!); }
        if (EnableLargeFileShare.IsPresent) { parameters.Add("LargeFileSharesState", "Enabled"); }
        if (boundParameters.ContainsKey(nameof(DnsEndpointType))) { parameters.Add("DnsEndpointType", DnsEndpointType!); }
        if (boundParameters.ContainsKey(nameof(EnableHttpsTrafficOnly))) { parameters.Add("SupportsHttpsTrafficOnly", EnableHttpsTrafficOnly!); }
        if (boundParameters.ContainsKey(nameof(AllowBlobPublicAccess))) { parameters.Add("AllowBlobPublicAccess", AllowBlobPublicAccess!); }
        if (boundParameters.ContainsKey(nameof(AllowSharedKeyAccess))) { parameters.Add("AllowSharedKeyAccess", AllowSharedKeyAccess!); }
        if (boundParameters.ContainsKey(nameof(EnableHierarchicalNamespace))) { parameters.Add("IsHnsEnabled", EnableHierarchicalNamespace!); }
        if (boundParameters.ContainsKey(nameof(EnableSftp))) { parameters.Add("IsSftpEnabled", EnableSftp!); }
        if (boundParameters.ContainsKey(nameof(EnableLocalUser))) { parameters.Add("IsLocalUserEnabled", EnableLocalUser!); }
        if (boundParameters.ContainsKey(nameof(EnableExtendedGroups))) { parameters.Add("EnableExtendedGroups", EnableExtendedGroups!); }
        if (boundParameters.ContainsKey(nameof(EnableNfsV3))) { parameters.Add("IsNfsV3Enabled", EnableNfsV3!); }
        if (boundParameters.ContainsKey(nameof(AllowCrossTenantReplication))) { parameters.Add("AllowCrossTenantReplication", AllowCrossTenantReplication!); }
        if (boundParameters.ContainsKey(nameof(DefaultToOAuthAuthentication))) { parameters.Add("DefaultToOAuthAuthentication", DefaultToOAuthAuthentication!); }
        if (boundParameters.ContainsKey(nameof(EdgeZone))) { parameters.Add("EdgeZone", EdgeZone!); }
        // Like Az.Storage, -AssignIdentity alone asks for a system-assigned identity; an explicit -IdentityType wins.
        if (boundParameters.ContainsKey(nameof(IdentityType))) { parameters.Add("IdentityType", StorageCmdletArgumentConverter.ToIdentityType(IdentityType!)); }
        else if (AssignIdentity.IsPresent) { parameters.Add("IdentityType", "SystemAssigned"); }
        if (boundParameters.ContainsKey(nameof(UserAssignedIdentityId))) { parameters.Add("UserIdentityId", UserAssignedIdentityId!); }
        if (boundParameters.ContainsKey(nameof(Zone))) { parameters.Add("Zones", Zone!); }
        if (boundParameters.ContainsKey(nameof(ZonePlacementPolicy))) { parameters.Add("ZonePlacementPolicy", ZonePlacementPolicy!); }
        if (boundParameters.ContainsKey(nameof(RoutingChoice))) { parameters.Add("RoutingChoice", RoutingChoice!); }
        if (boundParameters.ContainsKey(nameof(PublishMicrosoftEndpoint))) { parameters.Add("PublishMicrosoftEndpoints", PublishMicrosoftEndpoint!); }
        if (boundParameters.ContainsKey(nameof(PublishInternetEndpoint))) { parameters.Add("PublishInternetEndpoints", PublishInternetEndpoint!); }
        if (boundParameters.ContainsKey(nameof(PublishIpv6Endpoint))) { parameters.Add("PublishIpv6Endpoint", PublishIpv6Endpoint!); }
        if (boundParameters.ContainsKey(nameof(EnableBlobGeoPriorityReplication))) { parameters.Add("EnableBlobGeoPriorityReplication", EnableBlobGeoPriorityReplication!); }
        if (boundParameters.ContainsKey(nameof(KeyExpirationPeriodInDay))) { parameters.Add("KeyExpirationPeriodInDays", KeyExpirationPeriodInDay!); }
        if (SasExpirationPeriod is { } sasExpirationPeriod) { parameters.Add("SasExpirationPeriod", StorageCmdletArgumentConverter.ToSasExpirationPeriod(sasExpirationPeriod)); }
        if (boundParameters.ContainsKey(nameof(SasExpirationAction))) { parameters.Add("SasExpirationAction", SasExpirationAction!); }
        if (EnableAccountLevelImmutability.IsPresent) { parameters.Add("EnableAlw", true); }
        if (boundParameters.ContainsKey(nameof(ImmutabilityPeriod))) { parameters.Add("ImmutabilityPeriodSinceCreationInDays", ImmutabilityPeriod!); }
        if (boundParameters.ContainsKey(nameof(ImmutabilityPolicyState))) { parameters.Add("ImmutabilityPolicyState", ImmutabilityPolicyState!); }
        if (boundParameters.ContainsKey(nameof(AllowProtectedAppendWrites))) { parameters.Add("AllowProtectedAppendWrites", AllowProtectedAppendWrites!); }
        if (boundParameters.ContainsKey(nameof(Bypass))) { parameters.Add("Bypass", Bypass!); }
        if (boundParameters.ContainsKey(nameof(DefaultAction))) { parameters.Add("DefaultAction", DefaultAction!); }
        if (boundParameters.ContainsKey(nameof(Subnet))) { parameters.Add("Subnet", Subnet!); }
        if (boundParameters.ContainsKey(nameof(VnetName))) { parameters.Add("VnetName", VnetName!); }
        if (boundParameters.ContainsKey(nameof(Action))) { parameters.Add("Action", Action!); }
        if (boundParameters.ContainsKey(nameof(IpAddress))) { parameters.Add("IpAddress", IpAddress!); }
        if (boundParameters.ContainsKey(nameof(Ipv6Address))) { parameters.Add("Ipv6Address", Ipv6Address!); }
        if (boundParameters.ContainsKey(nameof(EnableAzureActiveDirectoryDomainServicesForFile))) { parameters.Add("EnableFilesAadds", EnableAzureActiveDirectoryDomainServicesForFile!); }
        if (boundParameters.ContainsKey(nameof(EnableAzureActiveDirectoryKerberosForFile))) { parameters.Add("EnableFilesAadkerb", EnableAzureActiveDirectoryKerberosForFile!); }
        if (boundParameters.ContainsKey(nameof(EnableActiveDirectoryDomainServicesForFile))) { parameters.Add("EnableFilesAdds", EnableActiveDirectoryDomainServicesForFile!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryDomainName))) { parameters.Add("DomainName", ActiveDirectoryDomainName!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryNetBiosDomainName))) { parameters.Add("NetBiosDomainName", ActiveDirectoryNetBiosDomainName!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryForestName))) { parameters.Add("ForestName", ActiveDirectoryForestName!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryDomainGuid))) { parameters.Add("DomainGuid", ActiveDirectoryDomainGuid!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryDomainSid))) { parameters.Add("DomainSid", ActiveDirectoryDomainSid!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryAzureStorageSid))) { parameters.Add("AzureStorageSid", ActiveDirectoryAzureStorageSid!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectorySamAccountName))) { parameters.Add("SamAccountName", ActiveDirectorySamAccountName!); }
        if (boundParameters.ContainsKey(nameof(ActiveDirectoryAccountType))) { parameters.Add("AccountType", ActiveDirectoryAccountType!); }
        if (boundParameters.ContainsKey(nameof(DefaultSharePermission))) { parameters.Add("DefaultSharePermission", DefaultSharePermission!); }
        if (boundParameters.ContainsKey(nameof(EnableSmbOAuth))) { parameters.Add("EnableSmbOauth", EnableSmbOAuth!); }
        if (boundParameters.ContainsKey(nameof(EncryptionKeySource))) { parameters.Add("EncryptionKeySource", EncryptionKeySource!); }
        if (boundParameters.ContainsKey(nameof(KeyVaultUri))) { parameters.Add("EncryptionKeyVault", KeyVaultUri!); }
        if (boundParameters.ContainsKey(nameof(KeyName))) { parameters.Add("EncryptionKeyName", KeyName!); }
        if (boundParameters.ContainsKey(nameof(KeyVersion))) { parameters.Add("EncryptionKeyVersion", KeyVersion!); }
        if (RequireInfrastructureEncryption.IsPresent) { parameters.Add("RequireInfrastructureEncryption", true); }
        if (boundParameters.ContainsKey(nameof(KeyVaultUserAssignedIdentityId))) { parameters.Add("KeyVaultUserIdentityId", KeyVaultUserAssignedIdentityId!); }
        if (boundParameters.ContainsKey(nameof(KeyVaultFederatedClientId))) { parameters.Add("KeyVaultFederatedClientId", KeyVaultFederatedClientId!); }
        if (boundParameters.ContainsKey(nameof(EncryptionKeyTypeForTable))) { parameters.Add("EncryptionKeyTypeForTable", EncryptionKeyTypeForTable!); }
        if (boundParameters.ContainsKey(nameof(EncryptionKeyTypeForQueue))) { parameters.Add("EncryptionKeyTypeForQueue", EncryptionKeyTypeForQueue!); }
        if (boundParameters.ContainsKey(nameof(CustomDomainName))) { parameters.Add("CustomDomainName", CustomDomainName!); }

        if (boundParameters.ContainsKey(nameof(Profile))) { parameters.Add("Profile", Profile!); }

        return parameters;
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
