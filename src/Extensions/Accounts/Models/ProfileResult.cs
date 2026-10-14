using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Models;

/// <summary>Profile projection returned by account/profile commands; omits runtime-only profile metadata.</summary>
public sealed record ProfileResult
{
    public string Name { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string SubscriptionId { get; init; } = default!;
    public string CloudName { get; init; } = default!;
    public AuthMethod AuthMethod { get; init; }
    public bool IsActive { get; init; }
    public string? DefaultLocation { get; init; }
    public string? DefaultResourceGroup { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? ClientId { get; init; }
    public string? ManagedIdentityId { get; init; }

    public static ProfileResult From(Profile profile, bool isActive = false) => new()
    {
        Name = profile.Name,
        TenantId = profile.TenantId,
        SubscriptionId = profile.SubscriptionId,
        CloudName = profile.CloudName,
        AuthMethod = profile.AuthMethod,
        IsActive = isActive,
        DefaultLocation = profile.DefaultLocation,
        DefaultResourceGroup = profile.DefaultResourceGroup,
        UserPrincipalName = profile.UserPrincipalName,
        ClientId = profile.ClientId,
        ManagedIdentityId = profile.ManagedIdentityId,
    };
}
