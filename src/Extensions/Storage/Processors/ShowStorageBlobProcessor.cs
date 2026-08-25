// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ShowStorageBlobProcessor : StorageBlobProcessorBase
{
    public bool IncludeTag { get; set; }

    public override async Task ProcessAsync()
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Head);
        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);

        IReadOnlyDictionary<string, string> tags = IncludeTag
            ? await GetTagsAsync()
            : new Dictionary<string, string>();
        await WriteOutput(ToStorageBlob(response, tags), Publisher.Token);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetTagsAsync()
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Get,
            new UriBuilder(BuildBlobUri()) { Query = "comp=tags" }.Uri);
        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);
        XDocument document = await XDocument.LoadAsync(
            await response.Content.ReadAsStreamAsync(Publisher.Token),
            LoadOptions.None,
            Publisher.Token);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "Tag")
            .ToDictionary(
                tag => tag.Elements().First(element => element.Name.LocalName == "Key").Value,
                tag => tag.Elements().First(element => element.Name.LocalName == "Value").Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private StorageBlob ToStorageBlob(
        HttpResponseMessage response,
        IReadOnlyDictionary<string, string> tags)
    {
        var metadata = response.Headers
            .Where(header => header.Key.StartsWith("x-ms-meta-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                header => header.Key["x-ms-meta-".Length..],
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);

        return new StorageBlob(
            BlobName,
            Header(response, "x-ms-blob-type"),
            response.Content?.Headers.ContentLength,
            response.Content?.Headers.ContentType?.ToString(),
            response.Headers.ETag?.Tag,
            Header(response, "Last-Modified"),
            Header(response, "x-ms-access-tier"),
            metadata,
            tags);
    }
}
