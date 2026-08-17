// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage
{
    [ExtensionVerb("storage")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──

        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newStorageAccount_CLI = new(
            Name: "NewStorageAccountProcessor",
            InvocationName: "storage account create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.NewStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.NewStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("AccountName", "name", typeof(string), "The name of the storage account. Must be between 3 and 24 characters in length and use numbers and lower-case letters only.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), "The Azure region in which to create the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["sku"]             = new("SkuName", "sku", typeof(string), "The SKU name of the storage account.", IsRequired: true, Aliases: [], AllowedValues: ["Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Standard_ZRS", "Premium_LRS", "Premium_ZRS", "Standard_GZRS", "Standard_RAGZRS", "StandardV2_LRS", "StandardV2_GRS", "StandardV2_ZRS", "StandardV2_GZRS", "PremiumV2_LRS", "PremiumV2_ZRS"]),
                ["kind"]            = new("Kind", "kind", typeof(string), "The type of storage account.", IsRequired: true, Aliases: [], AllowedValues: ["Storage", "StorageV2", "BlobStorage", "FileStorage", "BlockBlobStorage"]),
                ["tags"]                            = new("Tags", "tags", typeof(List<string>), "Resource tags in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["access-tier"]                     = new("AccessTier", "access-tier", typeof(string), "The access tier used for billing. Required when kind is BlobStorage.", IsRequired: false, Aliases: [], AllowedValues: ["Hot", "Cool", "Premium", "Cold", "Smart"]),
                ["min-tls-version"]                 = new("MinimumTlsVersion", "min-tls-version", typeof(string), "The minimum TLS version permitted on requests to storage. The service default is TLS1_0.", IsRequired: false, Aliases: [], AllowedValues: ["TLS1_0", "TLS1_1", "TLS1_2", "TLS1_3"]),
                ["public-network-access"]           = new("PublicNetworkAccess", "public-network-access", typeof(string), "Allow, disallow, or let Network Security Perimeter configuration evaluate public network access.", IsRequired: false, Aliases: [], AllowedValues: ["Enabled", "Disabled", "SecuredByPerimeter"]),
                ["allowed-copy-scope"]              = new("AllowedCopyScope", "allowed-copy-scope", typeof(string), "Restrict copy to and from storage accounts within an AAD tenant or with private links to the same VNet.", IsRequired: false, Aliases: [], AllowedValues: ["PrivateLink", "AAD", "All"]),
                ["large-file-shares-state"]         = new("LargeFileSharesState", "large-file-shares-state", typeof(string), "Allow large file shares. Cannot be disabled once enabled.", IsRequired: false, Aliases: [], AllowedValues: ["Disabled", "Enabled"]),
                ["dns-endpoint-type"]               = new("DnsEndpointType", "dns-endpoint-type", typeof(string), "The type of endpoint. AzureDnsZone allows creating a large number of accounts in a single subscription.", IsRequired: false, Aliases: [], AllowedValues: ["Standard", "AzureDnsZone"]),
                ["https-only"]                      = new("SupportsHttpsTrafficOnly", "https-only", typeof(string), "Allow https traffic only to the storage service. The service default is true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["allow-blob-public-access"]        = new("AllowBlobPublicAccess", "allow-blob-public-access", typeof(string), "Allow or disallow public access to all blobs or containers. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["allow-shared-key-access"]         = new("AllowSharedKeyAccess", "allow-shared-key-access", typeof(string), "Whether the account permits requests authorized with the account access key. The service default is true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-hierarchical-namespace"]   = new("IsHnsEnabled", "enable-hierarchical-namespace", typeof(string), "Enable a hierarchical namespace (Azure Data Lake Storage Gen2).", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-sftp"]                     = new("IsSftpEnabled", "enable-sftp", typeof(string), "Enable Secure File Transfer Protocol.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-local-user"]               = new("IsLocalUserEnabled", "enable-local-user", typeof(string), "Enable the local users feature.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-extended-groups"]          = new("EnableExtendedGroups", "enable-extended-groups", typeof(string), "Enable extended group support with the local users feature.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-nfs-v3"]                   = new("IsNfsV3Enabled", "enable-nfs-v3", typeof(string), "Enable NFS 3.0 protocol support.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["allow-cross-tenant-replication"]  = new("AllowCrossTenantReplication", "allow-cross-tenant-replication", typeof(string), "Allow or disallow cross AAD tenant object replication. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["default-to-oauth-authentication"] = new("DefaultToOAuthAuthentication", "default-to-oauth-authentication", typeof(string), "Whether the default authentication is OAuth. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["edge-zone"]                       = new("EdgeZone", "edge-zone", typeof(string), "The name of the edge zone to create the account in.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["identity-type"]                   = new("IdentityType", "identity-type", typeof(string), "The managed identity type for the account.", IsRequired: false, Aliases: [], AllowedValues: ["None", "SystemAssigned", "UserAssigned", "SystemAssigned,UserAssigned"]),
                ["user-identity-id"]                = new("UserIdentityId", "user-identity-id", typeof(string), "The ARM resource identifier of the user-assigned identity. Only one is permitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["zones"]                           = new("Zones", "zones", typeof(List<string>), "The availability zones to pin the account to (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["zone-placement-policy"]           = new("ZonePlacementPolicy", "zone-placement-policy", typeof(string), "The availability zone pinning policy for the account.", IsRequired: false, Aliases: [], AllowedValues: ["Any", "None"]),
                ["routing-choice"]                  = new("RoutingChoice", "routing-choice", typeof(string), "The kind of network routing used to deliver user data.", IsRequired: false, Aliases: [], AllowedValues: ["MicrosoftRouting", "InternetRouting"]),
                ["publish-microsoft-endpoints"]     = new("PublishMicrosoftEndpoints", "publish-microsoft-endpoints", typeof(string), "Whether Microsoft routing storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["publish-internet-endpoints"]      = new("PublishInternetEndpoints", "publish-internet-endpoints", typeof(string), "Whether internet routing storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["publish-ipv6-endpoint"]           = new("PublishIpv6Endpoint", "publish-ipv6-endpoint", typeof(string), "Whether IPv6 storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-blob-geo-priority-replication"] = new("EnableBlobGeoPriorityReplication", "enable-blob-geo-priority-replication", typeof(string), "Whether Blob Geo Priority Replication is enabled for the account.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["key-expiration-period-in-days"]   = new("KeyExpirationPeriodInDays", "key-expiration-period-in-days", typeof(string), "Expiration period in days of the key policy assigned to the account.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-expiration-period"]           = new("SasExpirationPeriod", "sas-expiration-period", typeof(string), "Expiration period of the SAS policy assigned to the account, in DD.HH:MM:SS form.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sas-expiration-action"]           = new("SasExpirationAction", "sas-expiration-action", typeof(string), "The action performed when the SAS expiration period is violated. Requires --sas-expiration-period. Defaults to Log.", IsRequired: false, Aliases: [], AllowedValues: ["Log", "Block"]),
                ["enable-alw"]                      = new("EnableAlw", "enable-alw", typeof(string), "Enable account-level immutability. Can only be set at creation time.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["immutability-period-since-creation-in-days"] = new("ImmutabilityPeriodSinceCreationInDays", "immutability-period-since-creation-in-days", typeof(string), "The immutability period for blobs since policy creation, in days (1-146000). Requires --enable-alw true.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["immutability-policy-state"]       = new("ImmutabilityPolicyState", "immutability-policy-state", typeof(string), "The mode of the account-level immutability policy. Requires --enable-alw true.", IsRequired: false, Aliases: [], AllowedValues: ["Unlocked", "Locked", "Disabled"]),
                ["allow-protected-append-writes"]   = new("AllowProtectedAppendWrites", "allow-protected-append-writes", typeof(string), "Allow new blocks to be written to an append blob while maintaining immutability protection. Requires --enable-alw true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["bypass"]                          = new("Bypass", "bypass", typeof(List<string>), "Traffic to bypass the network rules (space-separated): None, Logging, Metrics, AzureServices. Requires --default-action.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["default-action"]                  = new("DefaultAction", "default-action", typeof(string), "The action to apply when no network rule matches. Required when any other network rule flag is used.", IsRequired: false, Aliases: [], AllowedValues: ["Allow", "Deny"]),
                ["subnet"]                          = new("Subnet", "subnet", typeof(string), "Name or resource id of a subnet. If a name is given, --vnet-name is required.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vnet-name"]                       = new("VnetName", "vnet-name", typeof(string), "Name of the virtual network containing --subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["action"]                          = new("Action", "action", typeof(string), "The action applied to the subnet and IP rules. Defaults to Allow.", IsRequired: false, Aliases: [], AllowedValues: ["Allow"]),
                ["ip-address"]                      = new("IpAddress", "ip-address", typeof(List<string>), "IPv4 addresses or CIDR ranges to allow (space-separated). Requires --default-action.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ipv6-address"]                    = new("Ipv6Address", "ipv6-address", typeof(List<string>), "IPv6 addresses or CIDR ranges to allow (space-separated). Requires --default-action.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["custom-domain"]                   = new("CustomDomainName", "custom-domain", typeof(string), "The custom domain name assigned to the account. Name is the CNAME source.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
        };

        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["storage account create"]     = s_newStorageAccount_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Storage processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
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

        // ── Lookup ──
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
