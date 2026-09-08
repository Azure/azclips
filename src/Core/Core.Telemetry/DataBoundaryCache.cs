// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Simple file-backed cache that maps tenant IDs to ARM data-boundary values.
/// Stored at <c>{LocalAppData}/.azclips/data-boundary-cache.json</c> as a
/// <c>Dictionary&lt;string, string&gt;</c> (tenantId → boundary).
/// </summary>
internal static class DataBoundaryCache
{
    private static readonly string CachePath = Path.Combine(
        HomePaths.GetDefaultLocalAppDataRoot(),
        HomePaths.DataBoundaryCacheFileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Returns the cached data boundary for <paramref name="tenantId"/>, or <c>null</c> if not cached.
    /// </summary>
    internal static string? Get(string tenantId)
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            var json = File.ReadAllText(CachePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (cache is not null && cache.TryGetValue(tenantId, out var value))
                return value;
        }
        catch
        {
            // Best effort.
        }

        return null;
    }

    /// <summary>
    /// Stores (or updates) the data boundary for <paramref name="tenantId"/>.
    /// </summary>
    internal static void Set(string tenantId, string dataBoundary)
    {
        try
        {
            Dictionary<string, string> cache;
            if (File.Exists(CachePath))
            {
                var json = File.ReadAllText(CachePath);
                cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
            }
            else
            {
                cache = new Dictionary<string, string>();
            }

            cache[tenantId] = dataBoundary;

            var dir = Path.GetDirectoryName(CachePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch
        {
            // Best effort — cache write failure is non-fatal.
        }
    }
}
