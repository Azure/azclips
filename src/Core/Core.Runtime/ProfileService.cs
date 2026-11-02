using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Runtime;

/// <summary>
/// File-backed implementation of <see cref="IProfileService"/>. Profiles are stored as a JSON
/// object keyed by profile name in <c>~/.azclips/profiles.json</c>; the active
/// profile name is stored separately in <c>~/.azclips/active-profile</c>.
/// All writes are atomic (temp file plus rename) to prevent corruption from concurrent access.
/// </summary>
public class ProfileService(ILoggingService Logger, IPersistenceStore Persistence) : IProfileService
{
    private string _profilesFilePath => Persistence.GetFilePath(HomePaths.ProfilesFileName);
    private string _activeProfileFilePath => Persistence.GetFilePath(HomePaths.ActiveProfileFileName);

    // default profile keys for other services to use as default, for now SubscriptionId only
    public List<string> GetProfileKeysForDefault() =>
    [
        "SubscriptionId"
    ];

    public string? GetProfileValueByKey(Profile? profile, string key)
    {
        switch (key)
        {
            case "Name":
                return profile?.Name;
            case "SubscriptionId":
                return profile?.SubscriptionId;
            case "TenantId":
                return profile?.TenantId;
            case "ClientId":
                return profile?.ClientId;
            case "CloudName":
                return profile?.CloudName;
            case "AuthMethod":
                return profile?.AuthMethod.ToString();
            case "DefaultLocation":
                return profile?.DefaultLocation;
            case "UserPrincipalName":
                return profile?.UserPrincipalName;
            case "ManagedIdentityId":
                return profile?.ManagedIdentityId;
            default:
                return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Serializes read-modify-write sequences within the process to avoid lost updates.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
            return profiles.Values.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
            return profiles.TryGetValue(name, out var profile) ? profile : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfile(profile);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
            profiles[profile.Name] = profile;
            await SaveProfilesAsync(profiles, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfile(profile);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
            profiles[profile.Name] = profile;
            await SaveProfilesAsync(profiles, cancellationToken);
            await WriteActiveProfileNameAsync(profile.Name, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await LoadProfilesAsync(cancellationToken);
            if (!profiles.Remove(name))
            {
                return false;
            }

            await SaveProfilesAsync(profiles, cancellationToken);

            var active = await ReadActiveProfileNameAsync(cancellationToken);
            if (string.Equals(active, name, StringComparison.OrdinalIgnoreCase))
            {
                await WriteActiveProfileNameAsync(null, cancellationToken);
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadActiveProfileNameAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var name = await ReadActiveProfileNameAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var profiles = await LoadProfilesAsync(cancellationToken);
            return profiles.TryGetValue(name, out var profile) ? profile : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteActiveProfileNameAsync(name, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, Profile>> LoadProfilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var content = await Persistence.ReadAsync(HomePaths.ProfilesFileName, cancellationToken);
            if (content is null)
            {
                return new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            }

            Logger.Verbose($"[{nameof(ProfileService)}] Loading profiles from: {_profilesFilePath}");
            var json = DecodeText(content);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            }

            var loaded = JsonSerializer.Deserialize<Dictionary<string, Profile>>(json, JsonOptions);
            return loaded is null
                ? new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Profile>(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new Exception($"Failed to load profiles from file '{_profilesFilePath}': {ex.Message}", ex);
        }
    }

    private async Task SaveProfilesAsync(Dictionary<string, Profile> profiles, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        Logger.Verbose($"[{nameof(ProfileService)}] Saving {profiles.Count} profile(s) to: {_profilesFilePath}");
        await Persistence.WriteAsync(HomePaths.ProfilesFileName, Encoding.UTF8.GetBytes(json), cancellationToken);
    }

    private async Task<string?> ReadActiveProfileNameAsync(CancellationToken cancellationToken)
    {
        var content = await Persistence.ReadAsync(HomePaths.ActiveProfileFileName, cancellationToken);
        if (content is null)
        {
            return null;
        }

        var text = DecodeText(content).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private async Task WriteActiveProfileNameAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Logger.Verbose($"[{nameof(ProfileService)}] Clearing active profile pointer: {_activeProfileFilePath}");
            await Persistence.DeleteAsync(HomePaths.ActiveProfileFileName, cancellationToken);
            return;
        }

        Logger.Verbose($"[{nameof(ProfileService)}] Setting active profile to '{name}': {_activeProfileFilePath}");
        await Persistence.WriteAsync(HomePaths.ActiveProfileFileName, Encoding.UTF8.GetBytes(name), cancellationToken);
    }

    private static string DecodeText(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void ValidateProfile(Profile profile)
    {
        ProfileNameValidator.Validate(profile.Name);
        SubscriptionIdValidator.Validate(profile.SubscriptionId, nameof(Profile.SubscriptionId), allowEmpty: true);
    }
}
