using Core.Abstractions;
using System.Text.Json;

namespace Core.Authentication;

public class AuthContextService(ILoggingService Logger, ITelemetryService TelemetryService, IProfileService ProfileService) : IAuthContextService
{
    public async Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveContextAsync)}");
        var profile = new Profile
        {
            Name = "temp",
            TenantId = authContext.TenantId,
            SubscriptionId = authContext.SubscriptionId,
            AuthMethod = "browser",
            CloudName = "AzureCloud"
        };
        await ProfileService.SaveProfileAsync(profile, setActive: true, cancellationToken);
        Logger.Verbose($"[{nameof(AuthContextService)}] Saved authentication context as 'temp' profile.");
    }

    public async Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetCurrentContextAsync)}");
        var activeProfile = await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            Logger.Verbose($"[{nameof(AuthContextService)}] Derived AuthContext from active profile '{activeProfile.Name}'.");
            return new AuthContext(TenantId: activeProfile.TenantId, SubscriptionId: activeProfile.SubscriptionId);
        }
        Logger.Verbose($"[{nameof(AuthContextService)}] No active profile found.");
        return null;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(ClearContextAsync)}");
        Logger.Verbose($"[{nameof(AuthContextService)}] ClearContextAsync called — token clearing handled by auth service, profile definition preserved.");
        return Task.CompletedTask;
    }
}
