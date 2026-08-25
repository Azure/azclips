// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ShowStorageBlobProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public bool IncludeTag { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string url = StorageBlobCustomizationHelpers.BuildBlobUrl(AccountName, ContainerName, BlobName);
            GetStorageBlobPropertiesProcessor propertiesProcessor =
                CreateExtendedProcessor<GetStorageBlobPropertiesProcessor>(p => p.Url = url);
            using HttpResponseMessage response = await propertiesProcessor.SendAsync();
            IReadOnlyDictionary<string, string> tags = IncludeTag
                ? await GetTagsAsync(url)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await WriteOutput(ToStorageBlob(response, tags), Publisher.Token);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> GetTagsAsync(string url)
    {
        GetStorageBlobTagsProcessor tagsProcessor =
            CreateExtendedProcessor<GetStorageBlobTagsProcessor>(p => p.Url = url);
        using HttpResponseMessage response = await tagsProcessor.SendAsync();
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
        Dictionary<string, string> metadata = response.Headers
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
