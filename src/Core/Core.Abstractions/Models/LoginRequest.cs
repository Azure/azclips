// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public class LoginRequest
{
    public string? TenantId { get; init; }
    public string? SubscriptionId { get; init; }

    public string[]? Scopes { get; init; }

    /// <summary>
    /// The authentication mode to use. Defaults to <see cref="LoginMode.Interactive"/>.
    /// Replaces the older <c>UseDeviceCodeFlow</c> boolean.
    /// </summary>
    public LoginMode Mode { get; init; } = LoginMode.Interactive;

    /// <summary>Kept for backward compatibility; maps to <see cref="LoginMode.DeviceCode"/>.</summary>
    public bool UseDeviceCodeFlow => Mode == LoginMode.DeviceCode;

    // ── Managed Identity ──

    /// <summary>Client ID for a user-assigned managed identity.</summary>
    public string? ManagedIdentityClientId { get; init; }

    /// <summary>Object (principal) ID for a user-assigned managed identity.</summary>
    public string? ManagedIdentityObjectId { get; init; }

    /// <summary>ARM resource ID for a user-assigned managed identity.</summary>
    public string? ManagedIdentityResourceId { get; init; }

}
