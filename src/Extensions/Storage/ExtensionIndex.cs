using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage;

[ExtensionVerb("storage")]
public class ExtensionIndex : IExtensionIndex
{
    private const string AssemblyName = "Microsoft.Azure.Azclips.Extensions.Storage";

    private static readonly CommandMetadata s_newStorageAccount_PS = new(
        Name: nameof(CreateStorageAccountsProcessor_Orchestrator),
        InvocationName: "New-AzStorageAccount",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.CreateStorageAccountsProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(CreateStorageAccountsProcessor_Orchestrator),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Location"] = new("Location", "Location", typeof(string), "Location for the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["SkuName"] = new("SkuName", "SkuName", typeof(string), "The storage account SKU name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Kind"] = new("Kind", "Kind", typeof(string), "The storage account kind.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Tag"] = new("Tags", "Tag", typeof(Dictionary<string, string>), "Tags for the storage account.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["AccessTier"] = new("AccessTier", "AccessTier", typeof(string), "The access tier for BlobStorage or StorageV2 accounts.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableHttpsTrafficOnly"] = new("EnableHttpsTrafficOnly", "EnableHttpsTrafficOnly", typeof(bool), "Allows HTTPS traffic only to the storage service.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess", "AllowBlobPublicAccess", typeof(bool), "Allow or disallow public access to blobs or containers.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["MinimumTlsVersion"] = new("MinimumTlsVersion", "MinimumTlsVersion", typeof(string), "The minimum TLS version permitted on requests to storage.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["PublicNetworkAccess"] = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "Public network access setting.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableHierarchicalNamespace"] = new("EnableHierarchicalNamespace", "EnableHierarchicalNamespace", typeof(bool), "Enable hierarchical namespace.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableSftp"] = new("EnableSftp", "EnableSftp", typeof(bool), "Enable SFTP.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableLocalUser"] = new("EnableLocalUser", "EnableLocalUser", typeof(bool), "Enable local users.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableNfsV3"] = new("EnableNfsV3", "EnableNfsV3", typeof(bool), "Enable NFS 3.0 protocol support.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["DnsEndpointType"] = new("DnsEndpointType", "DnsEndpointType", typeof(string), "The DNS endpoint type.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["Zone"] = new("Zones", "Zone", typeof(List<string>), "Pinned logical availability zones.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_getStorageAccount_PS = new(
        Name: nameof(GetPropertiesStorageAccountsProcessor),
        InvocationName: "Get-AzStorageAccount_Get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.GetPropertiesStorageAccountsProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(GetPropertiesStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Expand"] = new("Expand", "Expand", typeof(string), "The storage account properties to expand.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_listStorageAccount_PS = new(
        Name: nameof(ListStorageAccountsCommand),
        InvocationName: "Get-AzStorageAccount_List",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageAccountsCommand",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(ListStorageAccountsCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_removeStorageAccount_PS = new(
        Name: nameof(DeleteStorageAccountsProcessor),
        InvocationName: "Remove-AzStorageAccount",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountsProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(DeleteStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_newStorageAccount_CLI = new(
        Name: nameof(CreateStorageAccountsProcessor_Orchestrator),
        InvocationName: "storage account create",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.CreateStorageAccountsProcessor_Orchestrator",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(CreateStorageAccountsProcessor_Orchestrator),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["location"] = new("Location", "location", typeof(string), "Location for the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["sku"] = new("SkuName", "sku", typeof(string), "The storage account SKU name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["kind"] = new("Kind", "kind", typeof(string), "The storage account kind.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["tags"] = new("Tags", "tags", typeof(Dictionary<string, string>), "Tags for the storage account.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "The access tier for BlobStorage or StorageV2 accounts.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["https-only"] = new("EnableHttpsTrafficOnly", "https-only", typeof(bool), "Allows HTTPS traffic only to the storage service.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["allow-blob-public-access"] = new("AllowBlobPublicAccess", "allow-blob-public-access", typeof(bool), "Allow or disallow public access to blobs or containers.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["min-tls-version"] = new("MinimumTlsVersion", "min-tls-version", typeof(string), "The minimum TLS version permitted on requests to storage.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["public-network-access"] = new("PublicNetworkAccess", "public-network-access", typeof(string), "Public network access setting.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["hns"] = new("EnableHierarchicalNamespace", "hns", typeof(bool), "Enable hierarchical namespace.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-sftp"] = new("EnableSftp", "enable-sftp", typeof(bool), "Enable SFTP.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-local-user"] = new("EnableLocalUser", "enable-local-user", typeof(bool), "Enable local users.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-nfs-v3"] = new("EnableNfsV3", "enable-nfs-v3", typeof(bool), "Enable NFS 3.0 protocol support.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["dns-endpoint-type"] = new("DnsEndpointType", "dns-endpoint-type", typeof(string), "The DNS endpoint type.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["zone"] = new("Zones", "zone", typeof(List<string>), "Pinned logical availability zones.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_getStorageAccount_CLI = new(
        Name: nameof(GetPropertiesStorageAccountsProcessor),
        InvocationName: "storage account show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.GetPropertiesStorageAccountsProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(GetPropertiesStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["expand"] = new("Expand", "expand", typeof(string), "The storage account properties to expand.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_listStorageAccount_CLI = new(
        Name: nameof(ListStorageAccountsCommand),
        InvocationName: "storage account list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageAccountsCommand",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(ListStorageAccountsCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_deleteStorageAccount_CLI = new(
        Name: nameof(DeleteStorageAccountsProcessor),
        InvocationName: "storage account delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountsProcessor",
        AssemblyName: AssemblyName,
        ProcessorType: typeof(DeleteStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
    {
        ["New-AzStorageAccount"] = s_newStorageAccount_PS,
        ["Get-AzStorageAccount_Get"] = s_getStorageAccount_PS,
        ["Get-AzStorageAccount_List"] = s_listStorageAccount_PS,
        ["Remove-AzStorageAccount"] = s_removeStorageAccount_PS,
    };

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["storage account create"] = s_newStorageAccount_CLI,
        ["storage account show"] = s_getStorageAccount_CLI,
        ["storage account list"] = s_listStorageAccount_CLI,
        ["storage account delete"] = s_deleteStorageAccount_CLI,
    };

    static ExtensionIndex()
    {
        AddStorageAccountCreateCustomParameters();

        foreach (CommandMetadata metadata in s_cliTable.Values)
        {
            metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
        }
        foreach (CommandMetadata metadata in s_psTable.Values)
        {
            metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
        }
    }

    private static void AddStorageAccountCreateCustomParameters()
    {
        AddCreateCliParameter("encryption-services", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionServices), typeof(List<string>), "Storage services to enable for encryption.");
        AddCreateCliParameter("encryption-key-source", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeySource), typeof(string), "Storage account encryption key source.");
        AddCreateCliParameter("encryption-key-name", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyName), typeof(string), "Storage account encryption key vault key name.");
        AddCreateCliParameter("encryption-key-vault", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyVault), typeof(string), "Storage account encryption key vault URI.");
        AddCreateCliParameter("encryption-key-version", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyVersion), typeof(string), "Storage account encryption key vault key version.");
        AddCreateCliParameter("assign-identity", nameof(CreateStorageAccountsProcessor_Orchestrator.AssignIdentity), typeof(bool), "Assign a system managed identity to the storage account.");
        AddCreateCliParameter("identity-type", nameof(CreateStorageAccountsProcessor_Orchestrator.IdentityType), typeof(string), "Storage account managed identity type.");
        AddCreateCliParameter("user-identity-id", nameof(CreateStorageAccountsProcessor_Orchestrator.UserAssignedIdentityIds), typeof(List<string>), "User-assigned managed identity resource ID.");
        AddCreateCliParameter("key-vault-user-identity-id", nameof(CreateStorageAccountsProcessor_Orchestrator.KeyVaultUserAssignedIdentityId), typeof(string), "User-assigned identity resource ID used to access Key Vault encryption keys.");
        AddCreateCliParameter("federated-identity-client-id", nameof(CreateStorageAccountsProcessor_Orchestrator.FederatedIdentityClientId), typeof(string), "Federated identity client ID used with Key Vault encryption.");
        AddCreateCliParameter("encryption-key-type-for-table", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyTypeForTable), typeof(string), "Encryption key type for the table service.");
        AddCreateCliParameter("encryption-key-type-for-queue", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyTypeForQueue), typeof(string), "Encryption key type for the queue service.");
        AddCreateCliParameter("require-infrastructure-encryption", nameof(CreateStorageAccountsProcessor_Orchestrator.RequireInfrastructureEncryption), typeof(bool), "Require infrastructure encryption for the storage account.");
        AddCreateCliParameter("enable-files-aadds", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAadds), typeof(bool), "Enable Azure Active Directory Domain Services authentication for Azure Files.");
        AddCreateCliParameter("enable-files-adds", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAdds), typeof(bool), "Enable Active Directory Domain Services authentication for Azure Files.");
        AddCreateCliParameter("enable-files-aadkerb", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAadkerb), typeof(bool), "Enable Azure Active Directory Kerberos authentication for Azure Files.");
        AddCreateCliParameter("domain-name", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainName), typeof(string), "Active Directory domain name.");
        AddCreateCliParameter("net-bios-domain-name", nameof(CreateStorageAccountsProcessor_Orchestrator.NetBiosDomainName), typeof(string), "Active Directory NetBIOS domain name.");
        AddCreateCliParameter("forest-name", nameof(CreateStorageAccountsProcessor_Orchestrator.ForestName), typeof(string), "Active Directory forest name.");
        AddCreateCliParameter("domain-guid", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainGuid), typeof(string), "Active Directory domain GUID.");
        AddCreateCliParameter("domain-sid", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainSid), typeof(string), "Active Directory domain SID.");
        AddCreateCliParameter("azure-storage-sid", nameof(CreateStorageAccountsProcessor_Orchestrator.AzureStorageSid), typeof(string), "Azure Storage SID for Active Directory authentication.");
        AddCreateCliParameter("sam-account-name", nameof(CreateStorageAccountsProcessor_Orchestrator.SamAccountName), typeof(string), "Active Directory SAM account name.");
        AddCreateCliParameter("account-type", nameof(CreateStorageAccountsProcessor_Orchestrator.AccountType), typeof(string), "Active Directory account type.");
        AddCreateCliParameter("default-share-permission", nameof(CreateStorageAccountsProcessor_Orchestrator.DefaultSharePermission), typeof(string), "Default Azure Files share permission.");
        AddCreateCliParameter("enable-smb-oauth", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableSmbOAuth), typeof(bool), "Enable SMB OAuth for Azure Files.");
        AddCreateCliParameter("bypass", nameof(CreateStorageAccountsProcessor_Orchestrator.Bypass), typeof(List<string>), "Network rule bypass services.");
        AddCreateCliParameter("default-action", nameof(CreateStorageAccountsProcessor_Orchestrator.DefaultAction), typeof(string), "Network rule default action.");
        AddCreateCliParameter("subnet", nameof(CreateStorageAccountsProcessor_Orchestrator.Subnet), typeof(string), "Subnet resource ID or subnet name for the storage account network rule.");
        AddCreateCliParameter("vnet-name", nameof(CreateStorageAccountsProcessor_Orchestrator.VirtualNetworkName), typeof(string), "Virtual network name used with subnet name for network rules.");
        AddCreateCliParameter("virtual-network-rule", nameof(CreateStorageAccountsProcessor_Orchestrator.VirtualNetworkRules), typeof(List<string>), "Virtual network subnet resource ID for a network rule.");
        AddCreateCliParameter("ip-rule", nameof(CreateStorageAccountsProcessor_Orchestrator.IpRules), typeof(List<string>), "IP address or CIDR range for a network rule.");
        AddCreateCliParameter("resource-access-rule", nameof(CreateStorageAccountsProcessor_Orchestrator.ResourceAccessRules), typeof(List<string>), "Resource access rule in tenantId=...;resourceId=... form.");
        AddCreateCliParameter("action", nameof(CreateStorageAccountsProcessor_Orchestrator.NetworkRuleAction), typeof(string), "Network rule action.");
        AddCreateCliParameter("routing-choice", nameof(CreateStorageAccountsProcessor_Orchestrator.RoutingChoice), typeof(string), "Storage account routing choice.");
        AddCreateCliParameter("publish-microsoft-endpoints", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishMicrosoftEndpoints), typeof(bool), "Publish Microsoft routing endpoints.");
        AddCreateCliParameter("publish-internet-endpoints", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishInternetEndpoints), typeof(bool), "Publish internet routing endpoints.");
        AddCreateCliParameter("edge-zone", nameof(CreateStorageAccountsProcessor_Orchestrator.EdgeZone), typeof(string), "Extended location EdgeZone name.");
        AddCreateCliParameter("zone-placement-policy", nameof(CreateStorageAccountsProcessor_Orchestrator.ZonePlacementPolicy), typeof(string), "Storage account zone placement policy.");
        AddCreateCliParameter("enable-blob-geo-priority-replication", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableBlobGeoPriorityReplication), typeof(bool), "Enable blob geo-priority replication.");
        AddCreateCliParameter("publish-ipv6-endpoint", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishIpv6Endpoint), typeof(bool), "Publish IPv6 endpoint.");
        AddCreateCliParameter("allowed-copy-scope", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowedCopyScope), typeof(string), "Allowed copy scope for storage account copy operations.");
        AddCreateCliParameter("allow-shared-key-access", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowSharedKeyAccess), typeof(bool), "Allow shared key access for the storage account.");
        AddCreateCliParameter("sas-expiration-period", nameof(CreateStorageAccountsProcessor_Orchestrator.SasExpirationPeriod), typeof(string), "SAS expiration period for the storage account.");
        AddCreateCliParameter("sas-expiration-action", nameof(CreateStorageAccountsProcessor_Orchestrator.SasExpirationAction), typeof(string), "Action for SAS expiration policy violations.");
        AddCreateCliParameter("key-expiration-period-in-days", nameof(CreateStorageAccountsProcessor_Orchestrator.KeyExpirationPeriodInDays), typeof(int), "Key expiration period in days.");
        AddCreateCliParameter("allow-cross-tenant-replication", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowCrossTenantReplication), typeof(bool), "Allow cross-tenant replication.");
        AddCreateCliParameter("enable-alw", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableAccountLevelImmutability), typeof(bool), "Enable account-level immutability with versioning.");
        AddCreateCliParameter("immutability-period-since-creation-in-days", nameof(CreateStorageAccountsProcessor_Orchestrator.ImmutabilityPeriodSinceCreationInDays), typeof(int), "Account-level immutability period in days.");
        AddCreateCliParameter("immutability-policy-state", nameof(CreateStorageAccountsProcessor_Orchestrator.ImmutabilityPolicyState), typeof(string), "Account-level immutability policy state.");
        AddCreateCliParameter("allow-protected-append-writes", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowProtectedAppendWrites), typeof(bool), "Allow protected append writes for account-level immutability.");
        AddCreateCliParameter("enable-large-file-share", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableLargeFileShare), typeof(bool), "Enable large file shares on the storage account.");

        AddCreatePsParameter("AssignIdentity", nameof(CreateStorageAccountsProcessor_Orchestrator.AssignIdentity), typeof(bool), "Assign a system managed identity to the storage account.");
        AddCreatePsParameter("IdentityType", nameof(CreateStorageAccountsProcessor_Orchestrator.IdentityType), typeof(string), "Storage account managed identity type.");
        AddCreatePsParameter("UserAssignedIdentityId", nameof(CreateStorageAccountsProcessor_Orchestrator.UserAssignedIdentityIds), typeof(List<string>), "User-assigned managed identity resource ID.");
        AddCreatePsParameter("KeyVaultUserAssignedIdentityId", nameof(CreateStorageAccountsProcessor_Orchestrator.KeyVaultUserAssignedIdentityId), typeof(string), "User-assigned identity resource ID used to access Key Vault encryption keys.");
        AddCreatePsParameter("KeyVaultFederatedClientId", nameof(CreateStorageAccountsProcessor_Orchestrator.KeyVaultFederatedClientId), typeof(string), "Federated identity client ID used with Key Vault encryption.");
        AddCreatePsParameter("KeyName", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyName), typeof(string), "Storage account encryption key vault key name.");
        AddCreatePsParameter("KeyVaultUri", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyVault), typeof(string), "Storage account encryption key vault URI.");
        AddCreatePsParameter("KeyVersion", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyVersion), typeof(string), "Storage account encryption key vault key version.");
        AddCreatePsParameter("EncryptionKeySource", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeySource), typeof(string), "Storage account encryption key source.");
        AddCreatePsParameter("EncryptionService", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionServices), typeof(List<string>), "Storage services to enable for encryption.");
        AddCreatePsParameter("EncryptionKeyTypeForTable", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyTypeForTable), typeof(string), "Encryption key type for the table service.");
        AddCreatePsParameter("EncryptionKeyTypeForQueue", nameof(CreateStorageAccountsProcessor_Orchestrator.EncryptionKeyTypeForQueue), typeof(string), "Encryption key type for the queue service.");
        AddCreatePsParameter("RequireInfrastructureEncryption", nameof(CreateStorageAccountsProcessor_Orchestrator.RequireInfrastructureEncryption), typeof(bool), "Require infrastructure encryption for the storage account.");
        AddCreatePsParameter("EnableAzureActiveDirectoryDomainServicesForFile", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAadds), typeof(bool), "Enable Azure Active Directory Domain Services authentication for Azure Files.");
        AddCreatePsParameter("EnableActiveDirectoryDomainServicesForFile", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAdds), typeof(bool), "Enable Active Directory Domain Services authentication for Azure Files.");
        AddCreatePsParameter("EnableAzureActiveDirectoryKerberosForFile", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableFilesAadkerb), typeof(bool), "Enable Azure Active Directory Kerberos authentication for Azure Files.");
        AddCreatePsParameter("ActiveDirectoryDomainName", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainName), typeof(string), "Active Directory domain name.");
        AddCreatePsParameter("ActiveDirectoryNetBiosDomainName", nameof(CreateStorageAccountsProcessor_Orchestrator.NetBiosDomainName), typeof(string), "Active Directory NetBIOS domain name.");
        AddCreatePsParameter("ActiveDirectoryForestName", nameof(CreateStorageAccountsProcessor_Orchestrator.ForestName), typeof(string), "Active Directory forest name.");
        AddCreatePsParameter("ActiveDirectoryDomainGuid", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainGuid), typeof(string), "Active Directory domain GUID.");
        AddCreatePsParameter("ActiveDirectoryDomainSid", nameof(CreateStorageAccountsProcessor_Orchestrator.DomainSid), typeof(string), "Active Directory domain SID.");
        AddCreatePsParameter("ActiveDirectoryAzureStorageSid", nameof(CreateStorageAccountsProcessor_Orchestrator.AzureStorageSid), typeof(string), "Azure Storage SID for Active Directory authentication.");
        AddCreatePsParameter("ActiveDirectorySamAccountName", nameof(CreateStorageAccountsProcessor_Orchestrator.SamAccountName), typeof(string), "Active Directory SAM account name.");
        AddCreatePsParameter("ActiveDirectoryAccountType", nameof(CreateStorageAccountsProcessor_Orchestrator.AccountType), typeof(string), "Active Directory account type.");
        AddCreatePsParameter("DefaultSharePermission", nameof(CreateStorageAccountsProcessor_Orchestrator.DefaultSharePermission), typeof(string), "Default Azure Files share permission.");
        AddCreatePsParameter("EnableSmbOAuth", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableSmbOAuth), typeof(bool), "Enable SMB OAuth for Azure Files.");
        AddCreatePsParameter("NetworkBypass", nameof(CreateStorageAccountsProcessor_Orchestrator.Bypass), typeof(List<string>), "Network rule bypass services.");
        AddCreatePsParameter("DefaultAction", nameof(CreateStorageAccountsProcessor_Orchestrator.DefaultAction), typeof(string), "Network rule default action.");
        AddCreatePsParameter("Subnet", nameof(CreateStorageAccountsProcessor_Orchestrator.Subnet), typeof(string), "Subnet resource ID or subnet name for the storage account network rule.");
        AddCreatePsParameter("VirtualNetworkName", nameof(CreateStorageAccountsProcessor_Orchestrator.VirtualNetworkName), typeof(string), "Virtual network name used with subnet name for network rules.");
        AddCreatePsParameter("VirtualNetworkRule", nameof(CreateStorageAccountsProcessor_Orchestrator.VirtualNetworkRules), typeof(List<string>), "Virtual network subnet resource ID for a network rule.");
        AddCreatePsParameter("IpRule", nameof(CreateStorageAccountsProcessor_Orchestrator.IpRules), typeof(List<string>), "IP address or CIDR range for a network rule.");
        AddCreatePsParameter("ResourceAccessRule", nameof(CreateStorageAccountsProcessor_Orchestrator.ResourceAccessRules), typeof(List<string>), "Resource access rule in tenantId=...;resourceId=... form.");
        AddCreatePsParameter("NetworkRuleAction", nameof(CreateStorageAccountsProcessor_Orchestrator.NetworkRuleAction), typeof(string), "Network rule action.");
        AddCreatePsParameter("RoutingChoice", nameof(CreateStorageAccountsProcessor_Orchestrator.RoutingChoice), typeof(string), "Storage account routing choice.");
        AddCreatePsParameter("PublishMicrosoftEndpoint", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishMicrosoftEndpoints), typeof(bool), "Publish Microsoft routing endpoints.");
        AddCreatePsParameter("PublishInternetEndpoint", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishInternetEndpoints), typeof(bool), "Publish internet routing endpoints.");
        AddCreatePsParameter("EdgeZone", nameof(CreateStorageAccountsProcessor_Orchestrator.EdgeZone), typeof(string), "Extended location EdgeZone name.");
        AddCreatePsParameter("ZonePlacementPolicy", nameof(CreateStorageAccountsProcessor_Orchestrator.ZonePlacementPolicy), typeof(string), "Storage account zone placement policy.");
        AddCreatePsParameter("EnableBlobGeoPriorityReplication", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableBlobGeoPriorityReplication), typeof(bool), "Enable blob geo-priority replication.");
        AddCreatePsParameter("PublishIpv6Endpoint", nameof(CreateStorageAccountsProcessor_Orchestrator.PublishIpv6Endpoint), typeof(bool), "Publish IPv6 endpoint.");
        AddCreatePsParameter("AllowedCopyScope", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowedCopyScope), typeof(string), "Allowed copy scope for storage account copy operations.");
        AddCreatePsParameter("AllowSharedKeyAccess", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowSharedKeyAccess), typeof(bool), "Allow shared key access for the storage account.");
        AddCreatePsParameter("SasExpirationPeriod", nameof(CreateStorageAccountsProcessor_Orchestrator.SasExpirationPeriod), typeof(string), "SAS expiration period for the storage account.");
        AddCreatePsParameter("SasExpirationAction", nameof(CreateStorageAccountsProcessor_Orchestrator.SasExpirationAction), typeof(string), "Action for SAS expiration policy violations.");
        AddCreatePsParameter("KeyExpirationPeriodInDay", nameof(CreateStorageAccountsProcessor_Orchestrator.KeyExpirationPeriodInDays), typeof(int), "Key expiration period in days.");
        AddCreatePsParameter("AllowCrossTenantReplication", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowCrossTenantReplication), typeof(bool), "Allow cross-tenant replication.");
        AddCreatePsParameter("EnableAccountLevelImmutability", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableAccountLevelImmutability), typeof(bool), "Enable account-level immutability with versioning.");
        AddCreatePsParameter("ImmutabilityPeriod", nameof(CreateStorageAccountsProcessor_Orchestrator.ImmutabilityPeriodSinceCreationInDays), typeof(int), "Account-level immutability period in days.");
        AddCreatePsParameter("ImmutabilityPolicyState", nameof(CreateStorageAccountsProcessor_Orchestrator.ImmutabilityPolicyState), typeof(string), "Account-level immutability policy state.");
        AddCreatePsParameter("AllowProtectedAppendWrites", nameof(CreateStorageAccountsProcessor_Orchestrator.AllowProtectedAppendWrites), typeof(bool), "Allow protected append writes for account-level immutability.");
        AddCreatePsParameter("EnableLargeFileShare", nameof(CreateStorageAccountsProcessor_Orchestrator.EnableLargeFileShare), typeof(bool), "Enable large file shares on the storage account.");
    }

    private static void AddCreateCliParameter(string invocationName, string name, Type parameterType, string help) =>
        s_newStorageAccount_CLI.Parameters.TryAdd(invocationName, new ParameterMetadata(name, invocationName, parameterType, help, IsRequired: false, Aliases: [], AllowedValues: []));

    private static void AddCreatePsParameter(string invocationName, string name, Type parameterType, string help) =>
        s_newStorageAccount_PS.Parameters.TryAdd(invocationName, new ParameterMetadata(name, invocationName, parameterType, help, IsRequired: false, Aliases: [], AllowedValues: []));

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        Dictionary<string, CommandMetadata> table = clientType switch
        {
            ClientType.PowerShell => s_psTable,
            ClientType.CLI => s_cliTable,
            _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
        };

        return table.TryGetValue(name, out CommandMetadata? metadata)
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