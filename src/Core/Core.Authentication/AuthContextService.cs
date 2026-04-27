using Core.Abstractions;
using System.Text.Json;

namespace Core.Authentication;

public class AuthContextService(ILoggingService Logger, ITelemetryService TelemetryService) : IAuthContextService // todo: testability, consider injecting file system abstractions
{
    private static readonly string ContextDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(ContextDirectory, "context.json");
    private static readonly string ProfilesDirectory = Path.Combine(ContextDirectory, "profiles");
    private static readonly string ActiveProfilePath = Path.Combine(ContextDirectory, "active-profile");
    private const string DefaultProfileName = "default";
    private AuthContext? _currentContext;

    public async Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveContextAsync)}");
        try
        {
            Directory.CreateDirectory(ContextDirectory);
            var json = JsonSerializer.Serialize(authContext, new JsonSerializerOptions { WriteIndented = true });
            Logger.Verbose($"[{nameof(AuthContextService)}] Saving authentication context to: {ContextFilePath}");
            await WriteTextAtomicAsync(ContextFilePath, json, cancellationToken);
            await SaveProfileAsync(DefaultProfileName, authContext, cancellationToken: cancellationToken);
            await SetActiveProfileAsync(DefaultProfileName, cancellationToken);
            _currentContext = authContext;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save authentication context.", ex);
        }
    }

    public async Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetCurrentContextAsync)}");
        var activeProfile = await GetActiveProfileAsync(cancellationToken);
        if (activeProfile is not null)
        {
            _currentContext = activeProfile.ToAuthContext();
            return _currentContext;
        }

        _currentContext = await LoadContextAsync(cancellationToken);  // todo: proper cache management (synchronization, multi-account, etc.) instead of hard-reloading
        return _currentContext;
    }

    private async Task<AuthContext?> LoadContextAsync(CancellationToken cancellationToken = default)
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
        _currentContext = null;
        if (File.Exists(ContextFilePath))
        {
            Logger.Verbose($"[{nameof(AuthContextService)}] Deleting persisted authentication context: {ContextFilePath}");
            File.Delete(ContextFilePath);
        }

        if (File.Exists(ActiveProfilePath))
        {
            Logger.Verbose($"[{nameof(AuthContextService)}] Clearing active profile pointer: {ActiveProfilePath}");
            File.Delete(ActiveProfilePath);
        }

        return Task.CompletedTask;
    }

    public async Task SaveProfileAsync(
        string profileName,
        AuthContext authContext,
        string? accountId = null,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveProfileAsync)}");

        var normalized = NormalizeProfileName(profileName);
        var existing = await GetProfileAsync(profileName, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var profile = new AuthProfile(
            ProfileName: profileName,
            TenantId: authContext.TenantId,
            SubscriptionId: authContext.SubscriptionId,
            AccountId: accountId ?? existing?.AccountId ?? "unknown",
            AccountType: existing?.AccountType ?? "User",
            Environment: existing?.Environment ?? "AzureCloud",
            CreatedAt: existing?.CreatedAt ?? now,
            LastUsedAt: now,
            DisplayName: displayName ?? existing?.DisplayName,
            Version: 1);

        var path = GetProfilePath(normalized);
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        await WriteTextAtomicAsync(path, json, cancellationToken);
    }

    public async Task<AuthProfile?> GetProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetProfileAsync)}");

        var normalized = NormalizeProfileName(profileName);
        var path = GetProfilePath(normalized);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<AuthProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"[{nameof(AuthContextService)}] Failed to read profile '{profileName}': {ex.Message}");
            return null;
        }
    }

    public Task<IReadOnlyList<AuthProfile>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(ListProfilesAsync)}");

        var profiles = new List<AuthProfile>();
        if (!Directory.Exists(ProfilesDirectory))
        {
            return Task.FromResult<IReadOnlyList<AuthProfile>>(profiles);
        }

        foreach (var file in Directory.EnumerateFiles(ProfilesDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AuthProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (profile is not null)
                {
                    profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[{nameof(AuthContextService)}] Skipping invalid profile file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        return Task.FromResult<IReadOnlyList<AuthProfile>>(profiles);
    }

    public async Task SetActiveProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(SetActiveProfileAsync)}");

        var profile = await GetProfileAsync(profileName, cancellationToken);
        if (profile is null)
        {
            throw new InvalidOperationException($"Profile '{profileName}' not found.");
        }

        var normalized = NormalizeProfileName(profileName);
        await WriteTextAtomicAsync(ActiveProfilePath, normalized, cancellationToken);
    }

    public async Task<AuthProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(AuthContextService)}.{nameof(GetActiveProfileAsync)}");

        if (!File.Exists(ActiveProfilePath))
        {
            return null;
        }

        try
        {
            var profileKey = (await File.ReadAllTextAsync(ActiveProfilePath, cancellationToken)).Trim();
            if (string.IsNullOrWhiteSpace(profileKey))
            {
                return null;
            }

            return await GetProfileAsync(profileKey, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[{nameof(AuthContextService)}] Active profile pointer is invalid: {ex.Message}");
            return null;
        }
    }

    private static string NormalizeProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        var trimmed = profileName.Trim();
        if (trimmed is "." or "..")
        {
            throw new ArgumentException("Profile name is invalid.", nameof(profileName));
        }

        foreach (var ch in trimmed)
        {
            var isLetterOrDigit = char.IsLetterOrDigit(ch);
            var isAllowedPunctuation = ch is '-' or '_';
            if (!isLetterOrDigit && !isAllowedPunctuation)
            {
                throw new ArgumentException("Profile name supports only letters, digits, '-' and '_'.", nameof(profileName));
            }
        }

        return trimmed.ToLowerInvariant();
    }

    private static string GetProfilePath(string normalizedProfileName) =>
        Path.Combine(ProfilesDirectory, $"{normalizedProfileName}.json");

    private static async Task WriteTextAtomicAsync(string path, string contents, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, contents, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}
