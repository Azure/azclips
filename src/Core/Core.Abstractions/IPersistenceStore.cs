// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// User-scoped storage. Consumers supply relative paths and own serialization and read-modify-write
/// synchronization. SDKs that own their I/O can use the managed file and directory paths.
/// </summary>
public interface IPersistenceStore
{
    string RootDirectory { get; }
    /// <summary>Resolves a file path without creating it.</summary>
    string GetFilePath(string relativePath);
    /// <summary>Ensures a directory exists and returns its path, using the root when no relative path is supplied.</summary>
    Task<string> EnsureDirectoryAsync(string relativePath = "", CancellationToken cancellationToken = default);
    /// <summary>Reads bytes; null means the file is absent. Other failures propagate.</summary>
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
    /// <summary>Creates parent directories and atomically replaces the complete file contents.</summary>
    Task WriteAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    /// <summary>Deletes a file if present. Other failures propagate.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
