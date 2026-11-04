// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// File-backed tenant cache that stores confirmed EU/Global boundaries, never fallback destinations.
/// </summary>
/// <remarks>
/// Memory entries include the cache path so changing AZCLIPS_HOME cannot reuse another home's values.
/// </remarks>
internal static class DataBoundaryCache
{
    private static readonly ConcurrentDictionary<(string CachePath, string TenantId), string> MemoryCache = new();

    private static string CachePath => HomePaths.GetDefaultDataBoundaryCachePath();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Returns the cached value or null; the resolver validates values from legacy cache files.
    /// </summary>
    internal static string? Get(string tenantId)
    {
        var cachePath = CachePath;
        if (MemoryCache.TryGetValue((cachePath, tenantId), out var memoryValue))
            return memoryValue;

        try
        {
            if (!File.Exists(cachePath))
                return null;

            var json = File.ReadAllText(cachePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (cache is not null && cache.TryGetValue(tenantId, out var value))
            {
                MemoryCache[(cachePath, tenantId)] = value;
                return value;
            }
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
        if (!DataBoundaryRouting.IsConfirmed(dataBoundary))
            throw new ArgumentException("Only confirmed EU or Global boundaries can be cached.", nameof(dataBoundary));

        dataBoundary = DataBoundaryRouting.Normalize(dataBoundary);
        var cachePath = CachePath;
        try
        {
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
        finally
        {
            // Retain the confirmed value in memory even if persistence fails.
            MemoryCache[(cachePath, tenantId)] = dataBoundary;
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
