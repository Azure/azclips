using System.Text.Json.Serialization;

namespace Core.Abstractions;

/// <summary>
/// A named connection context describing how Azclips talks to a cloud service.
/// Profiles are the single source of truth for connection state, superseding the
/// former single <c>AuthContext</c>. All fields use language <c>default</c> values
/// when not explicitly set. <see cref="CloudName"/> is a stored property with no
/// behavioral impact in this change.
/// </summary>
public record Profile
{
    /// <summary>The unique profile name.</summary>
    public string Name { get; init; } = default!;

    /// <summary>The Azure tenant (directory) ID.</summary>
    public string TenantId { get; init; } = default!;

    /// <summary>Optional default Azure subscription ID for subscription-scoped commands.</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>The cloud environment name (e.g. "AzureCloud"). Stored only; no behavioral impact.</summary>
    public string CloudName { get; init; } = default!;

    /// <summary>The authentication method used to establish this profile.</summary>
    public AuthMethod AuthMethod { get; init; }

    /// <summary>Optional default location for commands that accept one.</summary>
    public string? DefaultLocation { get; init; }

    /// <summary>Optional default resource group for commands that accept one.</summary>
    public string? DefaultResourceGroup { get; init; }

    /// <summary>The authenticated account's user principal name, if known.</summary>
    public string? UserPrincipalName { get; init; }

    /// <summary>Optional client ID associated with the profile.</summary>
    public string? ClientId { get; init; }

    /// <summary>Persisted managed identity key used for token refresh.</summary>
    public string? ManagedIdentityId { get; init; }

    /// <summary>Runtime-only authentication metadata resolved from the current token cache.</summary>
    [JsonIgnore]
    public ProfileRuntimeMetadata Metadata { get; init; } = new();

    public Profile WithRuntimeMetadata(AuthResult authResult)
    {
        if (string.IsNullOrWhiteSpace(authResult.EntraObjectId))
        {
            return this;
        }

        return this with
        {
            Metadata = Metadata with { EntraObjectId = authResult.EntraObjectId }
        };
    }

    public override string ToString() =>
        $"Name: {Name}{Environment.NewLine}" +
        $"Subscription: {SubscriptionId}{Environment.NewLine}" +
        $"Tenant: {TenantId}{Environment.NewLine}" +
        $"Cloud: {CloudName}{Environment.NewLine}" +
        $"AuthMethod: {AuthMethod}" +
        (string.IsNullOrEmpty(UserPrincipalName) ? string.Empty : $"{Environment.NewLine}User: {UserPrincipalName}");
}

/// <summary>
/// Authentication values resolved at command runtime. These values must never be persisted to profiles.json.
/// </summary>
public record ProfileRuntimeMetadata
{
    /// <summary>The authenticated identity's Entra object ID, when available from MSAL token metadata.</summary>
    public string? EntraObjectId { get; init; }
}
