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

    /// <summary>The Azure subscription ID.</summary>
    public string SubscriptionId { get; init; } = default!;

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

    public override string ToString() =>
        $"Name: {Name}{Environment.NewLine}" +
        $"Subscription: {SubscriptionId}{Environment.NewLine}" +
        $"Tenant: {TenantId}{Environment.NewLine}" +
        $"Cloud: {CloudName}{Environment.NewLine}" +
        $"AuthMethod: {AuthMethod}" +
        (string.IsNullOrEmpty(UserPrincipalName) ? string.Empty : $"{Environment.NewLine}User: {UserPrincipalName}");
}
