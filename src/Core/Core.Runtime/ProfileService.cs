using Core.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Runtime;

/// <summary>
/// Manages named connection profiles persisted to {LocalApplicationData}/.cirrus/profiles.json.
/// Thread-safe: uses SemaphoreSlim for in-process concurrency, atomic file writes for cross-process safety.
/// </summary>
public class ProfileService(ILoggingService logger, ITelemetryService telemetryService) : IProfileService
{
    private static readonly string ProfileDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ProfileFilePath = Path.Combine(ProfileDirectory, "profiles.json");
    private static readonly string ContextFilePath = Path.Combine(ProfileDirectory, "context.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ProfileStore? _store;

    public async Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(GetAllProfilesAsync)}");
        var store = await LoadStoreAsync(cancellationToken);
        return store.Profiles.Values.ToList().AsReadOnly();
    }

    public async Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(GetProfileAsync)}");
        var store = await LoadStoreAsync(cancellationToken);
        return store.Profiles.FirstOrDefault(
            kvp => string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    public async Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(GetActiveProfileAsync)}");
        var store = await LoadStoreAsync(cancellationToken);
        if (string.IsNullOrEmpty(store.ActiveProfile))
            return null;
        return store.Profiles.FirstOrDefault(
            kvp => string.Equals(kvp.Key, store.ActiveProfile, StringComparison.OrdinalIgnoreCase)).Value;
    }

    public async Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(SetActiveProfileAsync)}");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreInternalAsync(cancellationToken);
            var key = store.Profiles.Keys.FirstOrDefault(
                k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key is null)
            {
                var available = string.Join(", ", store.Profiles.Keys);
                throw new InvalidOperationException(
                    $"Profile '{name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}");
            }
            store.ActiveProfile = key;
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(SaveProfileAsync)}");
        Profile.ValidateName(profile.Name);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreInternalAsync(cancellationToken);
            // Upsert: find existing key case-insensitively, replace or add
            var existingKey = store.Profiles.Keys.FirstOrDefault(
                k => string.Equals(k, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (existingKey is not null)
                store.Profiles.Remove(existingKey);
            store.Profiles[profile.Name] = profile;
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ProfileService)}.{nameof(RemoveProfileAsync)}");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreInternalAsync(cancellationToken);
            var key = store.Profiles.Keys.FirstOrDefault(
                k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key is null)
                return false;

            store.Profiles.Remove(key);
            if (string.Equals(store.ActiveProfile, key, StringComparison.OrdinalIgnoreCase))
                store.ActiveProfile = null;
            await SaveStoreAsync(store, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Loads the store, acquiring the lock externally (for public read-only methods).
    /// </summary>
    private async Task<ProfileStore> LoadStoreAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await LoadStoreInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Loads the store from disk. Caller MUST hold _lock.
    /// Handles migration from context.json if profiles.json does not exist.
    /// </summary>
    private async Task<ProfileStore> LoadStoreInternalAsync(CancellationToken cancellationToken)
    {
        if (_store is not null)
            return _store;

        if (File.Exists(ProfileFilePath))
        {
            logger.Verbose($"[{nameof(ProfileService)}] Loading profiles from: {ProfileFilePath}");
            var json = await File.ReadAllTextAsync(ProfileFilePath, cancellationToken);
            _store = JsonSerializer.Deserialize<ProfileStore>(json, JsonOptions) ?? new ProfileStore();
        }
        else if (File.Exists(ContextFilePath))
        {
            // Migration: read legacy context.json and create a default profile
            logger.Verbose($"[{nameof(ProfileService)}] Migrating from context.json to profiles.json");
            var contextJson = await File.ReadAllTextAsync(ContextFilePath, cancellationToken);
            var authContext = JsonSerializer.Deserialize<AuthContext>(contextJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (authContext is not null)
            {
                var defaultProfile = new Profile
                {
                    Name = "default",
                    TenantId = authContext.TenantId,
                    SubscriptionId = authContext.SubscriptionId
                };
                _store = new ProfileStore
                {
                    ActiveProfile = "default",
                    Profiles = new Dictionary<string, Profile> { ["default"] = defaultProfile }
                };
                await SaveStoreAsync(_store, cancellationToken);
            }
            else
            {
                _store = new ProfileStore();
            }
        }
        else
        {
            logger.Verbose($"[{nameof(ProfileService)}] No profiles found. Starting fresh.");
            _store = new ProfileStore();
        }

        return _store;
    }

    /// <summary>
    /// Atomically writes the store to disk (write-to-temp + rename). Caller MUST hold _lock.
    /// </summary>
    private async Task SaveStoreAsync(ProfileStore store, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ProfileDirectory);
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var tempFile = Path.Combine(ProfileDirectory, $"profiles.json.tmp.{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);
            File.Move(tempFile, ProfileFilePath, overwrite: true);
            _store = store;
            logger.Verbose($"[{nameof(ProfileService)}] Saved profiles to: {ProfileFilePath}");
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }
    }

    /// <summary>
    /// Internal representation of the profiles.json file.
    /// </summary>
    internal class ProfileStore
    {
        [JsonPropertyName("activeProfile")]
        public string? ActiveProfile { get; set; }

        [JsonPropertyName("profiles")]
        public Dictionary<string, Profile> Profiles { get; set; } = new();
    }
}
