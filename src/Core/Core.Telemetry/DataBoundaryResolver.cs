// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Looks up the active profile's tenant data boundary from a dedicated cache file.
/// This lets telemetry exporters choose the correct Aria endpoint (EU vs ROW)
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
    /// Returns true only when the active profile's tenant has a confirmed EU boundary
    /// in the cache. Missing profiles, unresolved boundaries, and file-access errors use ROW.
    /// </summary>
    internal static bool IsEuDataBoundary()
    {
        try
        {
            var tenantId = GetActiveTenantId();
            if (tenantId is null)
                return false;

            var boundary = DataBoundaryCache.Get(tenantId);
            return DataBoundaryRouting.IsEu(boundary);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the telemetry destination after silently authenticating the known tenant.
    /// Confirmed boundaries are cached; authenticated lookup failures use EU without caching.
    /// </summary>
    public static async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        ILoggingService? loggingService = null)
    {
        return await ResolveAndCacheAsync(
            tenantId,
            accessTokenProvider,
            DataBoundaryService.GetDataBoundaryAsync,
            timeout,
            cancellationToken,
            loggingService).ConfigureAwait(false);
    }

    internal static async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        Func<string, CancellationToken, Task<string?>> dataBoundaryProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        ILoggingService? loggingService = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return DataBoundaryRouting.Row;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        string? token;
        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            token = await accessTokenProvider(timeoutCts.Token).ConfigureAwait(false);
            timeoutCts.Token.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            LogFallback(loggingService, $"Silent authentication failed ({ex.GetType().Name}); telemetry will use ROW.");
            return DataBoundaryRouting.Row;
        }

        if (string.IsNullOrEmpty(token) || !TokenMatchesTenant(token, tenantId))
        {
            LogFallback(loggingService, "No tenant-matching token is available; telemetry will use ROW.");
            return DataBoundaryRouting.Row;
        }

        try
        {
            var cached = DataBoundaryCache.Get(tenantId);
            if (IsKnownBoundary(cached) ||
                string.Equals(cached, DataBoundaryRouting.Row, StringComparison.OrdinalIgnoreCase))
                return DataBoundaryRouting.Normalize(cached);

            var boundary = await dataBoundaryProvider(token, timeoutCts.Token).ConfigureAwait(false);
            if (!IsKnownBoundary(boundary))
            {
                LogFallback(loggingService, "Data boundary lookup returned no usable value; telemetry will use EU.");
                return DataBoundaryRouting.Eu;
            }

            var normalized = DataBoundaryRouting.Normalize(boundary);
            DataBoundaryCache.Set(tenantId, normalized);
            return normalized;
        }
        catch (Exception ex)
        {
            LogFallback(loggingService, $"Data boundary lookup failed ({ex.GetType().Name}); telemetry will use EU.");
            return DataBoundaryRouting.Eu;
        }
    }

    private static bool IsKnownBoundary(string? boundary) =>
        string.Equals(boundary, "EU", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(boundary, "Global", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(boundary, "NotDefined", StringComparison.OrdinalIgnoreCase);

    private static void LogFallback(ILoggingService? loggingService, string message)
    {
        try
        {
            loggingService?.Debug($"[{nameof(DataBoundaryResolver)}] {message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Data boundary diagnostics could not be logged ({ex.GetType().Name}). {message}");
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
