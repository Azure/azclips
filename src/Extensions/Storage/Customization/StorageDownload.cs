// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageDownload
{
    internal static void ValidateOptions(int workers, int chunkSize, long? start, long? end)
    {
        if (workers is < 1 or > 4 || chunkSize is < 1 or > StorageWireFormat.MaximumChunkBytes)
            throw new ArgumentException("Use 1-4 workers and chunks of 1 byte through 8 MiB.");
        if (start is < 0 || end is < 0 || end is not null && (start is null || end < start))
            throw new ArgumentException("Use nonnegative inclusive range bounds; an end requires a start and cannot precede it.");
    }

    internal static string ResolveDestination(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Specify a resolved destination file.");
        string result = Path.GetFullPath(destination);
        ValidateFileName(Path.GetFileName(result));
        if (Directory.Exists(result))
            throw new ArgumentException("The destination must identify a valid file, not a directory.");
        if (!Directory.Exists(Path.GetDirectoryName(result)))
            throw new DirectoryNotFoundException("The destination's parent directory must exist.");
        return result;
    }

    internal static void ValidateFileName(string name)
    {
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith(' ') || name.EndsWith('.'))
            throw new ArgumentException("The destination must identify a valid file, not a directory.");
        string stem = name.Split('.')[0].TrimEnd(' ');
        if (OperatingSystem.IsWindows() && (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) || stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3'))
            throw new ArgumentException("The destination cannot be a reserved device filename.");
    }

    internal static async Task TransferAsync(Stream output, long total, int workers, int chunkSize, byte[]? expectedHash,
        Func<long, int, Task<byte[]>> readRange, Func<long, long, bool, Task> progress, string checksumFailure,
        CancellationToken token)
    {
        using var hash = expectedHash is null ? null : StorageWireFormat.CreateContentMd5Hash();
        long transferred = 0;
        while (transferred < total)
        {
            token.ThrowIfCancellationRequested();
            int count = (int)Math.Min(workers, (total - transferred - 1) / chunkSize + 1);
            var requests = new Task<byte[]>[count];
            long cursor = transferred;
            for (int index = 0; index < count; index++)
            {
                int bytes = (int)Math.Min(chunkSize, total - cursor);
                requests[index] = readRange(cursor, bytes);
                cursor += bytes;
            }
            // Drain every request before consuming or leaving this bounded window.
            byte[][] chunks = await Task.WhenAll(requests);
            foreach (byte[] bytes in chunks)
            {
                token.ThrowIfCancellationRequested();
                hash?.AppendData(bytes);
                await output.WriteAsync(bytes, token);
                transferred += bytes.Length;
                await progress(transferred, total, false);
            }
        }
        if (hash is not null && !hash.GetHashAndReset().AsSpan().SequenceEqual(expectedHash))
            throw new InvalidDataException(checksumFailure);
        await output.FlushAsync(token);
    }

    internal static async Task<byte[]> ReadRangeAsync(HttpResponseMessage response, long offset, int count,
        long size, string? etag, bool validate, CancellationToken token)
    {
        var range = response.Content.Headers.ContentRange;
        if (response.StatusCode != HttpStatusCode.PartialContent || range?.Unit != "bytes" || range.From != offset ||
            range.To != offset + count - 1 || range.Length != size ||
            response.Content.Headers.ContentLength != count || response.Headers.ETag?.ToString() != etag)
            throw new InvalidDataException("Storage returned an inconsistent download range, length or ETag.");
        byte[] bytes = await StorageWireFormat.ReadChunkAsync(response.Content, token);
        if (bytes.Length != count)
            throw new InvalidDataException("Storage returned an incomplete or oversized download range.");
        if (validate)
        {
            byte[]? expected;
            try { expected = response.Content.Headers.ContentMD5; }
            catch (FormatException) { throw new InvalidDataException("Storage returned an invalid download Content-MD5."); }
            if (expected is null || expected.Length != 16 || !StorageWireFormat.ContentMd5(bytes).AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("The downloaded range did not match its Content-MD5.");
        }
        return bytes;
    }
}
