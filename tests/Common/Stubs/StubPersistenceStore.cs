// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubPersistenceStore(string rootDirectory) : IPersistenceStore
{
    public string RootDirectory { get; } = rootDirectory;
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public string GetFilePath(string relativePath) => Path.Combine(RootDirectory, relativePath);

    public virtual Task<string> EnsureDirectoryAsync(string relativePath = "", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(RootDirectory, relativePath);
        Directory.CreateDirectory(path);
        return Task.FromResult(path);
    }

    public virtual Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Files.TryGetValue(relativePath, out var value) ? value.ToArray() : null);
    }

    public virtual Task WriteAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Files[relativePath] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Files.Remove(relativePath);
        return Task.CompletedTask;
    }
}
