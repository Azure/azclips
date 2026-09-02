// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageBlobProcessor_Orchestrator : StorageBlobProcessorBase<object, ListBlobsResponse>
{
    private readonly List<StorageBlob> _blobs = [];
    private ListBlobsResponse? _lastPage;

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public int NumResults { get; set; }
    public string[] Include { get; set; } = [];

    protected override Task BeforeProcessAsync()
    {
        if (NumResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumResults), "NumResults cannot be negative.");
        }
        if (ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required for authenticated Storage requests.");
        }
        _blobs.Clear();
        _lastPage = null;
        return base.BeforeProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        string? marker = Marker;
        while (true)
        {
            int? maxResults = NumResults == 0 ? null : Math.Min(5000, NumResults - _blobs.Count);
            ListStorageBlobProcessor processor = CreateExtendedProcessor<ListStorageBlobProcessor>(p =>
            {
                p.Url = StorageBlobCustomizationHelpers.BuildContainerUrl(AccountName, ContainerName);
                p.Prefix = Prefix;
                p.Marker = marker;
                p.MaxResults = maxResults;
                p.Include = Include;
            });
            HttpResponseMessage response = await processor.SendAsync();
            ListBlobsResponse page;
            try
            {
                page = await processor.DeserializeResponseAsync(response)
                    ?? throw new InvalidDataException("The list blobs response did not contain a body.");
            }
            catch
            {
                response.Dispose();
                throw;
            }
            _blobs.AddRange(page.Blobs.Select(ToStorageBlob));
            marker = page.NextMarker;
            if (string.IsNullOrWhiteSpace(marker) || NumResults > 0 && _blobs.Count >= NumResults)
            {
                _lastPage = page;
                return response;
            }
            response.Dispose();
        }
    }

    internal override Task<ListBlobsResponse?> DeserializeResponseAsync(HttpResponseMessage response)
    {
        ListBlobsResponse? page = _lastPage;
        _lastPage = null;
        return Task.FromResult(page);
    }

    protected override Task BeforeWriteOutputAsync(ListBlobsResponse? body, ref bool returnNow)
    {
        returnNow = true;
        StorageBlob[] blobs = NumResults == 0
            ? _blobs.ToArray()
            : _blobs.Take(NumResults).ToArray();
        if (blobs.Length > 0 && !string.IsNullOrWhiteSpace(body?.NextMarker))
        {
            blobs[^1] = blobs[^1] with { ContinuationToken = body.NextMarker };
        }
        return WriteOutput(blobs, Publisher.Token);
    }

    private static StorageBlob ToStorageBlob(BlobItem item)
    {
        return new StorageBlob(
            item.Name.Content,
            item.Properties.BlobType?.ToString(),
            item.Properties.ContentLength,
            item.Properties.ContentType,
            item.Properties.ETag,
            item.Properties.LastModified,
            item.Properties.AccessTier,
            item.Metadata?.Items.ToDictionary(
                element => element.LocalName,
                element => element.InnerText,
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            item.Tags?.TagSet.ToDictionary(
                tag => tag.Key,
                tag => tag.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
