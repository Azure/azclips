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
    private static string CachePath => Path.Combine(
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
            var cachePath = CachePath;
            if (!File.Exists(cachePath))
                return null;

            var json = File.ReadAllText(cachePath);
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
            var cachePath = CachePath;
            var dir = Path.GetDirectoryName(cachePath)!;
            Directory.CreateDirectory(dir);

            using var cacheLock = TryAcquireLock(cachePath);
            Dictionary<string, string> cache;
            if (File.Exists(cachePath))
            {
                var json = File.ReadAllText(cachePath);
                cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
            }
            else
            {
                cache = new Dictionary<string, string>();
            }

            cache[tenantId] = dataBoundary;

            var tempPath = Path.Combine(
                dir,
                $"{Path.GetFileName(cachePath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, JsonSerializer.Serialize(cache, JsonOptions));
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            // Best effort — cache write failure is non-fatal.
        }
    }

    private static FileStream TryAcquireLock(string cachePath)
    {
        var lockPath = cachePath + ".lock";
        var deadline = DateTime.UtcNow.AddSeconds(2);

        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }
        }
    }
}
