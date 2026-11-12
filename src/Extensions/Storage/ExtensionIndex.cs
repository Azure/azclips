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
        private static readonly CommandMetadata s_newStorageAccount_PS = new(
            Name: "NewStorageAccountProcessor_Orchestrator",
            InvocationName: "New-AzStorageAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.NewStorageAccountProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.NewStorageAccountProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["AccountName"] = new("AccountName", "AccountName", typeof(string), "The name of the storage account. Must be between 3 and 24 characters in length and use numbers and lower-case letters only.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "The Azure region in which to create the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SkuName"] = new("SkuName", "SkuName", typeof(string), "The SKU name of the storage account.", IsRequired: true, Aliases: [], AllowedValues: ["Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Standard_ZRS", "Premium_LRS", "Premium_ZRS", "Standard_GZRS", "Standard_RAGZRS", "StandardV2_LRS", "StandardV2_GRS", "StandardV2_ZRS", "StandardV2_GZRS", "PremiumV2_LRS", "PremiumV2_ZRS"]),
                ["Kind"] = new("Kind", "Kind", typeof(string), "The type of storage account.", IsRequired: true, Aliases: [], AllowedValues: ["Storage", "StorageV2", "BlobStorage", "FileStorage", "BlockBlobStorage"]),
                ["Tags"] = new("Tags", "Tags", typeof(List<string>), "Resource tags in KEY=VALUE form (comma-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AccessTier"] = new("AccessTier", "AccessTier", typeof(string), "The access tier used for billing. Required when kind is BlobStorage.", IsRequired: false, Aliases: [], AllowedValues: ["Hot", "Cool", "Premium", "Cold", "Smart"]),
                ["MinimumTlsVersion"] = new("MinimumTlsVersion", "MinimumTlsVersion", typeof(string), "The minimum TLS version permitted on requests to storage. The service default is TLS1_0.", IsRequired: false, Aliases: [], AllowedValues: ["TLS1_0", "TLS1_1", "TLS1_2", "TLS1_3"]),
                ["PublicNetworkAccess"] = new("PublicNetworkAccess", "PublicNetworkAccess", typeof(string), "Allow, disallow, or let Network Security Perimeter configuration evaluate public network access.", IsRequired: false, Aliases: [], AllowedValues: ["Enabled", "Disabled", "SecuredByPerimeter"]),
                ["AllowedCopyScope"] = new("AllowedCopyScope", "AllowedCopyScope", typeof(string), "Restrict copy to and from storage accounts within an AAD tenant or with private links to the same VNet.", IsRequired: false, Aliases: [], AllowedValues: ["PrivateLink", "AAD", "All"]),
                ["LargeFileSharesState"] = new("LargeFileSharesState", "LargeFileSharesState", typeof(string), "Allow large file shares. Cannot be disabled once enabled.", IsRequired: false, Aliases: [], AllowedValues: ["Disabled", "Enabled"]),
                ["DnsEndpointType"] = new("DnsEndpointType", "DnsEndpointType", typeof(string), "The type of endpoint. AzureDnsZone allows creating a large number of accounts in a single subscription.", IsRequired: false, Aliases: [], AllowedValues: ["Standard", "AzureDnsZone"]),
                ["SupportsHttpsTrafficOnly"] = new("SupportsHttpsTrafficOnly", "SupportsHttpsTrafficOnly", typeof(string), "Allow https traffic only to the storage service. The service default is true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess", "AllowBlobPublicAccess", typeof(string), "Allow or disallow public access to all blobs or containers. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["AllowSharedKeyAccess"] = new("AllowSharedKeyAccess", "AllowSharedKeyAccess", typeof(string), "Whether the account permits requests authorized with the account access key. The service default is true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["IsHnsEnabled"] = new("IsHnsEnabled", "IsHnsEnabled", typeof(string), "Enable a hierarchical namespace (Azure Data Lake Storage Gen2).", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["IsSftpEnabled"] = new("IsSftpEnabled", "IsSftpEnabled", typeof(string), "Enable Secure File Transfer Protocol.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["IsLocalUserEnabled"] = new("IsLocalUserEnabled", "IsLocalUserEnabled", typeof(string), "Enable the local users feature.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EnableExtendedGroups"] = new("EnableExtendedGroups", "EnableExtendedGroups", typeof(string), "Enable extended group support with the local users feature.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["IsNfsV3Enabled"] = new("IsNfsV3Enabled", "IsNfsV3Enabled", typeof(string), "Enable NFS 3.0 protocol support.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["AllowCrossTenantReplication"] = new("AllowCrossTenantReplication", "AllowCrossTenantReplication", typeof(string), "Allow or disallow cross AAD tenant object replication. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["DefaultToOAuthAuthentication"] = new("DefaultToOAuthAuthentication", "DefaultToOAuthAuthentication", typeof(string), "Whether the default authentication is OAuth. The service default is false.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EdgeZone"] = new("EdgeZone", "EdgeZone", typeof(string), "The name of the edge zone to create the account in.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IdentityType"] = new("IdentityType", "IdentityType", typeof(string), "The managed identity type for the account.", IsRequired: false, Aliases: [], AllowedValues: ["None", "SystemAssigned", "UserAssigned", "SystemAssigned,UserAssigned"]),
                ["UserIdentityId"] = new("UserIdentityId", "UserIdentityId", typeof(string), "The ARM resource identifier of the user-assigned identity. Only one is permitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Zones"] = new("Zones", "Zones", typeof(List<string>), "The availability zones to pin the account to (comma-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ZonePlacementPolicy"] = new("ZonePlacementPolicy", "ZonePlacementPolicy", typeof(string), "The availability zone pinning policy for the account.", IsRequired: false, Aliases: [], AllowedValues: ["Any", "None"]),
                ["RoutingChoice"] = new("RoutingChoice", "RoutingChoice", typeof(string), "The kind of network routing used to deliver user data.", IsRequired: false, Aliases: [], AllowedValues: ["MicrosoftRouting", "InternetRouting"]),
                ["PublishMicrosoftEndpoints"] = new("PublishMicrosoftEndpoints", "PublishMicrosoftEndpoints", typeof(string), "Whether Microsoft routing storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["PublishInternetEndpoints"] = new("PublishInternetEndpoints", "PublishInternetEndpoints", typeof(string), "Whether internet routing storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["PublishIpv6Endpoint"] = new("PublishIpv6Endpoint", "PublishIpv6Endpoint", typeof(string), "Whether IPv6 storage endpoints are published.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EnableBlobGeoPriorityReplication"] = new("EnableBlobGeoPriorityReplication", "EnableBlobGeoPriorityReplication", typeof(string), "Whether Blob Geo Priority Replication is enabled for the account.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["KeyExpirationPeriodInDays"] = new("KeyExpirationPeriodInDays", "KeyExpirationPeriodInDays", typeof(string), "Expiration period in days of the key policy assigned to the account.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasExpirationPeriod"] = new("SasExpirationPeriod", "SasExpirationPeriod", typeof(string), "Expiration period of the SAS policy assigned to the account, in DD.HH:MM:SS form.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SasExpirationAction"] = new("SasExpirationAction", "SasExpirationAction", typeof(string), "The action performed when the SAS expiration period is violated. Requires -SasExpirationPeriod. Defaults to Log.", IsRequired: false, Aliases: [], AllowedValues: ["Log", "Block"]),
                ["EnableAlw"] = new("EnableAlw", "EnableAlw", typeof(string), "Enable account-level immutability. Can only be set at creation time.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["ImmutabilityPeriodSinceCreationInDays"] = new("ImmutabilityPeriodSinceCreationInDays", "ImmutabilityPeriodSinceCreationInDays", typeof(string), "The immutability period for blobs since policy creation, in days (1-146000). Requires -EnableAlw true.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ImmutabilityPolicyState"] = new("ImmutabilityPolicyState", "ImmutabilityPolicyState", typeof(string), "The mode of the account-level immutability policy. Requires -EnableAlw true.", IsRequired: false, Aliases: [], AllowedValues: ["Unlocked", "Locked", "Disabled"]),
                ["AllowProtectedAppendWrites"] = new("AllowProtectedAppendWrites", "AllowProtectedAppendWrites", typeof(string), "Allow new blocks to be written to an append blob while maintaining immutability protection. Requires -EnableAlw true.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["Bypass"] = new("Bypass", "Bypass", typeof(List<string>), "Traffic to bypass the network rules (comma-separated): None, Logging, Metrics, AzureServices. Requires -DefaultAction.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DefaultAction"] = new("DefaultAction", "DefaultAction", typeof(string), "The action to apply when no network rule matches. Required when any other network rule flag is used.", IsRequired: false, Aliases: [], AllowedValues: ["Allow", "Deny"]),
                ["Subnet"] = new("Subnet", "Subnet", typeof(string), "Name or resource id of a subnet. If a name is given, -VnetName is required.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VnetName"] = new("VnetName", "VnetName", typeof(string), "Name of the virtual network containing -Subnet.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Action"] = new("Action", "Action", typeof(string), "The action applied to the subnet and IP rules. Defaults to Allow.", IsRequired: false, Aliases: [], AllowedValues: ["Allow"]),
                ["IpAddress"] = new("IpAddress", "IpAddress", typeof(List<string>), "IPv4 addresses or CIDR ranges to allow (comma-separated). Requires -DefaultAction.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Ipv6Address"] = new("Ipv6Address", "Ipv6Address", typeof(List<string>), "IPv6 addresses or CIDR ranges to allow (comma-separated). Requires -DefaultAction.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableFilesAadds"] = new("EnableFilesAadds", "EnableFilesAadds", typeof(string), "Enable Microsoft Entra Domain Services authentication for Azure Files.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EnableFilesAadkerb"] = new("EnableFilesAadkerb", "EnableFilesAadkerb", typeof(string), "Enable Microsoft Entra Kerberos authentication for Azure Files.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EnableFilesAdds"] = new("EnableFilesAdds", "EnableFilesAdds", typeof(string), "Enable Active Directory Domain Services authentication for Azure Files. Requires all Active Directory properties.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["DomainName"] = new("DomainName", "DomainName", typeof(string), "The primary domain the AD DNS server is authoritative for.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NetBiosDomainName"] = new("NetBiosDomainName", "NetBiosDomainName", typeof(string), "The NetBIOS domain name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ForestName"] = new("ForestName", "ForestName", typeof(string), "The Active Directory forest to get.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DomainGuid"] = new("DomainGuid", "DomainGuid", typeof(string), "The domain GUID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DomainSid"] = new("DomainSid", "DomainSid", typeof(string), "The security identifier (SID) of the AD domain.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AzureStorageSid"] = new("AzureStorageSid", "AzureStorageSid", typeof(string), "The security identifier (SID) for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SamAccountName"] = new("SamAccountName", "SamAccountName", typeof(string), "The Active Directory SAMAccountName for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AccountType"] = new("AccountType", "AccountType", typeof(string), "The Active Directory account type for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: ["User", "Computer"]),
                ["DefaultSharePermission"] = new("DefaultSharePermission", "DefaultSharePermission", typeof(string), "The default share permission for users using Kerberos authentication when no RBAC role is assigned.", IsRequired: false, Aliases: [], AllowedValues: ["None", "StorageFileDataSmbShareReader", "StorageFileDataSmbShareContributor", "StorageFileDataSmbShareElevatedContributor"]),
                ["EnableSmbOauth"] = new("EnableSmbOauth", "EnableSmbOauth", typeof(string), "Whether managed identities can access SMB shares using OAuth.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["EncryptionKeySource"] = new("EncryptionKeySource", "EncryptionKeySource", typeof(string), "The encryption key source. Inferred as Microsoft.Keyvault when key vault options are supplied.", IsRequired: false, Aliases: [], AllowedValues: ["Microsoft.Storage", "Microsoft.Keyvault"]),
                ["EncryptionKeyVault"] = new("EncryptionKeyVault", "EncryptionKeyVault", typeof(string), "The URI of the key vault. Must be given together with -EncryptionKeyName.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyName"] = new("EncryptionKeyName", "EncryptionKeyName", typeof(string), "The name of the key vault key. Must be given together with -EncryptionKeyVault.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyVersion"] = new("EncryptionKeyVersion", "EncryptionKeyVersion", typeof(string), "The version of the key vault key. Opts out of implicit key rotation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RequireInfrastructureEncryption"] = new("RequireInfrastructureEncryption", "RequireInfrastructureEncryption", typeof(string), "Whether the service applies a secondary layer of encryption with platform managed keys.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["KeyVaultUserIdentityId"] = new("KeyVaultUserIdentityId", "KeyVaultUserIdentityId", typeof(string), "Resource id of the user-assigned identity used for server-side encryption.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["KeyVaultFederatedClientId"] = new("KeyVaultFederatedClientId", "KeyVaultFederatedClientId", typeof(string), "Client id of the multi-tenant application used for cross-tenant customer-managed-key encryption.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EncryptionKeyTypeForTable"] = new("EncryptionKeyTypeForTable", "EncryptionKeyTypeForTable", typeof(string), "The encryption key type for the Table service.", IsRequired: false, Aliases: [], AllowedValues: ["Account", "Service"]),
                ["EncryptionKeyTypeForQueue"] = new("EncryptionKeyTypeForQueue", "EncryptionKeyTypeForQueue", typeof(string), "The encryption key type for the Queue service.", IsRequired: false, Aliases: [], AllowedValues: ["Account", "Service"]),
                ["CustomDomainName"] = new("CustomDomainName", "CustomDomainName", typeof(string), "The custom domain name assigned to the account. Name is the CNAME source.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_showStorageAccount_PS = new(
            Name: "ShowStorageAccountProcessor",
            InvocationName: "Get-AzStorageAccount_AccountName",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.ShowStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.ShowStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("AccountName", "Name", typeof(string), "The name of the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Expand"] = new("Expand", "Expand", typeof(string), "Additional properties to include in the response.", IsRequired: false, Aliases: [], AllowedValues: ["geoReplicationStats"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_listStorageAccount_PS = new(
            Name: "ListStorageAccountProcessor_Orchestrator",
            InvocationName: "Get-AzStorageAccount_ResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.ListStorageAccountProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.ListStorageAccountProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every storage account in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata s_removeStorageAccount_PS = new(
            Name: "DeleteStorageAccountProcessor",
            InvocationName: "Remove-AzStorageAccount",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.DeleteStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("AccountName", "Name", typeof(string), "The name of the storage account.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );


        private static readonly CommandMetadata s_newStorageAccount_CLI = new(
            Name: "NewStorageAccountProcessor_Orchestrator",
            InvocationName: "storage account create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.NewStorageAccountProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.NewStorageAccountProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["name"]            = new("AccountName", "name", typeof(string), "The name of the storage account. Must be between 3 and 24 characters in length and use numbers and lower-case letters only.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), "The Azure region in which to create the storage account. Defaults to the resource group location.", IsRequired: false, Aliases: ["l"], AllowedValues: []),
                ["sku"]             = new("SkuName", "sku", typeof(string), "The SKU name of the storage account. Defaults to Standard_RAGRS.", IsRequired: false, Aliases: [], AllowedValues: ["Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Standard_ZRS", "Premium_LRS", "Premium_ZRS", "Standard_GZRS", "Standard_RAGZRS", "StandardV2_LRS", "StandardV2_GRS", "StandardV2_ZRS", "StandardV2_GZRS", "PremiumV2_LRS", "PremiumV2_ZRS"]),
                ["kind"]            = new("Kind", "kind", typeof(string), "The type of storage account. Defaults to StorageV2.", IsRequired: false, Aliases: [], AllowedValues: ["Storage", "StorageV2", "BlobStorage", "FileStorage", "BlockBlobStorage"]),
                ["tags"]                            = new("Tags", "tags", typeof(List<string>), "Resource tags in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["access-tier"]                     = new("AccessTier", "access-tier", typeof(string), "The access tier used for billing. Required when kind is BlobStorage.", IsRequired: false, Aliases: [], AllowedValues: ["Hot", "Cool", "Premium", "Cold", "Smart"]),
                ["min-tls-version"]                 = new("MinimumTlsVersion", "min-tls-version", typeof(string), "The minimum TLS version permitted on requests to storage. The service default is TLS1_0.", IsRequired: false, Aliases: [], AllowedValues: ["TLS1_0", "TLS1_1", "TLS1_2", "TLS1_3"]),
                ["public-network-access"]           = new("PublicNetworkAccess", "public-network-access", typeof(string), "Allow, disallow, or let Network Security Perimeter configuration evaluate public network access.", IsRequired: false, Aliases: [], AllowedValues: ["Enabled", "Disabled", "SecuredByPerimeter"]),
                ["allowed-copy-scope"]              = new("AllowedCopyScope", "allowed-copy-scope", typeof(string), "Restrict copy to and from storage accounts within an AAD tenant or with private links to the same VNet.", IsRequired: false, Aliases: [], AllowedValues: ["PrivateLink", "AAD", "All"]),
                ["large-file-shares-state"]         = new("LargeFileSharesState", "large-file-shares-state", typeof(string), "Allow large file shares. Cannot be disabled once enabled.", IsRequired: false, Aliases: [], AllowedValues: ["Disabled", "Enabled"]),
                ["enable-large-file-share"]          = new("EnableLargeFileShare", "enable-large-file-share", typeof(bool), "Enable large file shares. Cannot be disabled once enabled.", IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["enable-files-aadds"]              = new("EnableFilesAadds", "enable-files-aadds", typeof(string), "Enable Microsoft Entra Domain Services authentication for Azure Files.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-files-aadkerb"]            = new("EnableFilesAadkerb", "enable-files-aadkerb", typeof(string), "Enable Microsoft Entra Kerberos authentication for Azure Files.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["enable-files-adds"]               = new("EnableFilesAdds", "enable-files-adds", typeof(string), "Enable Active Directory Domain Services authentication for Azure Files. Requires all Active Directory properties.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["domain-name"]                     = new("DomainName", "domain-name", typeof(string), "The primary domain the AD DNS server is authoritative for.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["net-bios-domain-name"]            = new("NetBiosDomainName", "net-bios-domain-name", typeof(string), "The NetBIOS domain name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["forest-name"]                     = new("ForestName", "forest-name", typeof(string), "The Active Directory forest to get.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["domain-guid"]                     = new("DomainGuid", "domain-guid", typeof(string), "The domain GUID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["domain-sid"]                      = new("DomainSid", "domain-sid", typeof(string), "The security identifier (SID) of the AD domain.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["azure-storage-sid"]               = new("AzureStorageSid", "azure-storage-sid", typeof(string), "The security identifier (SID) for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["sam-account-name"]                = new("SamAccountName", "sam-account-name", typeof(string), "The Active Directory SAMAccountName for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["account-type"]                    = new("AccountType", "account-type", typeof(string), "The Active Directory account type for Azure Storage.", IsRequired: false, Aliases: [], AllowedValues: ["User", "Computer"]),
                ["default-share-permission"]        = new("DefaultSharePermission", "default-share-permission", typeof(string), "The default share permission for users using Kerberos authentication when no RBAC role is assigned.", IsRequired: false, Aliases: [], AllowedValues: ["None", "StorageFileDataSmbShareReader", "StorageFileDataSmbShareContributor", "StorageFileDataSmbShareElevatedContributor"]),
                ["enable-smb-oauth"]                = new("EnableSmbOauth", "enable-smb-oauth", typeof(string), "Whether managed identities can access SMB shares using OAuth.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["encryption-key-source"]           = new("EncryptionKeySource", "encryption-key-source", typeof(string), "The encryption key source. Inferred as Microsoft.Keyvault when key vault options are supplied.", IsRequired: false, Aliases: [], AllowedValues: ["Microsoft.Storage", "Microsoft.Keyvault"]),
                ["encryption-key-vault"]            = new("EncryptionKeyVault", "encryption-key-vault", typeof(string), "The URI of the key vault. Must be given together with --encryption-key-name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-name"]             = new("EncryptionKeyName", "encryption-key-name", typeof(string), "The name of the key vault key. Must be given together with --encryption-key-vault.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-version"]          = new("EncryptionKeyVersion", "encryption-key-version", typeof(string), "The version of the key vault key. Opts out of implicit key rotation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["require-infrastructure-encryption"] = new("RequireInfrastructureEncryption", "require-infrastructure-encryption", typeof(string), "Whether the service applies a secondary layer of encryption with platform managed keys.", IsRequired: false, Aliases: [], AllowedValues: ["true", "false"]),
                ["key-vault-user-identity-id"]      = new("KeyVaultUserIdentityId", "key-vault-user-identity-id", typeof(string), "Resource id of the user-assigned identity used for server-side encryption.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["key-vault-federated-client-id"]   = new("KeyVaultFederatedClientId", "key-vault-federated-client-id", typeof(string), "Client id of the multi-tenant application used for cross-tenant customer-managed-key encryption.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["encryption-key-type-for-table"]   = new("EncryptionKeyTypeForTable", "encryption-key-type-for-table", typeof(string), "The encryption key type for the Table service.", IsRequired: false, Aliases: [], AllowedValues: ["Account", "Service"]),
                ["encryption-key-type-for-queue"]   = new("EncryptionKeyTypeForQueue", "encryption-key-type-for-queue", typeof(string), "The encryption key type for the Queue service.", IsRequired: false, Aliases: [], AllowedValues: ["Account", "Service"]),
                ["custom-domain"]                   = new("CustomDomainName", "custom-domain", typeof(string), "The custom domain name assigned to the account. Name is the CNAME source.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
                ["l"] = "location",
            },
            ParameterSets: new Dictionary<string, ParameterSetMetadata>
            {
                ["Default"] = new("Default", IsDefault: true, Parameters: new Dictionary<string, ParameterReference>
                {
                    ["SubscriptionId"] = new("SubscriptionId"),
                    ["ResourceGroupName"] = new("ResourceGroupName", IsRequired: true),
                    ["AccountName"] = new("AccountName", IsRequired: true),
                    ["Location"] = new("Location"),
                    ["SkuName"] = new("SkuName"),
                    ["Kind"] = new("Kind"),
                    ["Tags"] = new("Tags"),
                    ["AccessTier"] = new("AccessTier"),
                    ["MinimumTlsVersion"] = new("MinimumTlsVersion"),
                    ["PublicNetworkAccess"] = new("PublicNetworkAccess"),
                    ["AllowedCopyScope"] = new("AllowedCopyScope"),
                    ["LargeFileSharesState"] = new("LargeFileSharesState"),
                    ["EnableLargeFileShare"] = new("EnableLargeFileShare"),
                    ["DnsEndpointType"] = new("DnsEndpointType"),
                    ["SupportsHttpsTrafficOnly"] = new("SupportsHttpsTrafficOnly"),
                    ["AllowBlobPublicAccess"] = new("AllowBlobPublicAccess"),
                    ["AllowSharedKeyAccess"] = new("AllowSharedKeyAccess"),
                    ["IsHnsEnabled"] = new("IsHnsEnabled"),
                    ["IsSftpEnabled"] = new("IsSftpEnabled"),
                    ["IsLocalUserEnabled"] = new("IsLocalUserEnabled"),
                    ["EnableExtendedGroups"] = new("EnableExtendedGroups"),
                    ["IsNfsV3Enabled"] = new("IsNfsV3Enabled"),
                    ["AllowCrossTenantReplication"] = new("AllowCrossTenantReplication"),
                    ["DefaultToOAuthAuthentication"] = new("DefaultToOAuthAuthentication"),
                    ["EdgeZone"] = new("EdgeZone"),
                    ["IdentityType"] = new("IdentityType"),
                    ["UserIdentityId"] = new("UserIdentityId"),
                    ["Zones"] = new("Zones"),
                    ["ZonePlacementPolicy"] = new("ZonePlacementPolicy"),
                    ["RoutingChoice"] = new("RoutingChoice"),
                    ["PublishMicrosoftEndpoints"] = new("PublishMicrosoftEndpoints"),
                    ["PublishInternetEndpoints"] = new("PublishInternetEndpoints"),
                    ["PublishIpv6Endpoint"] = new("PublishIpv6Endpoint"),
                    ["EnableBlobGeoPriorityReplication"] = new("EnableBlobGeoPriorityReplication"),
                    ["KeyExpirationPeriodInDays"] = new("KeyExpirationPeriodInDays"),
                    ["SasExpirationPeriod"] = new("SasExpirationPeriod"),
                    ["SasExpirationAction"] = new("SasExpirationAction"),
                    ["EnableAlw"] = new("EnableAlw"),
                    ["ImmutabilityPeriodSinceCreationInDays"] = new("ImmutabilityPeriodSinceCreationInDays"),
                    ["ImmutabilityPolicyState"] = new("ImmutabilityPolicyState"),
                    ["AllowProtectedAppendWrites"] = new("AllowProtectedAppendWrites"),
                    ["Bypass"] = new("Bypass"),
                    ["DefaultAction"] = new("DefaultAction"),
                    ["Subnet"] = new("Subnet"),
                    ["VnetName"] = new("VnetName"),
                    ["Action"] = new("Action"),
                    ["IpAddress"] = new("IpAddress"),
                    ["Ipv6Address"] = new("Ipv6Address"),
                    ["EnableFilesAadds"] = new("EnableFilesAadds"),
                    ["EnableFilesAadkerb"] = new("EnableFilesAadkerb"),
                    ["EnableFilesAdds"] = new("EnableFilesAdds"),
                    ["DomainName"] = new("DomainName"),
                    ["NetBiosDomainName"] = new("NetBiosDomainName"),
                    ["ForestName"] = new("ForestName"),
                    ["DomainGuid"] = new("DomainGuid"),
                    ["DomainSid"] = new("DomainSid"),
                    ["AzureStorageSid"] = new("AzureStorageSid"),
                    ["SamAccountName"] = new("SamAccountName"),
                    ["AccountType"] = new("AccountType"),
                    ["DefaultSharePermission"] = new("DefaultSharePermission"),
                    ["EnableSmbOauth"] = new("EnableSmbOauth"),
                    ["EncryptionKeySource"] = new("EncryptionKeySource"),
                    ["EncryptionKeyVault"] = new("EncryptionKeyVault"),
                    ["EncryptionKeyName"] = new("EncryptionKeyName"),
                    ["EncryptionKeyVersion"] = new("EncryptionKeyVersion"),
                    ["RequireInfrastructureEncryption"] = new("RequireInfrastructureEncryption"),
                    ["KeyVaultUserIdentityId"] = new("KeyVaultUserIdentityId"),
                    ["KeyVaultFederatedClientId"] = new("KeyVaultFederatedClientId"),
                    ["EncryptionKeyTypeForTable"] = new("EncryptionKeyTypeForTable"),
                    ["EncryptionKeyTypeForQueue"] = new("EncryptionKeyTypeForQueue"),
                    ["CustomDomainName"] = new("CustomDomainName"),
                }),
            }
        );

        private static readonly CommandMetadata s_showStorageAccount_CLI = new(
            Name: "ShowStorageAccountProcessor",
            InvocationName: "storage account show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.ShowStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.ShowStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["name"]            = new("AccountName", "name", typeof(string), "The name of the storage account.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["expand"]          = new("Expand", "expand", typeof(string), "Additional properties to include in the response.", IsRequired: false, Aliases: [], AllowedValues: ["geoReplicationStats"]),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            },
            ParameterSets: new Dictionary<string, ParameterSetMetadata>
            {
                ["Default"] = new("Default", IsDefault: true, Parameters: new Dictionary<string, ParameterReference>
                {
                    ["SubscriptionId"] = new("SubscriptionId"),
                    ["ResourceGroupName"] = new("ResourceGroupName", IsRequired: true),
                    ["AccountName"] = new("AccountName", IsRequired: true),
                    ["Expand"] = new("Expand"),
                }),
            }
        );

        private static readonly CommandMetadata s_listStorageAccount_CLI = new(
            Name: "ListStorageAccountProcessor_Orchestrator",
            InvocationName: "storage account list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.ListStorageAccountProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.ListStorageAccountProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every storage account in the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            },
            // One set per API operation: StorageAccounts_List and StorageAccounts_ListByResourceGroup.
            ParameterSets: new Dictionary<string, ParameterSetMetadata>
            {
                ["List"] = new("List", IsDefault: true, Parameters: new Dictionary<string, ParameterReference>
                {
                    ["SubscriptionId"] = new("SubscriptionId"),
                }),
                ["ListByResourceGroup"] = new("ListByResourceGroup", Parameters: new Dictionary<string, ParameterReference>
                {
                    ["SubscriptionId"] = new("SubscriptionId"),
                    ["ResourceGroupName"] = new("ResourceGroupName", IsRequired: true),
                }),
            }
        );

        private static readonly CommandMetadata s_deleteStorageAccount_CLI = new(
            Name: "DeleteStorageAccountProcessor",
            InvocationName: "storage account delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Processors.DeleteStorageAccountProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(Processors.DeleteStorageAccountProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the subscription of the active profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["name"]            = new("AccountName", "name", typeof(string), "The name of the storage account.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            },
            ParameterSets: new Dictionary<string, ParameterSetMetadata>
            {
                ["Default"] = new("Default", IsDefault: true, Parameters: new Dictionary<string, ParameterReference>
                {
                    ["SubscriptionId"] = new("SubscriptionId"),
                    ["ResourceGroupName"] = new("ResourceGroupName", IsRequired: true),
                    ["AccountName"] = new("AccountName", IsRequired: true),
                }),
            }
        );

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzStorageAccount"] = s_newStorageAccount_PS,
            // Get-AzStorageAccount resolves to one of these by parameter set: the cmdlet appends
            // its ParameterSetName to the command name, so each set binds to its own processor.
            ["Get-AzStorageAccount_AccountName"] = s_showStorageAccount_PS,
            ["Get-AzStorageAccount_ResourceGroup"] = s_listStorageAccount_PS,
            ["Remove-AzStorageAccount"] = s_removeStorageAccount_PS,
        };

        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["storage account create"]     = s_newStorageAccount_CLI,
            ["storage account show"]       = s_showStorageAccount_CLI,
            ["storage account list"]       = s_listStorageAccount_CLI,
            ["storage account delete"]     = s_deleteStorageAccount_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Storage processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
        static ExtensionIndex()
        {
            foreach (var metadata in s_cliTable.Values)
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));

                // Parameter set resolution rejects any argument outside the resolved set, so the
                // injected parameter must also be a member of every set.
                foreach (var parameterSet in metadata.ParameterSets?.Values ?? [])
                {
                    parameterSet.Parameters?.TryAdd("Profile", new ParameterReference("Profile"));
                }
            }
            s_cliTable.AddDefaultParameterSets();
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
