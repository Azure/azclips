// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>Encodes and decodes the persisted managed identity key.</summary>
public static class ManagedIdentityIdCodec // todo: move this inside Accounts extension?
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
        var credentials = request.Credential as AuthCredential.ManagedIdentity;
        Debug.Assert(credentials != null, "Expected the credential to be a ManagedIdentity.");
        return Format(credentials.Type, credentials.ID);
    }

    /// <summary>Parses a managed identity key.</summary>
    public static (ManagedIdentityIdType IdType, string? IdValue) Parse(string? combinedId) // consider deprecating this
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

    public static AuthCredential.ManagedIdentity ParseToCredentials(string? combinedId)
    {
        var (idType, idValue) = Parse(combinedId);
        return new AuthCredential.ManagedIdentity(idType, idValue);
    }
}
