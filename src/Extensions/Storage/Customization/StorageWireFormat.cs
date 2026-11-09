// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using FileModels = Microsoft.Azure.Azclips.Extensions.Storage.Files.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageWireFormat
{
    internal const int MaximumChunkBytes = 8 * 1024 * 1024;
    internal const long MaximumBlockBytes = 4000L * 1024 * 1024;

    [SuppressMessage("Security", "CA5351", Justification = "Azure Storage Content-MD5 is a protocol checksum, not a cryptographic security primitive.")]
    internal static byte[] ContentMd5(byte[] bytes) => MD5.HashData(bytes);

    [SuppressMessage("Security", "CA5351", Justification = "Azure Storage Content-MD5 is a protocol checksum, not a cryptographic security primitive.")]
    internal static IncrementalHash CreateContentMd5Hash() => IncrementalHash.CreateHash(HashAlgorithmName.MD5);

    internal static async Task<byte[]> ReadFileChunkAsync(FileStream source, long offset, int length, CancellationToken token)
    {
        byte[] bytes = new byte[length];
        await ReadFileChunkAsync(source, offset, bytes.AsMemory(), token);
        return bytes;
    }

    internal static async Task ReadFileChunkAsync(FileStream source, long offset, Memory<byte> bytes, CancellationToken token)
    {
        int read = 0;
        while (read < bytes.Length)
        {
            int received = await RandomAccess.ReadAsync(source.SafeFileHandle, bytes[read..], offset + read, token);
            if (received == 0) throw new EndOfStreamException("The source file ended before the expected upload length.");
            read += received;
        }
    }

    internal static async Task<byte[]> ContentMd5Async(StorageFileRange range, CancellationToken token)
    {
        using var hash = CreateContentMd5Hash();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(FileRangeBufferBytes);
        try
        {
            for (long done = 0; done < range.Length;)
            {
                int count = (int)Math.Min(FileRangeBufferBytes, range.Length - done);
                await ReadFileChunkAsync(range.Source, range.Offset + done, buffer.AsMemory(0, count), token);
                hash.AppendData(buffer, 0, count);
                done += count;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return hash.GetHashAndReset();
    }

    internal const int FileRangeBufferBytes = 64 * 1024;

    internal static string DecodeXmlValue(string value, bool encoded)
    {
        if (!encoded) return value;
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            if (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2]))
                throw InvalidXml();
            index += 2;
        }
        return Uri.UnescapeDataString(value);
    }

    internal static HttpContent? CreateContent(object? body)
    {
        if (body is null) return null;
        if (body is StorageFileRange range)
        {
            if (range.Length is < 0 or > MaximumBlockBytes || range.Offset < 0)
                throw new ArgumentException("Storage file-range bodies must be at most 4000 MiB.");
            var content = new StorageFileRangeContent(range);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return content;
        }
        if (body is byte[] bytes)
        {
            if (bytes.Length > MaximumChunkBytes)
                throw new ArgumentException("Storage request bodies must use chunks of at most 8 MiB.");
            var content = new ReplayableByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return content;
        }
        XElement xml = body switch
        {
            BlobModels.BlockLookupList blocks => new XElement("BlockList",
                BlockElements("Committed", blocks.Committed),
                BlockElements("Uncommitted", blocks.Uncommitted),
                BlockElements("Latest", blocks.Latest)),
            BlobModels.KeyInfo key => KeyInfo(key.Start, key.Expiry, key.DelegatedUserTid),
            FileModels.KeyInfo key => KeyInfo(key.Start, key.Expiry, key.DelegatedUserTid),
            _ => throw new NotSupportedException("The Storage request schema needs an explicit binary or XML codec.")
        };
        return new StringContent(xml.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false), "application/xml");
    }

    internal static async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken token)
        where TResponse : class
    {
        if (typeof(TResponse) == typeof(byte[]))
            return (TResponse)(object)await ReadChunkAsync(response.Content, token);
        var xml = await ReadXmlAsync(response.Content, token);
        object result;
        if (typeof(TResponse) == typeof(BlobModels.UserDelegationKey))
        {
            RequireRoot(xml, "UserDelegationKey");
            result = new BlobModels.UserDelegationKey
            {
                SignedOid = Required(xml, "SignedOid"), SignedTid = Required(xml, "SignedTid"),
                SignedStart = Required(xml, "SignedStart"), SignedExpiry = Required(xml, "SignedExpiry"),
                SignedService = Required(xml, "SignedService"), SignedVersion = Required(xml, "SignedVersion"),
                SignedDelegatedUserTid = (string?)xml.Element("SignedDelegatedUserTid"),
                Value = DecodeBase64(Required(xml, "Value"))
            };
        }
        else if (typeof(TResponse) == typeof(FileModels.UserDelegationKey))
        {
            RequireRoot(xml, "UserDelegationKey");
            result = new FileModels.UserDelegationKey
            {
                SignedOid = Required(xml, "SignedOid"), SignedTid = Required(xml, "SignedTid"),
                SignedStart = Required(xml, "SignedStart"), SignedExpiry = Required(xml, "SignedExpiry"),
                SignedService = Required(xml, "SignedService"), SignedVersion = Required(xml, "SignedVersion"),
                SignedDelegatedUserTid = (string?)xml.Element("SignedDelegatedUserTid"),
                Value = Required(xml, "Value")
            };
        }
        else if (typeof(TResponse) == typeof(BlobModels.BlobTags))
        {
            RequireRoot(xml, "Tags");
            result = StorageBlobXml.ReadTags(xml);
        }
        else if (typeof(TResponse) == typeof(BlobModels.ListBlobsResponse))
            result = StorageBlobXml.ReadFlat(xml);
        else if (typeof(TResponse) == typeof(BlobModels.ListBlobsHierarchicalResponse))
            result = StorageBlobXml.ReadHierarchy(xml);
        else
            throw new NotSupportedException("The Storage response schema needs an explicit XML codec before this operation can be exposed.");
        return (TResponse)result;
    }

    internal static async Task CopyToAsync(HttpContent content, Stream destination, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await using var source = await content.ReadAsStreamAsync(token);
        await source.CopyToAsync(destination, 64 * 1024, token);
    }

    internal static async Task<byte[]> ReadChunkAsync(HttpContent content, CancellationToken token)
    {
        if (content.Headers.ContentLength > MaximumChunkBytes)
            throw new InvalidDataException("Use the streaming download workflow or a range of at most 8 MiB.");
        await using var source = await content.ReadAsStreamAsync(token);
        if (content.Headers.ContentLength is long length)
        {
            byte[] bytes = new byte[checked((int)length)];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = await source.ReadAsync(bytes.AsMemory(offset), token);
                if (read == 0)
                    throw new InvalidDataException("Storage returned an incomplete download chunk.");
                offset += read;
            }
            if (await source.ReadAsync(new byte[1], token) != 0)
                throw new InvalidDataException("Storage returned an oversized download chunk.");
            return bytes;
        }
        using var output = new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaximumChunkBytes - (int)output.Length + 1)), token);
                if (read == 0) return output.ToArray();
                if (output.Length + read > MaximumChunkBytes)
                    throw new InvalidDataException("Use the streaming download workflow or a range of at most 8 MiB.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static async Task<XElement> ReadXmlAsync(HttpContent content, CancellationToken token)
    {
        await using var source = await content.ReadAsStreamAsync(token);
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 64 * 1024 * 1024
        });
        try
        {
            return await XElement.LoadAsync(reader, LoadOptions.None, token);
        }
        catch (XmlException)
        {
            throw InvalidXml();
        }
    }

    private static IEnumerable<XElement> BlockElements(string name, IEnumerable<byte[]>? values)
    {
        if (values is null) yield break;
        foreach (byte[] value in values)
        {
            if (value is null || value.Length is < 1 or > 64)
                throw new ArgumentException("Block IDs must contain 1-64 bytes before base64 encoding.");
            yield return new XElement(name, Convert.ToBase64String(value));
        }
    }

    private static XElement KeyInfo(string? start, string? expiry, string? delegatedUserTid)
    {
        if (string.IsNullOrWhiteSpace(expiry))
            throw new ArgumentException("A delegation-key expiry is required.");
        return new XElement("KeyInfo", start is null ? null : new XElement("Start", start),
            new XElement("Expiry", expiry), delegatedUserTid is null ? null : new XElement("DelegatedUserTid", delegatedUserTid));
    }

    private static string Required(XElement xml, string name, bool allowEmpty = false)
    {
        string? value = (string?)xml.Element(name);
        return value is not null && (allowEmpty || value.Length > 0) ? value : throw InvalidXml();
    }

    private static void RequireRoot(XElement xml, string name)
    {
        if (xml.Name != name) throw InvalidXml();
    }

    private static byte[] DecodeBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw InvalidXml();
        }
    }

    internal static InvalidDataException InvalidXml() =>
        new("Storage returned invalid or oversized XML. Sensitive response details are omitted; reduce the listing page size if necessary.");
}

/// <summary>A range of an open local file used as a Storage request body.</summary>
internal sealed record StorageFileRange(FileStream Source, long Offset, long Length);

/// <summary>Streams a file range on every send; each retry copy reads the range again from the file.</summary>
internal sealed class StorageFileRangeContent(StorageFileRange range) : HttpContent, IReplayableHttpContent
{
    public HttpContent CreateReplay() => new StorageFileRangeContent(range);

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(StorageWireFormat.FileRangeBufferBytes);
        try
        {
            for (long done = 0; done < range.Length;)
            {
                int count = (int)Math.Min(StorageWireFormat.FileRangeBufferBytes, range.Length - done);
                await StorageWireFormat.ReadFileChunkAsync(range.Source, range.Offset + done, buffer.AsMemory(0, count), cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                done += count;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        length = range.Length;
        return true;
    }
}
