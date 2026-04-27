using Core.Abstractions;
using System.Text.Json;

namespace Core.Authentication;

/// <summary>
/// Adapter: delegates to IProfileService for profile-based auth context, while maintaining
/// backward compatibility by also reading/writing context.json during migration.
/// </summary>
public class AuthContextService(ILoggingService Logger, ITelemetryService TelemetryService, IProfileService ProfileService) : IAuthContextService
{
    private static readonly string ContextDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(ContextDirectory, "context.json");

    public async Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveContextAsync)}");

        // Update the active profile if one exists
        var activeProfile = await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            activeProfile.TenantId = authContext.TenantId;
            activeProfile.SubscriptionId = authContext.SubscriptionId;
            await ProfileService.SaveProfileAsync(activeProfile, cancellationToken);
        }

        // Also write context.json for backward compatibility during migration
        try
        {
            Directory.CreateDirectory(ContextDirectory);
            var json = JsonSerializer.Serialize(authContext, new JsonSerializerOptions { WriteIndented = true });
            Logger.Verbose($"[{nameof(AuthContextService)}] Saving authentication context to: {ContextFilePath}");
            await File.WriteAllTextAsync(ContextFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save authentication context.", ex);
        }
    }

    public async Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetCurrentContextAsync)}");

        // Delegate to profile service — active profile is the source of truth
        var activeProfile = await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            return new AuthContext(TenantId: activeProfile.TenantId, SubscriptionId: activeProfile.SubscriptionId);
        }

        // Fallback: read legacy context.json if no profiles exist yet
        return await LoadLegacyContextAsync(cancellationToken);
    }

    private async Task<AuthContext?> LoadLegacyContextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(ContextFilePath))
            {
                Logger.Verbose($"[{nameof(AuthContextService)}] Loading persisted authentication context from: {ContextFilePath}");
                var json = await File.ReadAllTextAsync(ContextFilePath, cancellationToken);
                return JsonSerializer.Deserialize<AuthContext>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else
            {
                Logger.Verbose($"[{nameof(AuthContextService)}] No persisted authentication context found.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load authentication context from file.", ex);
        }
        return null;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(ClearContextAsync)}");
        // Profile definitions are preserved — only clear legacy context.json
        if (File.Exists(ContextFilePath))
        {
            Logger.Verbose($"[{nameof(AuthContextService)}] Deleting persisted authentication context: {ContextFilePath}");
            File.Delete(ContextFilePath);
        }
        return Task.CompletedTask;
    }
}
