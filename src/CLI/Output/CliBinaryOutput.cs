// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli.Output;

internal static class CliBinaryOutput
{
    internal static async Task<bool> TryWriteAsync(object? value, Stream destination, string? query, CancellationToken token)
    {
        if (value is not BinaryOutputChunk chunk) return false;
        if (query is not null)
            throw new ArgumentException("Binary download output cannot be queried. Download to a file to query its properties.");
        await destination.WriteAsync(chunk.Bytes, token);
        return true;
    }
}
