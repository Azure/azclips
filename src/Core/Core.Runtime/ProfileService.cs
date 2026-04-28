using System.Runtime.InteropServices;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Runtime;

public class ProfileService : IProfileService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly string ProfileDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ProfileFilePath = Path.Combine(ProfileDirectory, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILoggingService _logger;
    private ProfileStore? _cachedStore;

    public ProfileService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.Profiles.Values.ToList().AsReadOnly();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.Profiles.TryGetValue(name, out var profile) ? profile : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (store.ActiveProfile is not null && store.Profiles.TryGetValue(store.ActiveProfile, out var profile))
                return profile;
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (!store.Profiles.ContainsKey(name))
            {
                var available = string.Join(", ", store.Profiles.Keys);
                throw new InvalidOperationException(
                    $"Profile '{name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}. " +
                    $"Run 'login --out-profile {name}' to create it.");
            }
            store.ActiveProfile = name;
            await PersistStoreAsync(store, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveProfileAsync(Profile profile, bool setActive = true, CancellationToken cancellationToken = default)
    {
        if (!Profile.IsValidName(profile.Name))
            throw new ArgumentException(
                $"Invalid profile name '{profile.Name}'. Profile names must match [a-zA-Z0-9_-]+ (alphanumeric, hyphens, underscores only).");

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.Profiles[profile.Name] = profile;
            if (setActive)
                store.ActiveProfile = profile.Name;
            await PersistStoreAsync(store, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateProfileAsync(string name, Action<Profile> updates, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (!store.Profiles.TryGetValue(name, out var profile))
            {
                var available = string.Join(", ", store.Profiles.Keys);
                throw new InvalidOperationException(
                    $"Profile '{name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
            }
            updates(profile);
            await PersistStoreAsync(store, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (!store.Profiles.Remove(name))
            {
                var available = string.Join(", ", store.Profiles.Keys);
                throw new InvalidOperationException(
                    $"Profile '{name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
            }
            if (string.Equals(store.ActiveProfile, name, StringComparison.OrdinalIgnoreCase))
                store.ActiveProfile = null;
            await PersistStoreAsync(store, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<ProfileStore> LoadStoreAsync(CancellationToken cancellationToken)
    {
        if (_cachedStore is not null)
            return _cachedStore;

        if (!File.Exists(ProfileFilePath))
        {
            _logger.Verbose($"[{nameof(ProfileService)}] No profiles file found. Creating empty store.");
            _cachedStore = new ProfileStore();
            return _cachedStore;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ProfileFilePath, cancellationToken);
            var store = JsonSerializer.Deserialize<ProfileStore>(json, JsonOptions);
            if (store is null)
                throw new InvalidOperationException("Deserialization returned null.");

            if (store.SchemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Profile store schema version {store.SchemaVersion} is newer than supported version {CurrentSchemaVersion}. " +
                    $"Please upgrade Cirrus or delete '{ProfileFilePath}' and re-create your profiles.");

            _cachedStore = store;
            return store;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Profile store '{ProfileFilePath}' is corrupted or contains invalid JSON. " +
                $"Delete the file and re-create your profiles with 'login --out-profile <name>'. Details: {ex.Message}", ex);
        }
    }

    private async Task PersistStoreAsync(ProfileStore store, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ProfileDirectory);
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var tempPath = ProfileFilePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(tempPath, ProfileFilePath, overwrite: true);
        _cachedStore = store;
        _logger.Verbose($"[{nameof(ProfileService)}] Profiles persisted to: {ProfileFilePath}");
    }
}
