// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class ListStorageBlobsProcessor : BlobContainerWorkflowBase
{
    public string? Prefix { get; set; }
    public string? Delimiter { get; set; }
    public IList<string>? Include { get; set; }
    public string? Marker { get; set; }
    public StorageBlobContinuationToken? ContinuationToken { get; set; }
    public long? NumResults { get; set; }
    public int? MaxCount { get; set; }
    public bool ShowNextMarker { get; set; }
    internal Func<string, bool>? NameFilter { get; set; }
    internal Func<StorageBlobEntry, CancellationToken, Task>? EntryHandler { get; set; }
    internal Action<string?>? NextMarkerHandler { get; set; }

    protected override async Task ExecuteAsync()
    {
        var container = ResolveContainer();
        if (NumResults is <= 0 || NumResults is not null && MaxCount is not null ||
            Marker is not null && ContinuationToken is not null || Delimiter == string.Empty)
            throw new ArgumentException("Specify a positive result limit, one marker and one limit mode.");
        if (Include?.Any(value => value is not ("copy" or "deleted" or "metadata" or "snapshots" or "uncommittedblobs" or
            "versions" or "tags" or "immutabilitypolicy" or "legalhold" or "deletedwithversions")) == true)
            throw new ArgumentException("An unsupported blob listing include value was supplied.");
        if (ContinuationToken is { } token &&
            (token.StorageAccountName != container.Context.StorageAccountName || token.Container != container.Name ||
             token.Prefix != Prefix || token.Delimiter != Delimiter || string.IsNullOrEmpty(token.NextMarker)))
            throw new ArgumentException("The continuation token belongs to a different listing.");
        string? marker = ContinuationToken?.NextMarker ?? Marker;
        long remaining = NumResults ?? (MaxCount is > 0 ? MaxCount.Value : long.MaxValue);
        var seenMarkers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(marker)) seenMarkers.Add(marker);
        do
        {
            Token.ThrowIfCancellationRequested();
            int pageSize = (int)Math.Min(5000, remaining);
            StorageDataPlaneProcessorBase child = Delimiter is null
                ? CreateBlobProcessor<BlobProcessors.ListBlobsContainerProcessor>(container, processor =>
                {
                    processor.Url = container.Uri.AbsoluteUri; processor.Prefix = Prefix; processor.Marker = marker;
                    processor.Maxresults = pageSize; processor.Include = Include; processor.Timeout = Timeout;
                })
                : CreateBlobProcessor<BlobProcessors.ListBlobHierarchySegmentContainerProcessor>(container, processor =>
                {
                    processor.Url = container.Uri.AbsoluteUri; processor.Prefix = Prefix; processor.Marker = marker;
                    processor.Maxresults = pageSize; processor.Include = Include; processor.Timeout = Timeout; processor.Delimiter = Delimiter;
                });
            using var response = await child.SendAsync();
            var page = await StorageBlobXml.ReadPageAsync(response.Content, container, Delimiter is not null, Token);
            if (page.Entries.Count > pageSize || page.NextMarker is { } next && !seenMarkers.Add(next))
                throw new InvalidDataException("Storage returned an oversized page or a repeated continuation marker.");
            marker = page.NextMarker;
            var continuation = marker is null ? null : new StorageBlobContinuationToken(marker, container.Context.StorageAccountName, container.Name, Prefix, Delimiter);
            foreach (var entry in page.Entries)
            {
                Token.ThrowIfCancellationRequested();
                bool selected = NameFilter?.Invoke(entry.Name) ?? true;
                if (MaxCount is not null || selected) remaining--;
                if (selected)
                {
                    var output = entry is StorageBlob blob ? blob with { ContinuationToken = continuation } : entry;
                    await (EntryHandler?.Invoke(output, Token) ?? WriteOutput(output, Token));
                }
            }
        } while (remaining > 0 && marker is not null);

        if (NextMarkerHandler is not null)
            NextMarkerHandler(marker);
        else if (ShowNextMarker)
            await WriteOutput(new StorageBlobNextMarker(marker), Token);
        else if (marker is not null)
            await Publisher.Publish(Events.Warning, "Next Marker: " + marker);
    }
}
