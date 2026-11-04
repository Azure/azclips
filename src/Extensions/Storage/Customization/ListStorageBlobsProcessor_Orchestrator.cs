// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageBlobsProcessor_Orchestrator : BlobContainerWorkflowBase
{
    private static readonly (char Flag, string Dataset)[] s_includes =
        [('s', "snapshots"), ('m', "metadata"), ('c', "copy"), ('d', "deleted"), ('v', "versions"), ('t', "tags")];

    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? Include { get; set; }
    public string NumResults { get; set; } = "5000";

    public string? Prefix { get; set; }
    public string? Delimiter { get; set; }
    public IList<string>? Datasets { get; set; }
    public string? Marker { get; set; }
    public StorageBlobContinuationToken? ContinuationToken { get; set; }
    public long? ResultLimit { get; set; }
    public int? MaxCount { get; set; }
    public bool ShowNextMarker { get; set; }
    internal Func<string, bool>? NameFilter { get; set; }
    internal Func<StorageBlobEntry, CancellationToken, Task>? EntryHandler { get; set; }
    internal Action<string?>? NextMarkerHandler { get; set; }

    protected override async Task ExecuteAsync()
    {
        long? limit = NumResults == "*" ? null :
            long.TryParse(NumResults, NumberStyles.None, CultureInfo.InvariantCulture, out long count) && count > 0
                ? count : throw new ArgumentException("NumResults must be a positive 64-bit integer or *.", nameof(NumResults));
        if (Include?.Any(flag => !s_includes.Any(option => option.Flag == flag)) == true)
            throw new ArgumentException("Include supports combined c, d, m, s, t and v flags only.", nameof(Include));
        string[] includes = s_includes.Where(option => Include?.Contains(option.Flag) == true)
            .Select(option => option.Dataset).ToArray();
        bool includeTags = includes.Contains("tags", StringComparer.Ordinal);
        bool showNextMarker = ShowNextMarker;
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var container = ResolveContainer(defaultAccount: account);
        StorageBlobCliAddress.ValidateContext(account, container.Context);
        Context = container.Context;
        Profile = container.Context.Profile.Name;
        ContainerObject = container;
        Delimiter = string.IsNullOrEmpty(Delimiter) ? null : Delimiter;
        Datasets = includes.Length == 0 ? null : includes;
        ResultLimit = limit;
        var results = new List<JsonElement>();
        string? nextMarker = null;
        var previousEntryHandler = EntryHandler;
        var previousMarkerHandler = NextMarkerHandler;
        EntryHandler = (entry, token) =>
        {
            token.ThrowIfCancellationRequested();
            results.Add(StorageBlobCliOutput.ProjectEntry(entry, includeTags));
            return Task.CompletedTask;
        };
        NextMarkerHandler = marker => nextMarker = marker;
        try { await ExecuteOperationAsync(); }
        finally
        {
            EntryHandler = previousEntryHandler;
            NextMarkerHandler = previousMarkerHandler;
        }
        Token.ThrowIfCancellationRequested();
        if (showNextMarker)
            results.Add(JsonSerializer.SerializeToElement(new StorageBlobNextMarker(nextMarker)));
        else if (nextMarker is not null)
            await Publisher.Publish(Events.Warning, "Next Marker: " + nextMarker);
        Token.ThrowIfCancellationRequested();
        await WriteOutput(results.ToArray(), Token);
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var container = ResolveContainer();
        if (ResultLimit is <= 0 || ResultLimit is not null && MaxCount is not null ||
            Marker is not null && ContinuationToken is not null || Delimiter == string.Empty)
            throw new ArgumentException("Specify a positive result limit, one marker and one limit mode.");
        if (Datasets?.Any(value => value is not ("copy" or "deleted" or "metadata" or "snapshots" or "uncommittedblobs" or
            "versions" or "tags" or "immutabilitypolicy" or "legalhold" or "deletedwithversions")) == true)
            throw new ArgumentException("An unsupported blob listing include value was supplied.");
        if (ContinuationToken is { } token &&
            (token.StorageAccountName != container.Context.StorageAccountName || token.Container != container.Name ||
             token.Prefix != Prefix || token.Delimiter != Delimiter || string.IsNullOrEmpty(token.NextMarker)))
            throw new ArgumentException("The continuation token belongs to a different listing.");
        string? marker = ContinuationToken?.NextMarker ?? Marker;
        long remaining = ResultLimit ?? (MaxCount is > 0 ? MaxCount.Value : long.MaxValue);
        string? prefix = Prefix, delimiter = Delimiter;
        IList<string>? datasets = Datasets?.ToArray();
        int? timeout = Timeout;
        bool countAll = MaxCount is not null, showNextMarker = ShowNextMarker;
        var nameFilter = NameFilter;
        var entryHandler = EntryHandler;
        var markerHandler = NextMarkerHandler;
        var writeOutput = WriteOutput;
        var seenMarkers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(marker)) seenMarkers.Add(marker);
        do
        {
            Token.ThrowIfCancellationRequested();
            int pageSize = (int)Math.Min(5000, remaining);
            StorageDataPlaneProcessorBase child = delimiter is null
                ? CreateBlobProcessor<BlobProcessors.ListBlobsContainerProcessor>(container, processor =>
                {
                    processor.Url = container.Uri.AbsoluteUri; processor.Prefix = prefix; processor.Marker = marker;
                    processor.Maxresults = pageSize; processor.Include = datasets; processor.Timeout = timeout;
                })
                : CreateBlobProcessor<BlobProcessors.ListBlobHierarchySegmentContainerProcessor>(container, processor =>
                {
                    processor.Url = container.Uri.AbsoluteUri; processor.Prefix = prefix; processor.Marker = marker;
                    processor.Maxresults = pageSize; processor.Include = datasets; processor.Timeout = timeout; processor.Delimiter = delimiter;
                });
            using var response = await child.SendAsync();
            var page = await StorageBlobXml.ReadPageAsync(response.Content, container, delimiter is not null, Token);
            if (page.Entries.Count > pageSize || page.NextMarker is { } next && !seenMarkers.Add(next))
                throw new InvalidDataException("Storage returned an oversized page or a repeated continuation marker.");
            marker = page.NextMarker;
            var continuation = marker is null ? null : new StorageBlobContinuationToken(marker, container.Context.StorageAccountName, container.Name, prefix, delimiter);
            foreach (var entry in page.Entries)
            {
                Token.ThrowIfCancellationRequested();
                bool selected = nameFilter?.Invoke(entry.Name) ?? true;
                if (countAll || selected) remaining--;
                if (selected)
                {
                    var output = entry is StorageBlob blob ? blob with { ContinuationToken = continuation } : entry;
                    await (entryHandler?.Invoke(output, Token) ?? writeOutput(output, Token));
                }
            }
        } while (remaining > 0 && marker is not null);

        if (markerHandler is not null)
            markerHandler(marker);
        else if (showNextMarker)
            await writeOutput(new StorageBlobNextMarker(marker), Token);
        else if (marker is not null)
            await Publisher.Publish(Events.Warning, "Next Marker: " + marker);
    }
}
