// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Looks up the active profile's tenant data boundary from a dedicated cache file.
/// This lets telemetry exporters choose the correct Aria endpoint (EU vs Global)
/// without requiring DI.
/// </summary>
internal static class DataBoundaryResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Returns <c>true</c> when the active profile's tenant data boundary is "EU" (case-insensitive).
    /// Returns <c>false</c> for all other values, missing data, or file-access errors.
    /// </summary>
    internal static bool IsEuDataBoundary()
    {
        try
        {
            var tenantId = GetActiveTenantId();
            if (tenantId is null)
                return false;

            var boundary = DataBoundaryCache.Get(tenantId);
            return string.Equals(boundary, "EU", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the tenant ID from the active profile, or <c>null</c> if unavailable.
    /// </summary>
    internal static string? GetActiveTenantId()
    {
        try
        {
            var root = HomePaths.GetDefaultLocalAppDataRoot();

            var activeProfilePath = Path.Combine(root, HomePaths.ActiveProfileFileName);
            if (!File.Exists(activeProfilePath))
                return null;

            var activeProfileName = File.ReadAllText(activeProfilePath).Trim();
            if (string.IsNullOrEmpty(activeProfileName))
                return null;

            var profilesPath = Path.Combine(root, HomePaths.ProfilesFileName);
            if (!File.Exists(profilesPath))
                return null;

            var json = File.ReadAllText(profilesPath);
            var profiles = JsonSerializer.Deserialize<Dictionary<string, ProfileEntry>>(json, JsonOptions);

            if (profiles is not null && profiles.TryGetValue(activeProfileName, out var profile))
                return profile.TenantId;
        }
        catch
        {
            // Best effort.
        }

        return null;
    }

    /// <summary>Minimal DTO for profile deserialization.</summary>
    private sealed class ProfileEntry
    {
        public string? TenantId { get; set; }
    }
}
