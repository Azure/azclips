// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;

namespace Core.Runtime.Persistence;

/// <summary>Backend-independent text operations for byte-oriented persistence, defaulting to UTF-8.</summary>
public static class PersistenceStoreExtensions
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads text with BOM detection and a UTF-8 default, returning null for an absent file. Storage errors propagate.</summary>
    public static async Task<string?> ReadTextAsync(
        this IPersistenceStore persistence,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        cancellationToken.ThrowIfCancellationRequested();
        var content = await persistence.ReadAsync(relativePath, cancellationToken);
        if (content is null)
        {
            return null;
        }

        using var reader = new StreamReader(new MemoryStream(content), Utf8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>Writes UTF-8 text without a BOM. Invalid Unicode and storage errors propagate.</summary>
    public static Task WriteTextAsync(
        this IPersistenceStore persistence,
        string relativePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        return persistence.WriteAsync(relativePath, Utf8.GetBytes(content), cancellationToken);
    }
}
