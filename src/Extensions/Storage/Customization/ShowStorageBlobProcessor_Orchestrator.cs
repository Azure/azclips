// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ShowStorageBlobProcessor_Orchestrator : StorageBlobProcessorBase<object, object>
{
    private StorageBlob? _blob;

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public bool IncludeTag { get; set; }

    protected override Task BeforeProcessAsync()
    {
        if (ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required for authenticated Storage requests.");
        }
        return base.BeforeProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        string url = StorageBlobCustomizationHelpers.BuildBlobUrl(AccountName, ContainerName, BlobName);
        GetStorageBlobPropertiesProcessor propertiesProcessor =
            CreateExtendedProcessor<GetStorageBlobPropertiesProcessor>(p => p.Url = url);
        HttpResponseMessage response = await propertiesProcessor.SendAsync();
        try
        {
            IReadOnlyDictionary<string, string> tags = IncludeTag
                ? await GetTagsAsync(url)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _blob = ToStorageBlob(response, tags);
            response.Content?.Dispose();
            response.Content = null;
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    protected override Task BeforeWriteOutputAsync(object? body, ref bool returnNow)
    {
        returnNow = true;
        return WriteOutput(_blob!, Publisher.Token);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetTagsAsync(string url)
    {
        GetStorageBlobTagsProcessor tagsProcessor =
            CreateExtendedProcessor<GetStorageBlobTagsProcessor>(p => p.Url = url);
        using HttpResponseMessage response = await tagsProcessor.SendAsync();
        BlobTags tags = await tagsProcessor.DeserializeResponseAsync(response)
            ?? throw new InvalidDataException("The get blob tags response did not contain a body.");
        return tags.TagSet
            .ToDictionary(
                tag => tag.Key,
                tag => tag.Value,
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
