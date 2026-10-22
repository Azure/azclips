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
public static class DataBoundaryResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Returns <c>true</c> only when the active profile's tenant has a CONFIRMED "Global"
    /// boundary (case-insensitive) in the cache. Returns <c>false</c> for EU, for a missing/
    /// unresolved value, and for file-access errors -- callers that gate whether data may
    /// leave the EU must treat "we don't know" the same as EU (fail-closed), not the same as
    /// a confirmed non-EU tenant.
    /// </summary>
    internal static bool IsConfirmedGlobalDataBoundary()
    {
        try
        {
            var tenantId = GetActiveTenantId();
            if (tenantId is null)
                return false;

            var boundary = DataBoundaryCache.Get(tenantId);
            return DataBoundaryRouting.IsConfirmedGlobal(boundary);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the boundary for a known tenant, using the file cache first and calling
    /// the supplied token provider only on cache miss.
    /// </summary>
    public static async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await ResolveAndCacheAsync(
            tenantId,
            accessTokenProvider,
            DataBoundaryService.GetDataBoundaryAsync,
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        Func<string, CancellationToken, Task<string?>> dataBoundaryProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        var cached = DataBoundaryCache.Get(tenantId);
        if (!string.IsNullOrWhiteSpace(cached))
            return DataBoundaryRouting.Normalize(cached);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var token = await accessTokenProvider(timeoutCts.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token) || !TokenMatchesTenant(token, tenantId))
                return null;

            var boundary = await dataBoundaryProvider(token, timeoutCts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(boundary))
                return null;

            var normalized = DataBoundaryRouting.Normalize(boundary);
            DataBoundaryCache.Set(tenantId, normalized);
            return normalized;
        }
        catch
        {
            return null;
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

    private static bool TokenMatchesTenant(string token, string tenantId)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return false;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("tid", out var tidElement) &&
                   string.Equals(tidElement.GetString(), tenantId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Minimal DTO for profile deserialization.</summary>
    private sealed class ProfileEntry
    {
        public string? TenantId { get; set; }
    }
}
