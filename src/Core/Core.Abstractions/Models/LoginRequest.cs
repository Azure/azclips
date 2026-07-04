// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public class LoginRequest
{
    public string? TenantId { get; init; }
    public string? SubscriptionId { get; init; }

    public string[]? Scopes { get; init; }

    /// <summary>Authentication method; defaults to interactive browser sign-in.</summary>
    public AuthMethod AuthMode { get; init; } = AuthMethod.Browser;

    /// <summary>Compatibility flag for device code login.</summary>
    public bool UseDeviceCodeFlow => AuthMode == AuthMethod.DeviceCode;

    // Managed identity

    /// <summary>User-assigned managed identity client ID.</summary>
    public string? ManagedIdentityClientId { get; init; }

    /// <summary>User-assigned managed identity object ID.</summary>
    public string? ManagedIdentityObjectId { get; init; }

    /// <summary>User-assigned managed identity resource ID.</summary>
    public string? ManagedIdentityResourceId { get; init; }

}
