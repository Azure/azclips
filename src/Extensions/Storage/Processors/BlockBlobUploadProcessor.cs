// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlockBlobUploadProcessor : StorageBlobProcessorBase
{
    private const int ChunkSize = 4 * 1024 * 1024;

    public Stream Content { get; set; } = Stream.Null;
    public long ContentLength { get; set; }
    public bool Overwrite { get; set; }
    public string BlobType { get; set; } = "Block";
    public string[] Metadata { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Properties { get; set; } = [];
    public string? StandardBlobTier { get; set; }
    public string? PremiumPageBlobTier { get; set; }
    public string? EncryptionScope { get; set; }
    public string? TagCondition { get; set; }

    public override async Task ProcessAsync()
    {
        using HttpResponseMessage response = await SendAsync();
        await response.EnsureSuccessAsync(Publisher);
    }

    internal async Task<HttpResponseMessage> SendAsync()
    {
        if (!string.IsNullOrWhiteSpace(StandardBlobTier)
            && !BlobType.Equals("Block", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("StandardBlobTier can only be used with block blobs.", nameof(StandardBlobTier));
        }
        if (!string.IsNullOrWhiteSpace(PremiumPageBlobTier)
            && !BlobType.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("PremiumPageBlobTier can only be used with page blobs.", nameof(PremiumPageBlobTier));
        }

        return BlobType.ToLowerInvariant() switch
        {
            "block" => await UploadBlockBlobAsync(),
            "page" => await UploadPageBlobAsync(),
            "append" => await UploadAppendBlobAsync(),
            _ => throw new ArgumentException("BlobType must be Block, Page, or Append.", nameof(BlobType)),
        };
    }

    private async Task<HttpResponseMessage> UploadBlockBlobAsync()
    {
        if (ContentLength <= 5000L * 1024 * 1024)
        {
            using HttpRequestMessage request = CreateUploadRequest(BuildBlobUri(), "BlockBlob");
            request.Content = CreateStreamContent(Content, ContentLength);
            return await SendAsync(request);
        }

        var blockIds = new List<string>();
        long offset = 0;
        while (offset < ContentLength)
        {
            byte[] chunk = await ReadChunkAsync();
            string blockId = GenerateBlockId(offset);
            blockIds.Add(blockId);
            Uri uri = WithQuery(BuildBlobUri(), $"comp=block&blockid={Uri.EscapeDataString(blockId)}");
            using HttpRequestMessage request = CreateRequest(HttpMethod.Put, uri);
            AddRequestConditions(request);
            request.Content = new ByteArrayContent(chunk);
            using HttpResponseMessage response = await SendAsync(request);
            await response.EnsureSuccessAsync(Publisher);
            offset += chunk.Length;
        }

        string xml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>{string.Concat(blockIds.Select(id => $"<Latest>{id}</Latest>"))}</BlockList>";
        using HttpRequestMessage commit = CreateUploadRequest(WithQuery(BuildBlobUri(), "comp=blocklist"), null);
        commit.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return await SendAsync(commit);
    }

    private async Task<HttpResponseMessage> UploadPageBlobAsync()
    {
        if (ContentLength % 512 != 0)
        {
            throw new ArgumentException("Page blob file size must be a multiple of 512 bytes.", nameof(ContentLength));
        }

        using (HttpRequestMessage create = CreateUploadRequest(BuildBlobUri(), "PageBlob"))
        {
            create.Headers.TryAddWithoutValidation("x-ms-blob-content-length", ContentLength.ToString(CultureInfo.InvariantCulture));
            HttpResponseMessage response = await SendAsync(create);
            if (!response.IsSuccessStatusCode)
            {
                return response;
            }
            response.Dispose();
        }

        long offset = 0;
        while (offset < ContentLength)
        {
            byte[] chunk = await ReadChunkAsync();
            using HttpRequestMessage request = CreateRequest(HttpMethod.Put, WithQuery(BuildBlobUri(), "comp=page"));
            AddRequestConditions(request);
            request.Headers.TryAddWithoutValidation("x-ms-page-write", "update");
            request.Headers.TryAddWithoutValidation("x-ms-range", $"bytes={offset}-{offset + chunk.Length - 1}");
            request.Content = new ByteArrayContent(chunk);
            using HttpResponseMessage response = await SendAsync(request);
            await response.EnsureSuccessAsync(Publisher);
            offset += chunk.Length;
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> UploadAppendBlobAsync()
    {
        using (HttpRequestMessage create = CreateUploadRequest(BuildBlobUri(), "AppendBlob"))
        {
            HttpResponseMessage response = await SendAsync(create);
            if (!response.IsSuccessStatusCode)
            {
                return response;
            }
            response.Dispose();
        }

        while (Content.Position < ContentLength)
        {
            byte[] chunk = await ReadChunkAsync();
            using HttpRequestMessage request = CreateRequest(HttpMethod.Put, WithQuery(BuildBlobUri(), "comp=appendblock"));
            AddRequestConditions(request);
            request.Content = new ByteArrayContent(chunk);
            using HttpResponseMessage response = await SendAsync(request);
            await response.EnsureSuccessAsync(Publisher);
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.Created);
    }

    private HttpRequestMessage CreateUploadRequest(Uri uri, string? blobType)
    {
        HttpRequestMessage request = CreateRequest(HttpMethod.Put, uri);
        request.Options.Set(RuntimeHttpRequestOptions.DisableRetry, true);
        if (blobType is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-type", blobType);
        }
        if (!Overwrite)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        }
        AddBlobHeaders(request);
        return request;
    }

    private void AddBlobHeaders(HttpRequestMessage request)
    {
        AddPairs(request, Metadata, "x-ms-meta-");
        if (Tags.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("x-ms-tags", string.Join("&", ParsePairs(Tags).Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
        }
        foreach ((string key, string value) in ParsePairs(Properties))
        {
            string header = key.ToLowerInvariant() switch
            {
                "contenttype" => "x-ms-blob-content-type",
                "contentencoding" => "x-ms-blob-content-encoding",
                "contentlanguage" => "x-ms-blob-content-language",
                "cachecontrol" => "x-ms-blob-cache-control",
                "contentdisposition" => "x-ms-blob-content-disposition",
                "contentmd5" => "x-ms-blob-content-md5",
                _ => throw new ArgumentException($"Unsupported blob property '{key}'.", nameof(Properties)),
            };
            request.Headers.TryAddWithoutValidation(header, value);
        }
        string? tier = BlobType.Equals("Page", StringComparison.OrdinalIgnoreCase)
            ? PremiumPageBlobTier
            : StandardBlobTier;
        if (!string.IsNullOrWhiteSpace(tier))
        {
            request.Headers.TryAddWithoutValidation("x-ms-access-tier", tier);
        }
        AddRequestConditions(request);
    }

    private void AddRequestConditions(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(EncryptionScope))
        {
            request.Headers.TryAddWithoutValidation("x-ms-encryption-scope", EncryptionScope);
        }
        if (!string.IsNullOrWhiteSpace(TagCondition))
        {
            request.Headers.TryAddWithoutValidation("x-ms-if-tags", TagCondition);
        }
    }

    private static void AddPairs(HttpRequestMessage request, string[] values, string prefix)
    {
        foreach ((string key, string value) in ParsePairs(values))
        {
            if (!request.Headers.TryAddWithoutValidation(prefix + key, value))
            {
                throw new ArgumentException($"Invalid metadata name '{key}'.", nameof(values));
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ParsePairs(string[] values)
    {
        foreach (string value in values)
        {
            int separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Expected KEY=VALUE but received '{value}'.");
            }
            yield return new KeyValuePair<string, string>(value[..separator], value[(separator + 1)..]);
        }
    }

    private async Task<byte[]> ReadChunkAsync()
    {
        int expected = (int)Math.Min(ChunkSize, ContentLength - Content.Position);
        byte[] buffer = new byte[expected];
        int read = 0;
        while (read < expected)
        {
            int count = await Content.ReadAsync(buffer.AsMemory(read, expected - read), Publisher.Token);
            if (count == 0)
            {
                throw new EndOfStreamException("The upload file ended before the expected content length.");
            }
            read += count;
        }
        return buffer;
    }

    private static StreamContent CreateStreamContent(Stream stream, long length)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentLength = length;
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static string GenerateBlockId(long offset)
    {
        byte[] id = new byte[48];
        BitConverter.GetBytes(offset).CopyTo(id, 0);
        return Convert.ToBase64String(id);
    }

    private static Uri WithQuery(Uri uri, string query) => new UriBuilder(uri) { Query = query }.Uri;
}
