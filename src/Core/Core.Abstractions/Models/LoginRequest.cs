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

    // Service principal (federated / OIDC client assertion)

    /// <summary>Application (client) ID of the service principal to sign in as.</summary>
    public string? ServicePrincipalClientId { get; init; }

    /// <summary>
    /// OIDC JWT from a trusted external issuer, exchanged for an Entra token via the client assertion
    /// flow. Kept in the secret store so a refresh need not ask again; never written to the profile.
    /// </summary>
    public string? FederatedToken { get; init; }
}
