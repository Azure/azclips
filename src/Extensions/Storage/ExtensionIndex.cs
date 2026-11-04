// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

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
            }
        );

        // ── Storage data-plane PS Command Metadata ──
        private static readonly CommandMetadata s_newStorageContext_PS = new(
            Name: "NewStorageContextProcessor",
            InvocationName: "New-AzStorageContext",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.NewStorageContextProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(NewStorageContextProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["StorageAccountName"] = new("StorageAccountName", "StorageAccountName", typeof(string), "Public Azure storage account name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UseConnectedAccount"] = new("UseConnectedAccount", "UseConnectedAccount", typeof(bool), "Use Entra authentication. Defaults to true; false is unsupported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableFileBackupRequestIntent"] = new("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicitly enable backup intent for Azure Files OAuth.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_getStorageBlobs_PS = new(
            Name: "GetStorageBlobsPowerShellCommand",
            InvocationName: "Get-AzStorageBlob",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.GetStorageBlobsPowerShellCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(GetStorageBlobsPowerShellCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Context"] = new("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Container"] = new("Container", "Container", typeof(string), "Container name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ContainerObject"] = new("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["InputObject"] = new("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Blob"] = new("Blob", "Blob", typeof(string), "Exact blob name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Snapshot"] = new("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VersionId"] = new("VersionId", "VersionId", typeof(string), "Version selector.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ListMode"] = new("ListMode", "ListMode", typeof(bool), "Use flat listing instead of exact HEAD.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Prefix"] = new("Prefix", "Prefix", typeof(string), "Literal listing prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["NameFilter"] = new("NameFilter", "NameFilter", typeof(Func<string, bool>), "PowerShell-compiled name filter.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["MaxCount"] = new("MaxCount", "MaxCount", typeof(int), "Maximum scanned entries, before filtering; nonpositive means unlimited.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ContinuationToken"] = new("ContinuationToken", "ContinuationToken", typeof(StorageBlobContinuationToken), "Native listing continuation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IncludeDeleted"] = new("IncludeDeleted", "IncludeDeleted", typeof(bool), "Include deleted blobs when listing.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IncludeVersion"] = new("IncludeVersion", "IncludeVersion", typeof(bool), "Include versions when listing by prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IncludeTag"] = new("IncludeTag", "IncludeTag", typeof(bool), "Include blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IfTags"] = new("IfTags", "IfTags", typeof(string), "Tag condition for exact lookup.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Timeout"] = new("Timeout", "Timeout", typeof(int), "Positive per-request service timeout.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_removeStorageBlob_PS = new(
            Name: "RemoveStorageBlobPowerShellCommand",
            InvocationName: "Remove-AzStorageBlob",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.RemoveStorageBlobPowerShellCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(RemoveStorageBlobPowerShellCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Context"] = new("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Container"] = new("Container", "Container", typeof(string), "Container name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Blob"] = new("Blob", "Blob", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ContainerObject"] = new("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["InputObject"] = new("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Snapshot"] = new("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["VersionId"] = new("VersionId", "VersionId", typeof(string), "Version selector.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DeleteSnapshots"] = new("DeleteSnapshots", "DeleteSnapshots", typeof(string), "Include snapshots, or delete only snapshots.", IsRequired: false, Aliases: [], AllowedValues: ["include", "only"]),
                ["PassThru"] = new("PassThru", "PassThru", typeof(bool), "Emit true only after successful deletion.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IfTags"] = new("IfTags", "IfTags", typeof(string), "Tag condition for deletion.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Timeout"] = new("Timeout", "Timeout", typeof(int), "Positive per-request service timeout.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_setStorageBlobContent_PS = new(
            Name: "SetStorageBlobContentPowerShellCommand",
            InvocationName: "Set-AzStorageBlobContent",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.SetStorageBlobContentPowerShellCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(SetStorageBlobContentPowerShellCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Plan"] = new("Plan", "Plan", typeof(StorageBlobPowerShellPlan), "Locally validated and frozen Blob operation.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_getStorageBlobContent_PS = new(
            Name: "GetStorageBlobContentPowerShellCommand",
            InvocationName: "Get-AzStorageBlobContent",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.GetStorageBlobContentPowerShellCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(GetStorageBlobContentPowerShellCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Plan"] = new("Plan", "Plan", typeof(StorageBlobPowerShellPlan), "Locally validated and frozen Blob operation.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_startStorageBlobCopy_PS = new(
            Name: "StartStorageBlobCopyPowerShellCommand",
            InvocationName: "Start-AzStorageBlobCopy",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.StartStorageBlobCopyPowerShellCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(StartStorageBlobCopyPowerShellCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Plan"] = new("Plan", "Plan", typeof(StorageBlobPowerShellPlan), "Locally validated and frozen Blob operation.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_getStorageFile_PS = new(
            Name: "GetStorageFileProcessor",
            InvocationName: "Get-AzStorageFile",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.GetStorageFileProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(GetStorageFileProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Context"] = new("Context", "Context", typeof(StorageContext), "Native public Azure Entra context.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareName"] = new("ShareName", "ShareName", typeof(string), "Share name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareObject"] = new("ShareObject", "ShareObject", typeof(StorageFileShare), "Native share.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DirectoryObject"] = new("DirectoryObject", "DirectoryObject", typeof(StorageFileDirectory), "Native base directory.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Path"] = new("Path", "Path", typeof(string), "Relative Files path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareSnapshot"] = new("ShareSnapshot", "ShareSnapshot", typeof(string), "Read-only snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableFileBackupRequestIntent"] = new("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicit backup intent.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowTrailingDot"] = new("AllowTrailingDot", "AllowTrailingDot", typeof(bool), "Preserve trailing dots.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Timeout"] = new("Timeout", "Timeout", typeof(int), "Positive per-request service timeout.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Named public Azure profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ExcludeExtendedInfo"] = new("ExcludeExtendedInfo", "ExcludeExtendedInfo", typeof(bool), "Exclude extended properties when listing without Path.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_newStorageDirectory_PS = new(
            Name: "NewStorageDirectoryProcessor",
            InvocationName: "New-AzStorageDirectory",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.NewStorageDirectoryProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(NewStorageDirectoryProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Context"] = new("Context", "Context", typeof(StorageContext), "Native public Azure Entra context.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareName"] = new("ShareName", "ShareName", typeof(string), "Share name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareObject"] = new("ShareObject", "ShareObject", typeof(StorageFileShare), "Native share.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DirectoryObject"] = new("DirectoryObject", "DirectoryObject", typeof(StorageFileDirectory), "Native base directory.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Path"] = new("Path", "Path", typeof(string), "Relative Files path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ShareSnapshot"] = new("ShareSnapshot", "ShareSnapshot", typeof(string), "Read-only snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["EnableFileBackupRequestIntent"] = new("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicit backup intent.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["AllowTrailingDot"] = new("AllowTrailingDot", "AllowTrailingDot", typeof(bool), "Preserve trailing dots.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Timeout"] = new("Timeout", "Timeout", typeof(int), "Positive per-request service timeout.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Named public Azure profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Metadata"] = new("Metadata", "Metadata", typeof(IReadOnlyDictionary<string, string>), "Frozen metadata.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );

        private static readonly CommandMetadata s_showStorageBlob_CLI = new(
            Name: "ShowStorageBlobCommand",
            InvocationName: "storage blob show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ShowStorageBlobCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(ShowStorageBlobCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include-tags"] = new("IncludeTags", "include-tags", typeof(bool), "Include tags using an ETag-guarded tag lookup.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
                ["n"] = "name",
            }
        );

        private static readonly CommandMetadata s_testStorageBlobExists_CLI = new(
            Name: "TestStorageBlobExistsCommand",
            InvocationName: "storage blob exists",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.TestStorageBlobExistsCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(TestStorageBlobExistsCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
                ["n"] = "name",
            }
        );

        private static readonly CommandMetadata s_removeStorageBlob_CLI = new(
            Name: "RemoveStorageBlobCommand",
            InvocationName: "storage blob delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.RemoveStorageBlobCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(RemoveStorageBlobCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delete-snapshots"] = new("DeleteSnapshots", "delete-snapshots", typeof(string), "Delete the base and snapshots, or snapshots only.", IsRequired: false, Aliases: [], AllowedValues: ["include", "only"]),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
                ["n"] = "name",
            }
        );

        private static readonly CommandMetadata s_setStorageBlobTier_CLI = new(
            Name: "SetStorageBlobTierCommand",
            InvocationName: "storage blob set-tier",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.SetStorageBlobTierCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(SetStorageBlobTierCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["snapshot"] = new("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["version-id"] = new("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "Requested Block/Page access tier.", IsRequired: true, Aliases: [], AllowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                ["type"] = new("BlobType", "type", typeof(string), "Blob type: block (default) or page.", IsRequired: false, Aliases: ["t"], AllowedValues: ["block", "page"]),
                ["rehydrate-priority"] = new("RehydratePriority", "rehydrate-priority", typeof(string), "Block blob rehydration priority.", IsRequired: false, Aliases: ["r"], AllowedValues: ["High", "Standard"]),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
                ["n"] = "name",
                ["t"] = "type",
                ["r"] = "rehydrate-priority",
            }
        );

        private static readonly CommandMetadata s_listStorageBlobs_CLI = new(
            Name: "ListStorageBlobsCommand",
            InvocationName: "storage blob list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageBlobsCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(ListStorageBlobsCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Return names beginning with this literal prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["delimiter"] = new("Delimiter", "delimiter", typeof(string), "Nonempty hierarchy delimiter; omit or use empty for flat listing.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include"] = new("Include", "include", typeof(string), "Combined flags: c copy, d deleted, m metadata, s snapshots, t tags, v versions.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "Opaque continuation marker from the preceding listing.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["num-results"] = new("NumResults", "num-results", typeof(string), "Positive 64-bit result limit (default 5000), or * for all; service pages are at most 5000.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["show-next-marker"] = new("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker object (including null) to the result array.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
            }
        );

        private static readonly CommandMetadata s_startStorageBlobCopy_CLI = new(
            Name: "StartStorageBlobCopyCommand",
            InvocationName: "storage blob copy start",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.StartStorageBlobCopyCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(StartStorageBlobCopyCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-container"] = new("Container", "destination-container", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-blob"] = new("Blob", "destination-blob", typeof(string), "Exact destination blob; existing destination is overwritten unless conditioned.", IsRequired: true, Aliases: ["b"], AllowedValues: []),
                ["source-uri"] = new("SourceUri", "source-uri", typeof(string), "Public Azure Blob/Files source URI, optionally containing a read SAS.", IsRequired: false, Aliases: ["u"], AllowedValues: []),
                ["source-sas"] = new("SourceSas", "source-sas", typeof(string), "Existing read SAS for the source only; never included in output.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-account-key"] = new("SourceAccountKey", "source-account-key", typeof(string), "Source Files account key used only to sign a short-lived read SAS; never persisted or returned.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["use-source-account-key-lookup"] = new("UseSourceAccountKeyLookup", "use-source-account-key-lookup", typeof(bool), "Explicitly authorize ARM lookup of the source Files account key; never an automatic fallback.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-key-resource-group"] = new("SourceKeyResourceGroup", "source-key-resource-group", typeof(string), "Required source resource group when ARM key lookup is selected.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-key-subscription-id"] = new("SourceKeySubscriptionId", "source-key-subscription-id", typeof(string), "Source ARM subscription; defaults to the source profile subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-key-name"] = new("SourceKeyName", "source-key-name", typeof(string), "Source key name for explicit ARM lookup; defaults to key1.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-account-name"] = new("SourceStorageAccountName", "source-account-name", typeof(string), "Source account; defaults to destination for named sources.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-container"] = new("SourceContainer", "source-container", typeof(string), "Named Blob source container.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-blob"] = new("SourceBlob", "source-blob", typeof(string), "Named Blob source name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-snapshot"] = new("SourceSnapshot", "source-snapshot", typeof(string), "Blob source snapshot; cannot be combined with source-version-id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-version-id"] = new("SourceVersionId", "source-version-id", typeof(string), "Blob source version.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-share"] = new("SourceShareName", "source-share", typeof(string), "Named Files source share.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-path"] = new("SourcePath", "source-path", typeof(string), "Named Files source path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-share-snapshot"] = new("SourceShareSnapshot", "source-share-snapshot", typeof(string), "Files source share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-source-file-backup-request-intent"] = new("EnableSourceFileBackupRequestIntent", "enable-source-file-backup-request-intent", typeof(bool), "Files OAuth intent; does not authorize a normal-copy source without a service SAS or explicit account-key signing.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-lease-id"] = new("LeaseId", "destination-lease-id", typeof(string), "Destination lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-match"] = new("IfMatch", "destination-if-match", typeof(string), "Destination ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-none-match"] = new("IfNoneMatch", "destination-if-none-match", typeof(string), "Destination non-match condition; * protects absent destinations.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-modified-since"] = new("IfModifiedSinceInput", "destination-if-modified-since", typeof(string), "Destination modified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-if-unmodified-since"] = new("IfUnmodifiedSinceInput", "destination-if-unmodified-since", typeof(string), "Destination unmodified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination-tags-condition"] = new("IfTags", "destination-tags-condition", typeof(string), "Destination SQL-like tag condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-match"] = new("SourceIfMatch", "source-if-match", typeof(string), "Blob source ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-none-match"] = new("SourceIfNoneMatch", "source-if-none-match", typeof(string), "Blob source non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-modified-since"] = new("SourceIfModifiedSinceInput", "source-if-modified-since", typeof(string), "Blob source modified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-if-unmodified-since"] = new("SourceIfUnmodifiedSinceInput", "source-if-unmodified-since", typeof(string), "Blob source unmodified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source-tags-condition"] = new("SourceIfTags", "source-tags-condition", typeof(string), "Blob source SQL-like tag condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("MetadataInput", "metadata", typeof(string[]), "Space-separated key=value metadata; nonempty unique case-insensitive keys.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("TagsInput", "tags", typeof(string[]), "Space-separated key=value tags; bare keys mean empty values; unique case-sensitive keys.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "Compatible Block/Page destination tier.", IsRequired: false, Aliases: [], AllowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                ["rehydrate-priority"] = new("RehydratePriority", "rehydrate-priority", typeof(string), "Block-tier rehydration priority.", IsRequired: false, Aliases: [], AllowedValues: ["High", "Standard"]),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "destination-container",
                ["b"] = "destination-blob",
                ["u"] = "source-uri",
            }
        );

        private static readonly CommandMetadata s_uploadStorageBlob_CLI = new(
            Name: "UploadStorageBlobCommand",
            InvocationName: "storage blob upload",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.UploadStorageBlobCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(UploadStorageBlobCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["container-name"] = new("Container", "container-name", typeof(string), "Container name.", IsRequired: true, Aliases: ["c"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["file"] = new("File", "file", typeof(string), "Source file; name defaults to its filename.", IsRequired: true, Aliases: ["f"], AllowedValues: []),
                ["type"] = new("BlobType", "type", typeof(string), "Block (default), Page (inferred for .vhd), or Append.", IsRequired: false, Aliases: ["t"], AllowedValues: ["block", "page", "append"]),
                ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Replace existing content; false by default. Append otherwise appends.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["max-connections"] = new("Concurrency", "max-connections", typeof(int), "Bounded concurrent workers, 1-4 (default 2).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["chunk-size"] = new("ChunkSize", "chunk-size", typeof(int), "Bytes per chunk; at most 8 MiB (Page: 4 MiB).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validate-content"] = new("ValidateContent", "validate-content", typeof(bool), "Validate each transferred chunk with MD5.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-progress"] = new("NoProgress", "no-progress", typeof(bool), "Suppress transfer progress, not errors or warnings.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-type"] = new("ContentType", "content-type", typeof(string), "Content MIME type; inferred from common filename extensions when omitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("ContentEncoding", "content-encoding", typeof(string), "Content encoding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("ContentLanguage", "content-language", typeof(string), "Content language.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("ContentDisposition", "content-disposition", typeof(string), "Content disposition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("CacheControl", "content-cache-control", typeof(string), "Content cache control.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-md5"] = new("ContentMd5Input", "content-md5", typeof(string), "Base64-encoded stored Content-MD5.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("TagsInput", "tags", typeof(string[]), "Space-separated unique tags; bare keys mean empty values.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tier"] = new("Tier", "tier", typeof(string), "Compatible Block/Page tier.", IsRequired: false, Aliases: [], AllowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                ["maxsize-condition"] = new("MaxSizeCondition", "maxsize-condition", typeof(long), "Maximum Append blob size in bytes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["c"] = "container-name",
                ["n"] = "name",
                ["f"] = "file",
                ["t"] = "type",
            }
        );

        private static readonly CommandMetadata s_downloadStorageBlobBatch_CLI = new(
            Name: "DownloadStorageBlobBatchCommand",
            InvocationName: "storage blob download-batch",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.DownloadStorageBlobBatchCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(DownloadStorageBlobBatchCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["blob-endpoint"] = new("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["source"] = new("Container", "source", typeof(string), "Container name.", IsRequired: true, Aliases: ["s"], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["destination"] = new("Destination", "destination", typeof(string), "Existing destination directory.", IsRequired: true, Aliases: ["d"], AllowedValues: []),
                ["pattern"] = new("Pattern", "pattern", typeof(string), "Python fnmatch-compatible wildcard selection.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["overwrite"] = new("Overwrite", "overwrite", typeof(bool), "Overwrite existing files; false by default.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["dryrun"] = new("DryRun", "dryrun", typeof(bool), "Plan and report without writing local files.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["max-connections"] = new("Concurrency", "max-connections", typeof(int), "Bounded concurrent workers, 1-4 (default 2).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["chunk-size"] = new("ChunkSize", "chunk-size", typeof(int), "Bytes per chunk; at most 8 MiB (Page: 4 MiB).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validate-content"] = new("ValidateContent", "validate-content", typeof(bool), "Validate each transferred chunk with MD5.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-progress"] = new("NoProgress", "no-progress", typeof(bool), "Suppress transfer progress, not errors or warnings.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["check-md5"] = new("CheckMd5", "check-md5", typeof(bool), "Validate stored MD5 when available.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["lease-id"] = new("LeaseId", "lease-id", typeof(string), "Lease ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-match"] = new("IfMatch", "if-match", typeof(string), "ETag match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-none-match"] = new("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-modified-since"] = new("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["if-unmodified-since"] = new("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags-condition"] = new("IfTags", "tags-condition", typeof(string), "SQL-like tag condition.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["s"] = "source",
                ["d"] = "destination",
            }
        );

        private static readonly CommandMetadata s_uploadStorageFile_CLI = new(
            Name: "UploadStorageFileCommand",
            InvocationName: "storage file upload",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.UploadStorageFileCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(UploadStorageFileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Public Azure account name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["file-endpoint"] = new("FileEndpoint", "file-endpoint", typeof(string), "HTTPS public Azure Files account root, without query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "Share name.", IsRequired: true, Aliases: ["s"], AllowedValues: []),
                ["snapshot"] = new("ShareSnapshot", "snapshot", typeof(string), "Read-only share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-file-backup-request-intent"] = new("EnableFileBackupRequestIntent", "enable-file-backup-request-intent", typeof(bool), "Required explicit Files OAuth backup intent.", IsRequired: false, Aliases: ["backup-intent"], AllowedValues: []),
                ["disallow-trailing-dot"] = new("DisallowTrailingDot", "disallow-trailing-dot", typeof(bool), "Trim trailing dots from Files names; preserved by default.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive service timeout per request.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Named public Azure connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["path"] = new("Path", "path", typeof(string), "Remote file path; an existing directory or directory/ takes the source filename. Missing parents are created.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["source"] = new("Source", "source", typeof(string), "Local source file.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["content-type"] = new("ContentType", "content-type", typeof(string), "Content type; inferred from source extension if omitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-encoding"] = new("ContentEncoding", "content-encoding", typeof(string), "Content encoding.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-language"] = new("ContentLanguage", "content-language", typeof(string), "Content language.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-disposition"] = new("ContentDisposition", "content-disposition", typeof(string), "Content disposition.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-cache-control"] = new("CacheControl", "content-cache-control", typeof(string), "Cache control.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["content-md5"] = new("ContentMd5Input", "content-md5", typeof(string), "Base64-encoded 16-byte whole-file MD5.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["metadata"] = new("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validate-content"] = new("ValidateContent", "validate-content", typeof(bool), "Validate an MD5 hash for every uploaded range.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-progress"] = new("NoProgress", "no-progress", typeof(bool), "Suppress progress, not errors.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["max-connections"] = new("Concurrency", "max-connections", typeof(int), "One through four workers; default two.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["s"] = "share-name",
                ["backup-intent"] = "enable-file-backup-request-intent",
                ["p"] = "path",
            }
        );

        private static readonly CommandMetadata s_downloadStorageFile_CLI = new(
            Name: "DownloadStorageFileCommand",
            InvocationName: "storage file download",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.DownloadStorageFileCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(DownloadStorageFileCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Public Azure account name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["file-endpoint"] = new("FileEndpoint", "file-endpoint", typeof(string), "HTTPS public Azure Files account root, without query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "Share name.", IsRequired: true, Aliases: ["s"], AllowedValues: []),
                ["snapshot"] = new("ShareSnapshot", "snapshot", typeof(string), "Read-only share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-file-backup-request-intent"] = new("EnableFileBackupRequestIntent", "enable-file-backup-request-intent", typeof(bool), "Required explicit Files OAuth backup intent.", IsRequired: false, Aliases: ["backup-intent"], AllowedValues: []),
                ["disallow-trailing-dot"] = new("DisallowTrailingDot", "disallow-trailing-dot", typeof(bool), "Trim trailing dots from Files names; preserved by default.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive service timeout per request.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Named public Azure connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["path"] = new("Path", "path", typeof(string), "Exact source file path.", IsRequired: true, Aliases: ["p"], AllowedValues: []),
                ["dest"] = new("Destination", "dest", typeof(string), "Local file or existing directory; defaults to the source filename.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["open-mode"] = new("OpenMode", "open-mode", typeof(string), "wb replaces; ab appends and requires one worker.", IsRequired: false, Aliases: [], AllowedValues: ["wb", "ab"]),
                ["start-range"] = new("StartRange", "start-range", typeof(long), "Inclusive nonnegative start offset.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["end-range"] = new("EndRange", "end-range", typeof(long), "Inclusive end offset; requires start-range.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validate-content"] = new("ValidateContent", "validate-content", typeof(bool), "Validate every downloaded range.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["no-progress"] = new("NoProgress", "no-progress", typeof(bool), "Suppress progress, not errors.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["max-connections"] = new("Concurrency", "max-connections", typeof(int), "One through four workers; default two.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["s"] = "share-name",
                ["backup-intent"] = "enable-file-backup-request-intent",
                ["p"] = "path",
            }
        );

        private static readonly CommandMetadata s_listStorageFiles_CLI = new(
            Name: "ListStorageFilesCommand",
            InvocationName: "storage file list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageFilesCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(ListStorageFilesCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Public Azure account name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["file-endpoint"] = new("FileEndpoint", "file-endpoint", typeof(string), "HTTPS public Azure Files account root, without query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "Share name.", IsRequired: true, Aliases: ["s"], AllowedValues: []),
                ["snapshot"] = new("ShareSnapshot", "snapshot", typeof(string), "Read-only share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-file-backup-request-intent"] = new("EnableFileBackupRequestIntent", "enable-file-backup-request-intent", typeof(bool), "Required explicit Files OAuth backup intent.", IsRequired: false, Aliases: ["backup-intent"], AllowedValues: []),
                ["disallow-trailing-dot"] = new("DisallowTrailingDot", "disallow-trailing-dot", typeof(bool), "Trim trailing dots from Files names; preserved by default.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive service timeout per request.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Named public Azure connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["path"] = new("Path", "path", typeof(string), "Directory to list; omitted means share root.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Literal name prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "Opaque continuation marker.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["num-results"] = new("NumResults", "num-results", typeof(string), "Positive 64-bit limit or *; default all.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["show-next-marker"] = new("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker record.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["exclude-extended-info"] = new("ExcludeExtendedInfo", "exclude-extended-info", typeof(bool), "Exclude extended Files properties.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["s"] = "share-name",
                ["backup-intent"] = "enable-file-backup-request-intent",
                ["p"] = "path",
            }
        );

        private static readonly CommandMetadata s_listStorageDirectories_CLI = new(
            Name: "ListStorageDirectoriesCommand",
            InvocationName: "storage directory list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Storage.Customization.ListStorageDirectoriesCommand",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Storage",
            ProcessorType: typeof(ListStorageDirectoriesCommand),
            Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["account-name"] = new("StorageAccountName", "account-name", typeof(string), "Public Azure account name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["file-endpoint"] = new("FileEndpoint", "file-endpoint", typeof(string), "HTTPS public Azure Files account root, without query.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["share-name"] = new("ShareName", "share-name", typeof(string), "Share name.", IsRequired: true, Aliases: ["s"], AllowedValues: []),
                ["snapshot"] = new("ShareSnapshot", "snapshot", typeof(string), "Read-only share snapshot.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["enable-file-backup-request-intent"] = new("EnableFileBackupRequestIntent", "enable-file-backup-request-intent", typeof(bool), "Required explicit Files OAuth backup intent.", IsRequired: false, Aliases: ["backup-intent"], AllowedValues: []),
                ["disallow-trailing-dot"] = new("DisallowTrailingDot", "disallow-trailing-dot", typeof(bool), "Trim trailing dots from Files names; preserved by default.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["auth-mode"] = new("AuthMode", "auth-mode", typeof(string), "Entra login only; no key fallback.", IsRequired: false, Aliases: [], AllowedValues: ["login"]),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Positive service timeout per request.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["profile"] = new("Profile", "profile", typeof(string), "Named public Azure connection profile.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("Path", "name", typeof(string), "Directory to list; omitted means share root.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["prefix"] = new("Prefix", "prefix", typeof(string), "Literal name prefix.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["marker"] = new("Marker", "marker", typeof(string), "Opaque continuation marker.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["num-results"] = new("NumResults", "num-results", typeof(string), "Positive 64-bit limit or *; default all.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["show-next-marker"] = new("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker record.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["exclude-extended-info"] = new("ExcludeExtendedInfo", "exclude-extended-info", typeof(bool), "Exclude extended Files properties.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["s"] = "share-name",
                ["backup-intent"] = "enable-file-backup-request-intent",
                ["n"] = "name",
            }
        );

        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzStorageAccount"] = s_newStorageAccount_PS,
            // Get-AzStorageAccount resolves to one of these by parameter set: the cmdlet appends
            // its ParameterSetName to the command name, so each set binds to its own processor.
            ["Get-AzStorageAccount_AccountName"] = s_showStorageAccount_PS,
            ["Get-AzStorageAccount_ResourceGroup"] = s_listStorageAccount_PS,
            ["Remove-AzStorageAccount"] = s_removeStorageAccount_PS,
            ["New-AzStorageContext"] = s_newStorageContext_PS,
            ["Get-AzStorageBlob"] = s_getStorageBlobs_PS,
            ["Remove-AzStorageBlob"] = s_removeStorageBlob_PS,
            ["Set-AzStorageBlobContent"] = s_setStorageBlobContent_PS,
            ["Get-AzStorageBlobContent"] = s_getStorageBlobContent_PS,
            ["Start-AzStorageBlobCopy"] = s_startStorageBlobCopy_PS,
            ["Get-AzStorageFile"] = s_getStorageFile_PS,
            ["New-AzStorageDirectory"] = s_newStorageDirectory_PS,
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
            ["storage blob show"] = s_showStorageBlob_CLI,
            ["storage blob exists"] = s_testStorageBlobExists_CLI,
            ["storage blob delete"] = s_removeStorageBlob_CLI,
            ["storage blob set-tier"] = s_setStorageBlobTier_CLI,
            ["storage blob list"] = s_listStorageBlobs_CLI,
            ["storage blob copy start"] = s_startStorageBlobCopy_CLI,
            ["storage blob upload"] = s_uploadStorageBlob_CLI,
            ["storage blob download-batch"] = s_downloadStorageBlobBatch_CLI,
            ["storage file upload"] = s_uploadStorageFile_CLI,
            ["storage file download"] = s_downloadStorageFile_CLI,
            ["storage file list"] = s_listStorageFiles_CLI,
            ["storage directory list"] = s_listStorageDirectories_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Storage processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
        static ExtensionIndex()
        {
            foreach (var (name, metadata) in s_cliTable.ToArray())
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
                var references = new Dictionary<string, ParameterReference>(StringComparer.OrdinalIgnoreCase);
                foreach (var (parameterName, parameter) in metadata.Parameters.ToArray())
                {
                    metadata.Parameters[parameterName] = parameter with
                    {
                        ParameterSets = new Dictionary<string, ParameterSetReference>
                        {
                            ["Default"] = new("Default", IsRequired: parameter.IsRequired)
                        }
                    };
                    references.Add(parameter.Name, new(parameter.Name, IsRequired: parameter.IsRequired));
                }
                s_cliTable[name] = metadata with
                {
                    ParameterSets = new Dictionary<string, ParameterSetMetadata>
                    {
                        ["Default"] = new("Default", IsDefault: true, Parameters: references)
                    }
                };
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
