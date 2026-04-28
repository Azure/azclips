using Core.Abstractions;

namespace Core.Authentication;

/// <summary>
/// Delegates to IProfileService. AuthContext is derived from the active profile.
/// context.json is no longer read or written — profiles.json is the sole source of truth.
/// </summary>
public class AuthContextService(ITelemetryService TelemetryService, IProfileService ProfileService) : IAuthContextService
{
    public async Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveContextAsync)}");

        var activeProfile = await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            activeProfile.TenantId = authContext.TenantId;
            activeProfile.SubscriptionId = authContext.SubscriptionId;
            await ProfileService.SaveProfileAsync(activeProfile, cancellationToken);
        }
    }

    public async Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetCurrentContextAsync)}");

        var activeProfile = await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            return new AuthContext(TenantId: activeProfile.TenantId, SubscriptionId: activeProfile.SubscriptionId);
        }

        return null;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        // No-op: profile definitions persist. Tokens are cleared by LogoutCommand directly.
        return Task.CompletedTask;
    }
}
