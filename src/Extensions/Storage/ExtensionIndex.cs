using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage;

[ExtensionVerb("storage")]
public class ExtensionIndex : IExtensionIndex
{
    private const string Assembly = "Microsoft.Azure.Azclips.Extensions.Storage";

    private static readonly CommandMetadata s_newStorageAccount_PS = new(
        Name: "CreateStorageAccountsProcessor",
        InvocationName: "New-AzStorageAccount",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.CreateStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.CreateStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Location"] = new("Location", "Location", typeof(string), "Location for the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["SkuName"] = new("SkuName", "SkuName", typeof(string), "The storage account SKU name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Kind"] = new("Kind", "Kind", typeof(string), "The storage account kind.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Tag"] = new("Tags", "Tag", typeof(Dictionary<string, string>), "Tags for the storage account.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["AccessTier"] = new("AccessTier", "AccessTier", typeof(string), "The default access tier for block blobs.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableHttpsTrafficOnly"] = new("EnableHttpsTrafficOnly", "EnableHttpsTrafficOnly", typeof(bool), "Allow HTTPS traffic only.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess", "AllowBlobPublicAccess", typeof(bool), "Allow blob public access.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["MinimumTlsVersion"] = new("MinimumTlsVersion", "MinimumTlsVersion", typeof(string), "Minimum TLS version.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["PublicNetworkAccess"] = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "Public network access setting.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableHierarchicalNamespace"] = new("EnableHierarchicalNamespace", "EnableHierarchicalNamespace", typeof(bool), "Enable hierarchical namespace.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableSftp"] = new("EnableSftp", "EnableSftp", typeof(bool), "Enable SFTP.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableLocalUser"] = new("EnableLocalUser", "EnableLocalUser", typeof(bool), "Enable local users.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["EnableNfsV3"] = new("EnableNfsV3", "EnableNfsV3", typeof(bool), "Enable NFS v3.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["DnsEndpointType"] = new("DnsEndpointType", "DnsEndpointType", typeof(string), "DNS endpoint type.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["Zone"] = new("Zones", "Zone", typeof(List<string>), "Availability zones.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_getStorageAccount_PS = new(
        Name: "GetPropertiesStorageAccountsProcessor",
        InvocationName: "Get-AzStorageAccount_Get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.GetPropertiesStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.GetPropertiesStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Expand"] = new("Expand", "Expand", typeof(string), "Properties to expand.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_listStorageAccount_PS = new(
        Name: "ListStorageAccountsCommand",
        InvocationName: "Get-AzStorageAccount_List",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageAccountsCommand",
        AssemblyName: Assembly,
        ProcessorType: typeof(Customization.ListStorageAccountsCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_removeStorageAccount_PS = new(
        Name: "DeleteStorageAccountsProcessor",
        InvocationName: "Remove-AzStorageAccount",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.DeleteStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("AccountName", "Name", typeof(string), "The storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_newStorageAccount_CLI = new(
        Name: "CreateStorageAccountsProcessor",
        InvocationName: "storage account create",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.CreateStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.CreateStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
            ["location"] = new("Location", "location", typeof(string), "Location for the storage account.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
            ["sku"] = new("SkuName", "sku", typeof(string), "The storage account SKU name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["kind"] = new("Kind", "kind", typeof(string), "The storage account kind.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["tags"] = new("Tags", "tags", typeof(Dictionary<string, string>), "Tags for the storage account.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["access-tier"] = new("AccessTier", "access-tier", typeof(string), "The default access tier for block blobs.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["https-only"] = new("EnableHttpsTrafficOnly", "https-only", typeof(bool), "Allow HTTPS traffic only.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["allow-blob-public-access"] = new("AllowBlobPublicAccess", "allow-blob-public-access", typeof(bool), "Allow blob public access.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["min-tls-version"] = new("MinimumTlsVersion", "min-tls-version", typeof(string), "Minimum TLS version.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["public-network-access"] = new("PublicNetworkAccess", "public-network-access", typeof(string), "Public network access setting.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["hns"] = new("EnableHierarchicalNamespace", "hns", typeof(bool), "Enable hierarchical namespace.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-sftp"] = new("EnableSftp", "enable-sftp", typeof(bool), "Enable SFTP.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-local-user"] = new("EnableLocalUser", "enable-local-user", typeof(bool), "Enable local users.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["enable-nfs-v3"] = new("EnableNfsV3", "enable-nfs-v3", typeof(bool), "Enable NFS v3.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["dns-endpoint-type"] = new("DnsEndpointType", "dns-endpoint-type", typeof(string), "DNS endpoint type.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["zone"] = new("Zones", "zone", typeof(List<string>), "Availability zones.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_getStorageAccount_CLI = new(
        Name: "GetPropertiesStorageAccountsProcessor",
        InvocationName: "storage account show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.GetPropertiesStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.GetPropertiesStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
            ["expand"] = new("Expand", "expand", typeof(string), "Properties to expand.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_listStorageAccount_CLI = new(
        Name: "ListStorageAccountsCommand",
        InvocationName: "storage account list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageAccountsCommand",
        AssemblyName: Assembly,
        ProcessorType: typeof(Customization.ListStorageAccountsCommand),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_deleteStorageAccount_CLI = new(
        Name: "DeleteStorageAccountsProcessor",
        InvocationName: "storage account delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountsProcessor",
        AssemblyName: Assembly,
        ProcessorType: typeof(Processors.DeleteStorageAccountsProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("AccountName", "name", typeof(string), "The storage account name.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
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
        foreach (var metadata in s_cliTable.Values)
        {
            metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
        }

        foreach (var metadata in s_psTable.Values)
        {
            metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
        }
    }

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