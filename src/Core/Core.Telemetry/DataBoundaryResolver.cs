// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Telemetry;

/// <summary>
/// Reads the active profile's <c>DataBoundary</c> directly from the on-disk profile store
/// without requiring the full DI container. This lets telemetry exporters choose the correct
/// Aria endpoint (EU vs Global) at initialization time and in the background uploader process.
/// </summary>
internal static class DataBoundaryResolver
{
    private static readonly string AzclipsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ".azclips");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Returns <c>true</c> when the active profile's data boundary is "EU" (case-insensitive).
    /// Returns <c>false</c> for all other values, missing data, or file-access errors.
    /// </summary>
    internal static bool IsEuDataBoundary()
    {
        try
        {
            var activeProfilePath = Path.Combine(AzclipsDirectory, "active-profile");
            if (!File.Exists(activeProfilePath))
                return false;

            var activeProfileName = File.ReadAllText(activeProfilePath).Trim();
            if (string.IsNullOrEmpty(activeProfileName))
                return false;

            var profilesPath = Path.Combine(AzclipsDirectory, "profiles.json");
            if (!File.Exists(profilesPath))
                return false;

            var json = File.ReadAllText(profilesPath);
            var profiles = JsonSerializer.Deserialize<Dictionary<string, ProfileEntry>>(json, JsonOptions);

            if (profiles is not null && profiles.TryGetValue(activeProfileName, out var profile))
            {
                return string.Equals(profile.DataBoundary, "EU", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Best effort — any failure falls back to Global.
        }

        return false;
    }

    /// <summary>Minimal DTO that only deserializes the DataBoundary field from the profile JSON.</summary>
    private sealed class ProfileEntry
    {
        public string? DataBoundary { get; set; }
    }
}
