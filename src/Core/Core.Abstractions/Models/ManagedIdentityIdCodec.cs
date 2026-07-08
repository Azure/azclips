// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>Encodes and decodes the persisted managed identity key.</summary>
public static class ManagedIdentityIdCodec
{
    /// <summary>Formats a managed identity key.</summary>
    public static string Format(ManagedIdentityIdType idType, string? idValue) => idType switch
    {
        ManagedIdentityIdType.SystemAssigned => "MSI@",
        ManagedIdentityIdType.ClientId => $"MSIClient@{idValue}",
        ManagedIdentityIdType.ObjectId => $"MSIObject@{idValue}",
        ManagedIdentityIdType.ResourceId => $"MSIResource@{idValue}",
        _ => throw new System.ArgumentException($"Unsupported ManagedIdentityIdType: {idType}"),
    };

    /// <summary>Formats a key from the request's managed identity fields.</summary>
    public static string Format(LoginRequest request)
    {
        if (!string.IsNullOrEmpty(request.ManagedIdentityClientId))
            return Format(ManagedIdentityIdType.ClientId, request.ManagedIdentityClientId);
        if (!string.IsNullOrEmpty(request.ManagedIdentityObjectId))
            return Format(ManagedIdentityIdType.ObjectId, request.ManagedIdentityObjectId);
        if (!string.IsNullOrEmpty(request.ManagedIdentityResourceId))
            return Format(ManagedIdentityIdType.ResourceId, request.ManagedIdentityResourceId);
        return Format(ManagedIdentityIdType.SystemAssigned, null);
    }

    /// <summary>Parses a managed identity key.</summary>
    public static (ManagedIdentityIdType IdType, string? IdValue) Parse(string? combinedId)
    {
        if (string.IsNullOrEmpty(combinedId) || combinedId == "MSI@")
            return (ManagedIdentityIdType.SystemAssigned, null);

        var atIndex = combinedId.IndexOf('@');
        if (atIndex < 0)
            return (ManagedIdentityIdType.SystemAssigned, null);

        var prefix = combinedId[..atIndex];
        var value = combinedId[(atIndex + 1)..];

        return prefix switch
        {
            "MSI" => (ManagedIdentityIdType.SystemAssigned, null),
            "MSIClient" => (ManagedIdentityIdType.ClientId, value),
            "MSIObject" => (ManagedIdentityIdType.ObjectId, value),
            "MSIResource" => (ManagedIdentityIdType.ResourceId, value),
            _ => throw new System.ArgumentException($"Unknown MI prefix: '{prefix}' in '{combinedId}'"),
        };
    }
}
