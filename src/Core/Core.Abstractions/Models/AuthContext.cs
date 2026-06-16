// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Contextual information about the authentication state.
/// </summary>
public record AuthContext(string TenantId, string SubscriptionId, LoginMode Mode = LoginMode.Interactive)
{
    /// <summary>
    /// Combined managed identity identifier, stored as "{MSIType}@{value}".
    /// Uses Azure CLI naming convention: "MSI@" (system), "MSIClient@{guid}",
    /// "MSIObject@{guid}", "MSIResource@{armPath}".
    /// Null when mode is not ManagedIdentity.
    /// </summary>
    public string? ManagedIdentityId { get; init; }

    // ── MI combined key helpers ──

    /// <summary>
    /// Formats a managed identity type and value into the combined key: "{MSIType}@{value}".
    /// </summary>
    public static string FormatManagedIdentityId(ManagedIdentityIdType idType, string? idValue) => idType switch
    {
        ManagedIdentityIdType.SystemAssigned => "MSI@",
        ManagedIdentityIdType.ClientId => $"MSIClient@{idValue}",
        ManagedIdentityIdType.ObjectId => $"MSIObject@{idValue}",
        ManagedIdentityIdType.ResourceId => $"MSIResource@{idValue}",
        _ => throw new System.ArgumentException($"Unsupported ManagedIdentityIdType: {idType}"),
    };

    /// <summary>
    /// Formats a combined MI key from a LoginRequest's typed fields.
    /// </summary>
    public static string FormatManagedIdentityId(LoginRequest request)
    {
        if (!string.IsNullOrEmpty(request.ManagedIdentityClientId))
            return FormatManagedIdentityId(ManagedIdentityIdType.ClientId, request.ManagedIdentityClientId);
        if (!string.IsNullOrEmpty(request.ManagedIdentityObjectId))
            return FormatManagedIdentityId(ManagedIdentityIdType.ObjectId, request.ManagedIdentityObjectId);
        if (!string.IsNullOrEmpty(request.ManagedIdentityResourceId))
            return FormatManagedIdentityId(ManagedIdentityIdType.ResourceId, request.ManagedIdentityResourceId);
        return FormatManagedIdentityId(ManagedIdentityIdType.SystemAssigned, null);
    }

    /// <summary>
    /// Parses a combined managed identity key (e.g. "MSIClient@guid") back into type and value.
    /// </summary>
    public static (ManagedIdentityIdType IdType, string? IdValue) ParseManagedIdentityId(string? combinedId)
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

    public override string ToString() =>
        $"Subscription: {SubscriptionId}{System.Environment.NewLine}Tenant: {TenantId}{System.Environment.NewLine}Mode: {Mode}";
}