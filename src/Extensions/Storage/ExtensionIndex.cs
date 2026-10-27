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

        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = AddDataPlanePowerShellCommands(new()
        {
            ["New-AzStorageAccount"] = s_newStorageAccount_PS,
            // Get-AzStorageAccount resolves to one of these by parameter set: the cmdlet appends
            // its ParameterSetName to the command name, so each set binds to its own processor.
            ["Get-AzStorageAccount_AccountName"] = s_showStorageAccount_PS,
            ["Get-AzStorageAccount_ResourceGroup"] = s_listStorageAccount_PS,
            ["Remove-AzStorageAccount"] = s_removeStorageAccount_PS,
        });

        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex.
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = AddDataPlaneCliCommands(new()
        {
            ["storage account create"]     = s_newStorageAccount_CLI,
            ["storage account show"]       = s_showStorageAccount_CLI,
            ["storage account list"]       = s_listStorageAccount_CLI,
            ["storage account delete"]     = s_deleteStorageAccount_CLI,
        });

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

        private static Dictionary<string, CommandMetadata> AddDataPlaneCliCommands(Dictionary<string, CommandMetadata> table)
        {
            table.Add("storage account keys list", Command<ListStorageAccountKeysCommand>("storage account keys list",
                    [
                        Parameter("ResourceGroupName", "resource-group", typeof(string), "Storage account resource group.", true, ["g"]),
                        Parameter("AccountName", "account-name", typeof(string), "Storage account name.", true, ["n"]),
                        Parameter("SubscriptionId", "subscription-id", typeof(string), "ARM subscription ID. Defaults to the selected profile."),
                        Parameter("Profile", "profile", typeof(string), "Use the named connection profile."),
                        Parameter("ExpandKeyType", "expand-key-type", typeof(string), "Include Kerberos keys with kerb.", allowedValues: ["kerb"])
                    ]));
            AddBlob<ShowStorageBlobCommand>("show",
                [Parameter("IncludeTags", "include-tags", typeof(bool), "Include tags using an ETag-guarded tag lookup.")]);
            AddBlob<TestStorageBlobExistsCommand>("exists", []);
            AddBlob<RemoveStorageBlobCommand>("delete",
                [Parameter("DeleteSnapshots", "delete-snapshots", typeof(string), "Delete the base and snapshots, or snapshots only.",
                    allowedValues: ["include", "only"])]);
            AddBlob<SetStorageBlobTierCommand>("set-tier",
                [
                    Parameter("Tier", "tier", typeof(string), "Requested Block/Page access tier.", true,
                        allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                    Parameter("BlobType", "type", typeof(string), "Blob type: block (default) or page.", aliases: ["t"],
                        allowedValues: ["block", "page"]),
                    Parameter("RehydratePriority", "rehydrate-priority", typeof(string), "Block blob rehydration priority.", aliases: ["r"],
                        allowedValues: ["High", "Standard"])
                ], conditions: false);
            table.Add("storage blob list", Command<ListStorageBlobsCommand>("storage blob list",
                [
                    .. BlobAddressParameters(),
                    Parameter("Prefix", "prefix", typeof(string), "Return names beginning with this literal prefix."),
                    Parameter("Delimiter", "delimiter", typeof(string), "Nonempty hierarchy delimiter; omit or use empty for flat listing."),
                    Parameter("Include", "include", typeof(string), "Combined flags: c copy, d deleted, m metadata, s snapshots, t tags, v versions."),
                    Parameter("Marker", "marker", typeof(string), "Opaque continuation marker from the preceding listing."),
                    Parameter("NumResults", "num-results", typeof(string), "Positive 64-bit result limit (default 5000), or * for all; service pages are at most 5000."),
                    Parameter("ShowNextMarker", "show-next-marker", typeof(bool), "Append a nextMarker object (including null) to the result array.")
                ]));
            table.Add("storage blob copy start", Command<StartStorageBlobCopyCommand>("storage blob copy start",
                [
                    .. BlobAddressParameters("destination-container"),
                    Parameter("Blob", "destination-blob", typeof(string), "Exact destination blob; existing destination is overwritten unless conditioned.", true, ["b"]),
                    Parameter("SourceUri", "source-uri", typeof(string), "Public Azure Blob/Files source URI, optionally containing a read SAS.", aliases: ["u"]),
                    Parameter("SourceSas", "source-sas", typeof(string), "Existing read SAS for the source only; never included in output."),
                    Parameter("SourceAccountKey", "source-account-key", typeof(string), "Source Files account key used only to sign a short-lived read SAS; never persisted or returned."),
                    Parameter("UseSourceAccountKeyLookup", "use-source-account-key-lookup", typeof(bool), "Explicitly authorize ARM lookup of the source Files account key; never an automatic fallback."),
                    Parameter("SourceKeyResourceGroup", "source-key-resource-group", typeof(string), "Required source resource group when ARM key lookup is selected."),
                    Parameter("SourceKeySubscriptionId", "source-key-subscription-id", typeof(string), "Source ARM subscription; defaults to the source profile subscription."),
                    Parameter("SourceKeyName", "source-key-name", typeof(string), "Source key name for explicit ARM lookup; defaults to key1."),
                    Parameter("SourceStorageAccountName", "source-account-name", typeof(string), "Source account; defaults to destination for named sources."),
                    Parameter("SourceContainer", "source-container", typeof(string), "Named Blob source container."),
                    Parameter("SourceBlob", "source-blob", typeof(string), "Named Blob source name."),
                    Parameter("SourceSnapshot", "source-snapshot", typeof(string), "Blob source snapshot; cannot be combined with source-version-id."),
                    Parameter("SourceVersionId", "source-version-id", typeof(string), "Blob source version."),
                    Parameter("SourceShareName", "source-share", typeof(string), "Named Files source share."),
                    Parameter("SourcePath", "source-path", typeof(string), "Named Files source path."),
                    Parameter("SourceShareSnapshot", "source-share-snapshot", typeof(string), "Files source share snapshot."),
                    Parameter("EnableSourceFileBackupRequestIntent", "enable-source-file-backup-request-intent", typeof(bool), "Files OAuth intent; does not authorize a normal-copy source without a service SAS or explicit account-key signing."),
                    Parameter("LeaseId", "destination-lease-id", typeof(string), "Destination lease ID."),
                    Parameter("IfMatch", "destination-if-match", typeof(string), "Destination ETag match condition."),
                    Parameter("IfNoneMatch", "destination-if-none-match", typeof(string), "Destination non-match condition; * protects absent destinations."),
                    Parameter("IfModifiedSinceInput", "destination-if-modified-since", typeof(string), "Destination modified-since timestamp."),
                    Parameter("IfUnmodifiedSinceInput", "destination-if-unmodified-since", typeof(string), "Destination unmodified-since timestamp."),
                    Parameter("IfTags", "destination-tags-condition", typeof(string), "Destination SQL-like tag condition."),
                    Parameter("SourceIfMatch", "source-if-match", typeof(string), "Blob source ETag match condition."),
                    Parameter("SourceIfNoneMatch", "source-if-none-match", typeof(string), "Blob source non-match condition."),
                    Parameter("SourceIfModifiedSinceInput", "source-if-modified-since", typeof(string), "Blob source modified-since timestamp."),
                    Parameter("SourceIfUnmodifiedSinceInput", "source-if-unmodified-since", typeof(string), "Blob source unmodified-since timestamp."),
                    Parameter("SourceIfTags", "source-tags-condition", typeof(string), "Blob source SQL-like tag condition."),
                    Parameter("MetadataInput", "metadata", typeof(string[]), "Space-separated key=value metadata; nonempty unique case-insensitive keys."),
                    Parameter("TagsInput", "tags", typeof(string[]), "Space-separated key=value tags; bare keys mean empty values; unique case-sensitive keys."),
                    Parameter("Tier", "tier", typeof(string), "Compatible Block/Page destination tier.",
                        allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                    Parameter("RehydratePriority", "rehydrate-priority", typeof(string), "Block-tier rehydration priority.",
                        allowedValues: ["High", "Standard"])
                ]));
            AddBlob<UploadStorageBlobCommand>("upload",
                [
                    Parameter("File", "file", typeof(string), "Source file; name defaults to its filename.", true, ["f"]),
                    Parameter("BlobType", "type", typeof(string), "Block (default), Page (inferred for .vhd), or Append.", aliases: ["t"],
                        allowedValues: ["block", "page", "append"]),
                    Parameter("Overwrite", "overwrite", typeof(bool), "Replace existing content; false by default. Append otherwise appends."),
                    .. TransferParameters(),
                    Parameter("ContentType", "content-type", typeof(string), "Content MIME type; inferred from common filename extensions when omitted."),
                    Parameter("ContentEncoding", "content-encoding", typeof(string), "Content encoding."),
                    Parameter("ContentLanguage", "content-language", typeof(string), "Content language."),
                    Parameter("ContentDisposition", "content-disposition", typeof(string), "Content disposition."),
                    Parameter("CacheControl", "content-cache-control", typeof(string), "Content cache control."),
                    Parameter("ContentMd5Input", "content-md5", typeof(string), "Base64-encoded stored Content-MD5."),
                    Parameter("MetadataInput", "metadata", typeof(string[]), "Space-separated unique key=value metadata."),
                    Parameter("TagsInput", "tags", typeof(string[]), "Space-separated unique tags; bare keys mean empty values."),
                    Parameter("Tier", "tier", typeof(string), "Compatible Block/Page tier.",
                        allowedValues: [.. SetStorageBlobTierProcessor.BlockTiers, .. SetStorageBlobTierProcessor.PageTiers]),
                    Parameter("MaxSizeCondition", "maxsize-condition", typeof(long), "Maximum Append blob size in bytes.")
                ], nameRequired: false, selectors: false);
            AddBlob<DownloadStorageBlobCommand>("download",
                [
                    Parameter("File", "file", typeof(string), "Destination file; omit for byte-only standard output.", aliases: ["f"]),
                    Parameter("Overwrite", "overwrite", typeof(bool), "Overwrite the destination file; true by default."),
                    .. TransferParameters(),
                    Parameter("CheckMd5", "check-md5", typeof(bool), "Validate the stored whole-blob MD5 when available."),
                    Parameter("StartRange", "start-range", typeof(long), "Inclusive first byte."),
                    Parameter("EndRange", "end-range", typeof(long), "Inclusive last byte.")
                ]);
            table.Add("storage blob download-batch", Command<DownloadStorageBlobBatchCommand>("storage blob download-batch",
                [
                    .. BlobAddressParameters("source", ["s"]),
                    Parameter("Destination", "destination", typeof(string), "Existing destination directory.", true, ["d"]),
                    Parameter("Pattern", "pattern", typeof(string), "Python fnmatch-compatible wildcard selection."),
                    Parameter("Overwrite", "overwrite", typeof(bool), "Overwrite existing files; false by default."),
                    Parameter("DryRun", "dryrun", typeof(bool), "Plan and report without writing local files."),
                    .. TransferParameters(),
                    Parameter("CheckMd5", "check-md5", typeof(bool), "Validate stored MD5 when available."),
                    Parameter("LeaseId", "lease-id", typeof(string), "Lease ID."),
                    Parameter("IfMatch", "if-match", typeof(string), "ETag match condition."),
                    Parameter("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition."),
                    Parameter("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp."),
                    Parameter("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp."),
                    Parameter("IfTags", "tags-condition", typeof(string), "SQL-like tag condition.")
                ]));
            StorageFileFrontendRegistration.AddCli(table);
            return table;

            ParameterMetadata[] TransferParameters() =>
            [
                Parameter("Concurrency", "max-connections", typeof(int), "Bounded concurrent workers, 1-4 (default 2)."),
                Parameter("ChunkSize", "chunk-size", typeof(int), "Bytes per chunk; at most 8 MiB (Page: 4 MiB)."),
                Parameter("ValidateContent", "validate-content", typeof(bool), "Validate each transferred chunk with MD5."),
                Parameter("NoProgress", "no-progress", typeof(bool), "Suppress transfer progress, not errors or warnings.")
            ];

            ParameterMetadata[] BlobAddressParameters(string containerInvocation = "container-name", string[]? containerAliases = null) =>
            [
                Parameter("StorageAccountName", "account-name", typeof(string), "Storage account name. Use this or blob-endpoint."),
                Parameter("BlobEndpoint", "blob-endpoint", typeof(string), "HTTPS public Azure Blob account root, without query or resource path."),
                Parameter("Container", containerInvocation, typeof(string), "Container name.", true, containerAliases ?? ["c"]),
                Parameter("Profile", "profile", typeof(string), "Use the named connection profile."),
                Parameter("AuthMode", "auth-mode", typeof(string), "Entra login only; no account-key fallback.", allowedValues: ["login"]),
                Parameter("Timeout", "timeout", typeof(int), "Positive per-request service timeout in seconds.")
            ];

            void AddBlob<T>(string verb, ParameterMetadata[] extra, bool conditions = true, bool nameRequired = true, bool selectors = true) where T : IProcessor
            {
                List<ParameterMetadata> parameters =
                [
                    .. BlobAddressParameters(),
                    Parameter("Blob", "name", typeof(string), "Exact blob name; wildcards are not expanded.", nameRequired, ["n"]),
                    Parameter("LeaseId", "lease-id", typeof(string), "Lease ID."),
                    Parameter("IfTags", "tags-condition", typeof(string), "SQL-like condition on blob tags.")
                ];
                if (selectors)
                    parameters.AddRange(
                    [
                        Parameter("Snapshot", "snapshot", typeof(string), "Select a snapshot; cannot be combined with version-id."),
                        Parameter("VersionId", "version-id", typeof(string), "Select a version; cannot be combined with snapshot.")
                    ]);
                if (conditions)
                    parameters.AddRange(
                    [
                        Parameter("IfMatch", "if-match", typeof(string), "ETag match condition."),
                        Parameter("IfNoneMatch", "if-none-match", typeof(string), "ETag non-match condition."),
                        Parameter("IfModifiedSinceInput", "if-modified-since", typeof(string), "Modified-since timestamp; UTC if no offset is given."),
                        Parameter("IfUnmodifiedSinceInput", "if-unmodified-since", typeof(string), "Unmodified-since timestamp; UTC if no offset is given.")
                    ]);
                parameters.AddRange(extra);
                string invocation = $"storage blob {verb}";
                table.Add(invocation, Command<T>(invocation, [.. parameters]));
            }
        }

        private static Dictionary<string, CommandMetadata> AddDataPlanePowerShellCommands(Dictionary<string, CommandMetadata> table)
        {
            table.Add("New-AzStorageContext", Command<NewStorageContextProcessor>("New-AzStorageContext",
                    [
                        Parameter("StorageAccountName", "StorageAccountName", typeof(string), "Public Azure storage account name.", true),
                        Parameter("Profile", "Profile", typeof(string), "Use the named connection profile."),
                        Parameter("UseConnectedAccount", "UseConnectedAccount", typeof(bool), "Use Entra authentication. Defaults to true; false is unsupported."),
                        Parameter("EnableFileBackupRequestIntent", "EnableFileBackupRequestIntent", typeof(bool), "Explicitly enable backup intent for Azure Files OAuth.")
                    ]));
            table.Add("Get-AzStorageAccountKey", Command<ListStorageAccountKeysProcessor>("Get-AzStorageAccountKey",
                    [
                        Parameter("ResourceGroupName", "ResourceGroupName", typeof(string), "Storage account resource group.", true),
                        Parameter("AccountName", "Name", typeof(string), "Storage account name.", true, ["AccountName", "StorageAccountName"]),
                        Parameter("SubscriptionId", "SubscriptionId", typeof(string), "ARM subscription ID. Defaults to the selected profile."),
                        Parameter("Profile", "Profile", typeof(string), "Use the named connection profile."),
                        Parameter("ListKerbKey", "ListKerbKey", typeof(bool), "Include Kerberos keys.")
                    ]));
            table.Add("Get-AzStorageBlob", Command<GetStorageBlobsPowerShellCommand>("Get-AzStorageBlob",
                    [
                        Parameter("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context."),
                        Parameter("Container", "Container", typeof(string), "Container name."),
                        Parameter("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input."),
                        Parameter("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input."),
                        Parameter("Blob", "Blob", typeof(string), "Exact blob name."),
                        Parameter("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector."),
                        Parameter("VersionId", "VersionId", typeof(string), "Version selector."),
                        Parameter("ListMode", "ListMode", typeof(bool), "Use flat listing instead of exact HEAD."),
                        Parameter("Prefix", "Prefix", typeof(string), "Literal listing prefix."),
                        Parameter("NameFilter", "NameFilter", typeof(Func<string, bool>), "PowerShell-compiled name filter."),
                        Parameter("MaxCount", "MaxCount", typeof(int), "Maximum scanned entries, before filtering; nonpositive means unlimited."),
                        Parameter("ContinuationToken", "ContinuationToken", typeof(StorageBlobContinuationToken), "Native listing continuation."),
                        Parameter("IncludeDeleted", "IncludeDeleted", typeof(bool), "Include deleted blobs when listing."),
                        Parameter("IncludeVersion", "IncludeVersion", typeof(bool), "Include versions when listing by prefix."),
                        Parameter("IncludeTag", "IncludeTag", typeof(bool), "Include blob tags."),
                        Parameter("IfTags", "IfTags", typeof(string), "Tag condition for exact lookup."),
                        Parameter("Timeout", "Timeout", typeof(int), "Positive per-request service timeout."),
                        Parameter("Profile", "Profile", typeof(string), "Use the named connection profile.")
                    ]));
            table.Add("Remove-AzStorageBlob", Command<RemoveStorageBlobPowerShellCommand>("Remove-AzStorageBlob",
                    [
                        Parameter("Context", "Context", typeof(StorageContext), "Native public-Azure Entra context."),
                        Parameter("Container", "Container", typeof(string), "Container name."),
                        Parameter("Blob", "Blob", typeof(string), "Exact blob name; wildcards are not expanded."),
                        Parameter("ContainerObject", "ContainerObject", typeof(StorageBlobContainer), "Native container pipeline input."),
                        Parameter("InputObject", "InputObject", typeof(StorageBlob), "Native blob pipeline input."),
                        Parameter("Snapshot", "Snapshot", typeof(string), "UTC snapshot selector."),
                        Parameter("VersionId", "VersionId", typeof(string), "Version selector."),
                        Parameter("DeleteSnapshots", "DeleteSnapshots", typeof(string), "Include snapshots, or delete only snapshots.",
                            allowedValues: ["include", "only"]),
                        Parameter("PassThru", "PassThru", typeof(bool), "Emit true only after successful deletion."),
                        Parameter("IfTags", "IfTags", typeof(string), "Tag condition for deletion."),
                        Parameter("Timeout", "Timeout", typeof(int), "Positive per-request service timeout."),
                        Parameter("Profile", "Profile", typeof(string), "Use the named connection profile.")
                    ]));
            StorageBlobPowerShellRegistration.AddPowerShell(table);
            StorageFileFrontendRegistration.AddPowerShell(table);
            return table;
        }

        private static ParameterMetadata Parameter(string property, string invocation, Type type, string description,
            bool required = false, string[]? aliases = null, string[]? allowedValues = null) =>
            new(property, invocation, type, description, required, aliases ?? [], allowedValues ?? []);

        private static CommandMetadata Command<T>(string invocation, ParameterMetadata[] parameters) where T : IProcessor =>
            new(typeof(T).Name, invocation, [], typeof(T).FullName!, typeof(T).Assembly.GetName().Name!, typeof(T),
                parameters.ToDictionary(parameter => parameter.InvocationName, StringComparer.OrdinalIgnoreCase),
                parameters.SelectMany(parameter => parameter.Aliases.Select(alias => (alias, parameter.InvocationName)))
                    .ToDictionary(pair => pair.alias, pair => pair.InvocationName, StringComparer.OrdinalIgnoreCase));

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
