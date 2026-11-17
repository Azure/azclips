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
/// Memory entries belong to this store instance and cannot leak into another storage root.
/// </remarks>
internal sealed class DataBoundaryCache(IPersistenceStore persistence)
{
    private readonly ConcurrentDictionary<string, string> _memoryCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Returns the cached value or null; the resolver validates values from legacy cache files.
    /// </summary>
    internal async Task<string?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_memoryCache.TryGetValue(tenantId, out var memoryValue))
            return memoryValue;

        try
        {
            var content = await persistence.ReadAsync(HomePaths.DataBoundaryCacheFileName, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (content is null)
                return null;

            var cache = Deserialize(content);
            if (cache is not null && cache.TryGetValue(tenantId, out var value))
            {
                _memoryCache[tenantId] = value;
                return value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ReportFailure("read", ex);
        }

        return null;
    }

    /// <summary>
    /// Stores (or updates) the data boundary for <paramref name="tenantId"/>.
    /// </summary>
    internal async Task SetAsync(string tenantId, string dataBoundary, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DataBoundaryRouting.IsConfirmed(dataBoundary))
            throw new ArgumentException("Only confirmed EU or Global boundaries can be cached.", nameof(dataBoundary));

        dataBoundary = DataBoundaryRouting.Normalize(dataBoundary);
        try
        {
            await persistence.EnsureDirectoryAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
            var content = await persistence.ReadAsync(HomePaths.DataBoundaryCacheFileName, cancellationToken).ConfigureAwait(false);
            var cache = content is null
                ? new Dictionary<string, string>()
                : Deserialize(content);

            cache[tenantId] = dataBoundary;

            await persistence.WriteAsync(HomePaths.DataBoundaryCacheFileName,
                JsonSerializer.SerializeToUtf8Bytes(cache, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ReportFailure("write", ex);
        }
        cancellationToken.ThrowIfCancellationRequested();
        _memoryCache[tenantId] = dataBoundary;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = persistence.GetFilePath(HomePaths.DataBoundaryCacheFileName + ".lock");
        var deadline = DateTime.UtcNow.AddSeconds(2);

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }

        }
    }

    private static void ReportFailure(string operation, Exception exception) =>
        Console.Error.WriteLine($"[{nameof(DataBoundaryCache)}] Cache {operation} failed: {exception.GetType().Name}.");

    private static Dictionary<string, string> Deserialize(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? new();
    }
}
