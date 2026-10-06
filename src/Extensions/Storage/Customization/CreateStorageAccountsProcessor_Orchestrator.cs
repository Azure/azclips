using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public partial class CreateStorageAccountsProcessor_Orchestrator : StorageProcessorBase<object, StorageAccount>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string? AccessTier { get; set; }
    public object? EnableHttpsTrafficOnly { get; set; }
    public object? AllowBlobPublicAccess { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public object? EnableHierarchicalNamespace { get; set; }
    public object? EnableSftp { get; set; }
    public object? EnableLocalUser { get; set; }
    public object? EnableNfsV3 { get; set; }
    public string? DnsEndpointType { get; set; }
    public List<string>? Zones { get; set; }

    public List<string>? EncryptionServices { get; set; }
    public string? EncryptionKeySource { get; set; }
    public string? EncryptionKeyName { get; set; }
    public string? EncryptionKeyVault { get; set; }
    public string? EncryptionKeyVersion { get; set; }
    public object? AssignIdentity { get; set; }
    public string? IdentityType { get; set; }
    public List<string>? UserAssignedIdentityIds { get; set; }
    public string? KeyVaultUserAssignedIdentityId { get; set; }
    public string? KeyVaultFederatedClientId { get; set; }
    public string? FederatedIdentityClientId { get; set; }
    public string? EncryptionKeyTypeForTable { get; set; }
    public string? EncryptionKeyTypeForQueue { get; set; }
    public object? RequireInfrastructureEncryption { get; set; }

    public object? EnableFilesAadds { get; set; }
    public object? EnableFilesAdds { get; set; }
    public object? EnableFilesAadkerb { get; set; }
    public string? DomainName { get; set; }
    public string? NetBiosDomainName { get; set; }
    public string? ForestName { get; set; }
    public string? DomainGuid { get; set; }
    public string? DomainSid { get; set; }
    public string? AzureStorageSid { get; set; }
    public string? SamAccountName { get; set; }
    public string? AccountType { get; set; }
    public string? DefaultSharePermission { get; set; }
    public object? EnableSmbOAuth { get; set; }

    public List<string>? Bypass { get; set; }
    public string? DefaultAction { get; set; }
    public string? Subnet { get; set; }
    public string? VirtualNetworkName { get; set; }
    public List<string>? VirtualNetworkRules { get; set; }
    public List<string>? IpRules { get; set; }
    public List<string>? ResourceAccessRules { get; set; }
    public string? NetworkRuleAction { get; set; }
    public string? RoutingChoice { get; set; }
    public object? PublishMicrosoftEndpoints { get; set; }
    public object? PublishInternetEndpoints { get; set; }
    public string? EdgeZone { get; set; }
    public string? ZonePlacementPolicy { get; set; }
    public object? EnableBlobGeoPriorityReplication { get; set; }
    public object? PublishIpv6Endpoint { get; set; }
    public string? AllowedCopyScope { get; set; }

    public object? AllowSharedKeyAccess { get; set; }
    public string? SasExpirationPeriod { get; set; }
    public string? SasExpirationAction { get; set; }
    public object? KeyExpirationPeriodInDays { get; set; }
    public object? AllowCrossTenantReplication { get; set; }
    public object? EnableAccountLevelImmutability { get; set; }
    public object? ImmutabilityPeriodSinceCreationInDays { get; set; }
    public string? ImmutabilityPolicyState { get; set; }
    public object? AllowProtectedAppendWrites { get; set; }
    public object? EnableLargeFileShare { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";

    protected override object? CreateRequestBody()
    {
        var body = new JsonObject
        {
            ["sku"] = new JsonObject { ["name"] = SkuName },
            ["kind"] = Kind,
            ["location"] = Location,
        };

        if (Tags is not null)
        {
            body["tags"] = JsonSerializer.SerializeToNode(Tags, JsonOptions);
        }
        if (Zones is { Count: > 0 })
        {
            body["zones"] = JsonSerializer.SerializeToNode(Zones, JsonOptions);
        }
        if (!string.IsNullOrWhiteSpace(EdgeZone))
        {
            body["extendedLocation"] = new JsonObject
            {
                ["name"] = EdgeZone,
                ["type"] = "EdgeZone",
            };
        }

        if (BuildIdentity() is { } identity)
        {
            body["identity"] = identity;
        }

        var properties = new JsonObject();
        AddString(properties, "accessTier", AccessTier);
        AddBool(properties, "supportsHttpsTrafficOnly", EnableHttpsTrafficOnly);
        AddBool(properties, "allowBlobPublicAccess", AllowBlobPublicAccess);
        AddString(properties, "minimumTlsVersion", NormalizeMinimumTlsVersion(MinimumTlsVersion));
        AddString(properties, "publicNetworkAccess", PublicNetworkAccess);
        AddBool(properties, "isHnsEnabled", EnableHierarchicalNamespace);
        AddBool(properties, "isSftpEnabled", EnableSftp);
        AddBool(properties, "isLocalUserEnabled", EnableLocalUser);
        AddBool(properties, "isNfsV3Enabled", EnableNfsV3);
        AddString(properties, "dnsEndpointType", DnsEndpointType);
        AddString(properties, "largeFileSharesState", IsTrue(EnableLargeFileShare) ? "Enabled" : null);
        AddString(properties, "allowedCopyScope", AllowedCopyScope);
        AddBool(properties, "allowSharedKeyAccess", AllowSharedKeyAccess);
        AddBool(properties, "allowCrossTenantReplication", AllowCrossTenantReplication);

        if (BuildEncryption() is { } encryption)
        {
            properties["encryption"] = encryption;
        }
        if (BuildAzureFilesAuthentication() is { } azureFilesAuthentication)
        {
            properties["azureFilesIdentityBasedAuthentication"] = azureFilesAuthentication;
        }
        if (BuildNetworkAcls() is { } networkAcls)
        {
            properties["networkAcls"] = networkAcls;
        }
        if (BuildRoutingPreference() is { } routingPreference)
        {
            properties["routingPreference"] = routingPreference;
        }
        if (BuildSasPolicy() is { } sasPolicy)
        {
            properties["sasPolicy"] = sasPolicy;
        }
        if (ToInt(KeyExpirationPeriodInDays) is { } keyExpirationPeriodInDays)
        {
            properties["keyPolicy"] = new JsonObject { ["keyExpirationPeriodInDays"] = keyExpirationPeriodInDays };
        }
        if (BuildImmutableStorageWithVersioning() is { } immutableStorage)
        {
            properties["immutableStorageWithVersioning"] = immutableStorage;
        }
        if (!string.IsNullOrWhiteSpace(ZonePlacementPolicy))
        {
            properties["placement"] = new JsonObject { ["zonePlacementPolicy"] = ZonePlacementPolicy };
        }
        if (EnableBlobGeoPriorityReplication is not null)
        {
            properties["geoPriorityReplicationStatus"] = new JsonObject { ["isBlobEnabled"] = ToBool(EnableBlobGeoPriorityReplication) };
        }
        if (PublishIpv6Endpoint is not null)
        {
            properties["dualStackEndpointPreference"] = new JsonObject { ["publishIpv6Endpoint"] = ToBool(PublishIpv6Endpoint) };
        }

        body["properties"] = properties;
        return body;
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

    private JsonObject? BuildIdentity()
    {
        bool hasIdentity = IsTrue(AssignIdentity)
            || !string.IsNullOrWhiteSpace(IdentityType)
            || UserAssignedIdentityIds is { Count: > 0 };
        if (!hasIdentity)
        {
            return null;
        }

        var identity = new JsonObject
        {
            ["type"] = !string.IsNullOrWhiteSpace(IdentityType)
                ? NormalizeIdentityType(IdentityType)
                : "SystemAssigned",
        };

        if (UserAssignedIdentityIds is { Count: > 0 })
        {
            var userAssignedIdentities = new JsonObject();
            foreach (string id in UserAssignedIdentityIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                userAssignedIdentities[id] = new JsonObject();
            }
            if (userAssignedIdentities.Count > 0)
            {
                identity["userAssignedIdentities"] = userAssignedIdentities;
            }
        }

        return identity;
    }

    private JsonObject? BuildEncryption()
    {
        bool hasKeyVaultProperties = !string.IsNullOrWhiteSpace(EncryptionKeyName)
            || !string.IsNullOrWhiteSpace(EncryptionKeyVault)
            || !string.IsNullOrWhiteSpace(EncryptionKeyVersion);
        bool hasEncryptionIdentity = !string.IsNullOrWhiteSpace(KeyVaultUserAssignedIdentityId)
            || !string.IsNullOrWhiteSpace(KeyVaultFederatedClientId)
            || !string.IsNullOrWhiteSpace(FederatedIdentityClientId);
        bool hasEncryption = EncryptionServices is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(EncryptionKeySource)
            || hasKeyVaultProperties
            || hasEncryptionIdentity
            || !string.IsNullOrWhiteSpace(EncryptionKeyTypeForTable)
            || !string.IsNullOrWhiteSpace(EncryptionKeyTypeForQueue)
            || IsTrue(RequireInfrastructureEncryption);

        if (!hasEncryption)
        {
            return null;
        }

        var encryption = new JsonObject
        {
            ["keySource"] = !string.IsNullOrWhiteSpace(EncryptionKeySource)
                ? EncryptionKeySource
                : hasKeyVaultProperties ? "Microsoft.Keyvault" : "Microsoft.Storage",
        };

        var services = new JsonObject();
        foreach (string serviceName in EncryptionServices ?? [])
        {
            string normalizedServiceName = NormalizeStorageServiceName(serviceName);
            if (!string.IsNullOrWhiteSpace(normalizedServiceName))
            {
                services[normalizedServiceName] = new JsonObject { ["enabled"] = true };
            }
        }
        AddEncryptionServiceKeyType(services, "table", EncryptionKeyTypeForTable);
        AddEncryptionServiceKeyType(services, "queue", EncryptionKeyTypeForQueue);
        if (services.Count > 0)
        {
            encryption["services"] = services;
        }

        if (hasKeyVaultProperties)
        {
            var keyVaultProperties = new JsonObject();
            AddString(keyVaultProperties, "keyName", EncryptionKeyName);
            AddString(keyVaultProperties, "keyVaultUri", EncryptionKeyVault);
            AddString(keyVaultProperties, "keyVersion", EncryptionKeyVersion);
            encryption["keyVaultProperties"] = keyVaultProperties;
        }
        if (hasEncryptionIdentity)
        {
            var encryptionIdentity = new JsonObject();
            AddString(encryptionIdentity, "encryptionUserAssignedIdentity", KeyVaultUserAssignedIdentityId);
            AddString(encryptionIdentity, "encryptionFederatedIdentityClientId", KeyVaultFederatedClientId ?? FederatedIdentityClientId);
            encryption["encryptionIdentity"] = encryptionIdentity;
        }
        if (IsTrue(RequireInfrastructureEncryption))
        {
            encryption["requireInfrastructureEncryption"] = true;
        }

        return encryption;
    }

    private JsonObject? BuildAzureFilesAuthentication()
    {
        bool hasActiveDirectoryProperties = !string.IsNullOrWhiteSpace(DomainName)
            || !string.IsNullOrWhiteSpace(NetBiosDomainName)
            || !string.IsNullOrWhiteSpace(ForestName)
            || !string.IsNullOrWhiteSpace(DomainGuid)
            || !string.IsNullOrWhiteSpace(DomainSid)
            || !string.IsNullOrWhiteSpace(AzureStorageSid)
            || !string.IsNullOrWhiteSpace(SamAccountName)
            || !string.IsNullOrWhiteSpace(AccountType);
        bool hasAuthentication = EnableFilesAadds is not null
            || EnableFilesAdds is not null
            || EnableFilesAadkerb is not null
            || hasActiveDirectoryProperties
            || !string.IsNullOrWhiteSpace(DefaultSharePermission)
            || EnableSmbOAuth is not null;

        if (!hasAuthentication)
        {
            return null;
        }

        var authentication = new JsonObject();
        string? directoryServiceOptions = null;
        if (ToBool(EnableFilesAadds) is { } enableFilesAadds)
        {
            directoryServiceOptions = enableFilesAadds ? "AADDS" : "None";
        }
        if (ToBool(EnableFilesAadkerb) is { } enableFilesAadkerb)
        {
            directoryServiceOptions = enableFilesAadkerb ? "AADKERB" : "None";
        }
        if (ToBool(EnableFilesAdds) is { } enableFilesAdds)
        {
            directoryServiceOptions = enableFilesAdds ? "AD" : "None";
        }
        AddString(authentication, "directoryServiceOptions", directoryServiceOptions ?? "None");

        var activeDirectoryProperties = new JsonObject();
        AddString(activeDirectoryProperties, "domainName", DomainName);
        AddString(activeDirectoryProperties, "netBiosDomainName", NetBiosDomainName);
        AddString(activeDirectoryProperties, "forestName", ForestName);
        AddString(activeDirectoryProperties, "domainGuid", DomainGuid);
        AddString(activeDirectoryProperties, "domainSid", DomainSid);
        AddString(activeDirectoryProperties, "azureStorageSid", AzureStorageSid);
        AddString(activeDirectoryProperties, "samAccountName", SamAccountName);
        AddString(activeDirectoryProperties, "accountType", AccountType);
        if (activeDirectoryProperties.Count > 0)
        {
            authentication["activeDirectoryProperties"] = activeDirectoryProperties;
        }

        AddString(authentication, "defaultSharePermission", DefaultSharePermission);
        if (EnableSmbOAuth is not null)
        {
            authentication["smbOAuthSettings"] = new JsonObject { ["isSmbOAuthEnabled"] = ToBool(EnableSmbOAuth) };
        }

        return authentication;
    }

    private JsonObject? BuildNetworkAcls()
    {
        bool hasNetworkAcls = Bypass is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(DefaultAction)
            || !string.IsNullOrWhiteSpace(Subnet)
            || VirtualNetworkRules is { Count: > 0 }
            || IpRules is { Count: > 0 }
            || ResourceAccessRules is { Count: > 0 };
        if (!hasNetworkAcls)
        {
            return null;
        }

        var networkAcls = new JsonObject();
        if (Bypass is { Count: > 0 })
        {
            networkAcls["bypass"] = string.Join(',', Bypass.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        AddString(networkAcls, "defaultAction", DefaultAction);

        var virtualNetworkRules = new JsonArray();
        foreach (string virtualNetworkRule in VirtualNetworkRules ?? [])
        {
            AddVirtualNetworkRule(virtualNetworkRules, virtualNetworkRule);
        }
        if (!string.IsNullOrWhiteSpace(Subnet))
        {
            AddVirtualNetworkRule(virtualNetworkRules, ResolveSubnetResourceId(Subnet));
        }
        if (virtualNetworkRules.Count > 0)
        {
            networkAcls["virtualNetworkRules"] = virtualNetworkRules;
        }

        var ipRules = new JsonArray();
        foreach (string ipRule in IpRules ?? [])
        {
            if (!string.IsNullOrWhiteSpace(ipRule))
            {
                ipRules.Add(new JsonObject
                {
                    ["value"] = ipRule,
                    ["action"] = string.IsNullOrWhiteSpace(NetworkRuleAction) ? "Allow" : NetworkRuleAction,
                });
            }
        }
        if (ipRules.Count > 0)
        {
            networkAcls["ipRules"] = ipRules;
        }

        var resourceAccessRules = new JsonArray();
        foreach (string resourceAccessRule in ResourceAccessRules ?? [])
        {
            if (BuildResourceAccessRule(resourceAccessRule) is { } rule)
            {
                resourceAccessRules.Add(rule);
            }
        }
        if (resourceAccessRules.Count > 0)
        {
            networkAcls["resourceAccessRules"] = resourceAccessRules;
        }

        return networkAcls;
    }

    private JsonObject? BuildRoutingPreference()
    {
        bool hasRoutingPreference = !string.IsNullOrWhiteSpace(RoutingChoice)
            || PublishMicrosoftEndpoints is not null
            || PublishInternetEndpoints is not null;
        if (!hasRoutingPreference)
        {
            return null;
        }

        var routingPreference = new JsonObject();
        AddString(routingPreference, "routingChoice", RoutingChoice);
        AddBool(routingPreference, "publishMicrosoftEndpoints", PublishMicrosoftEndpoints);
        AddBool(routingPreference, "publishInternetEndpoints", PublishInternetEndpoints);
        return routingPreference;
    }

    private JsonObject? BuildSasPolicy()
    {
        if (string.IsNullOrWhiteSpace(SasExpirationPeriod) && string.IsNullOrWhiteSpace(SasExpirationAction))
        {
            return null;
        }

        var sasPolicy = new JsonObject();
        AddString(sasPolicy, "sasExpirationPeriod", SasExpirationPeriod);
        AddString(sasPolicy, "expirationAction", string.IsNullOrWhiteSpace(SasExpirationAction) && !string.IsNullOrWhiteSpace(SasExpirationPeriod) ? "Log" : SasExpirationAction);
        return sasPolicy;
    }

    private JsonObject? BuildImmutableStorageWithVersioning()
    {
        bool hasImmutabilityPolicy = ImmutabilityPeriodSinceCreationInDays is not null
            || !string.IsNullOrWhiteSpace(ImmutabilityPolicyState)
            || AllowProtectedAppendWrites is not null;
        bool hasImmutableStorage = EnableAccountLevelImmutability is not null || hasImmutabilityPolicy;
        if (!hasImmutableStorage)
        {
            return null;
        }

        var immutableStorage = new JsonObject
        {
            ["enabled"] = ToBool(EnableAccountLevelImmutability) ?? hasImmutabilityPolicy,
        };
        if (hasImmutabilityPolicy)
        {
            var immutabilityPolicy = new JsonObject();
            if (ToInt(ImmutabilityPeriodSinceCreationInDays) is { } immutabilityPeriod)
            {
                immutabilityPolicy["immutabilityPeriodSinceCreationInDays"] = immutabilityPeriod;
            }
            AddString(immutabilityPolicy, "state", ImmutabilityPolicyState);
            AddBool(immutabilityPolicy, "allowProtectedAppendWrites", AllowProtectedAppendWrites);
            immutableStorage["immutabilityPolicy"] = immutabilityPolicy;
        }

        return immutableStorage;
    }

    private void AddVirtualNetworkRule(JsonArray virtualNetworkRules, string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return;
        }

        virtualNetworkRules.Add(new JsonObject
        {
            ["id"] = resourceId,
            ["action"] = string.IsNullOrWhiteSpace(NetworkRuleAction) ? "Allow" : NetworkRuleAction,
        });
    }

    private string ResolveSubnetResourceId(string subnet)
    {
        if (subnet.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(VirtualNetworkName))
        {
            return subnet;
        }

        return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VirtualNetworkName}/subnets/{subnet}";
    }

    private static JsonObject? BuildResourceAccessRule(string resourceAccessRule)
    {
        if (string.IsNullOrWhiteSpace(resourceAccessRule))
        {
            return null;
        }

        var rule = new JsonObject();
        foreach (string part in resourceAccessRule.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
            {
                continue;
            }

            if (pair[0].Equals("tenantId", StringComparison.OrdinalIgnoreCase))
            {
                rule["tenantId"] = pair[1];
            }
            else if (pair[0].Equals("resourceId", StringComparison.OrdinalIgnoreCase))
            {
                rule["resourceId"] = pair[1];
            }
        }

        return rule.Count > 0 ? rule : null;
    }

    private static void AddEncryptionServiceKeyType(JsonObject services, string serviceName, string? keyType)
    {
        if (string.IsNullOrWhiteSpace(keyType))
        {
            return;
        }

        services[serviceName] = new JsonObject
        {
            ["enabled"] = true,
            ["keyType"] = keyType,
        };
    }

    private static void AddString(JsonObject target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[name] = value;
        }
    }

    private static void AddBool(JsonObject target, string name, object? value)
    {
        if (ToBool(value) is { } boolValue)
        {
            target[name] = boolValue;
        }
    }

    private static bool IsTrue(object? value) => ToBool(value) == true;

    private static bool? ToBool(object? value) => value switch
    {
        null => null,
        bool boolValue => boolValue,
        string stringValue when bool.TryParse(stringValue, out bool parsed) => parsed,
        _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
    };

    private static int? ToInt(object? value) => value switch
    {
        null => null,
        int intValue => intValue,
        string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
        _ => Convert.ToInt32(value, CultureInfo.InvariantCulture),
    };

    private static string NormalizeStorageServiceName(string value) => value.Trim().ToLowerInvariant() switch
    {
        "blob" => "blob",
        "file" => "file",
        "queue" => "queue",
        "table" => "table",
        _ => value.Trim(),
    };

    private static string NormalizeIdentityType(string value) => value.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase) switch
    {
        "SystemAssigned" => "SystemAssigned",
        "UserAssigned" => "UserAssigned",
        "SystemAssignedUserAssigned" => "SystemAssigned,UserAssigned",
        "SystemAssigned,UserAssigned" => "SystemAssigned,UserAssigned",
        "None" => "None",
        _ => value,
    };

    private static string? NormalizeMinimumTlsVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Replace(".", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant() switch
        {
            "TLS10" or "TLS11" => "TLS1_2",
            "TLS12" => "TLS1_2",
            "TLS13" => "TLS1_3",
            _ => value,
        };
    }
}