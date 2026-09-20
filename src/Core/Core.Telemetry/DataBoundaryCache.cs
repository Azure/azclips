// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Simple file-backed cache that maps tenant IDs to ARM data-boundary values.
/// Stored at <c>{LocalAppData}/.azclips/data-boundary-cache.json</c> as a
/// <c>Dictionary&lt;string, string&gt;</c> (tenantId → boundary).
/// </summary>
/// <remarks>
/// Backed by an in-process memory cache in front of the file: <see cref="Get"/> is now called
/// on every command invocation that carries a profile (see <c>ExecutionSession</c>'s data
/// boundary tagging), and a long-lived process — PowerShell's module import, or a benchmark
/// harness looping many invocations — would otherwise re-read and re-parse the JSON file from
/// disk on every single command. The memory cache is keyed by (cache file path, tenantId), not
/// tenantId alone, so switching the effective home root (e.g. tests overriding
/// <see cref="HomePaths.HomeOverrideVariable"/> per test) does not leak a cached value from one
/// root into another.
/// </remarks>
internal static class DataBoundaryCache
{
    private static readonly ConcurrentDictionary<(string CachePath, string TenantId), string> MemoryCache = new();

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
            // Populate the memory cache even if the file write above failed — this process
            // already knows the answer for the rest of its lifetime either way.
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
