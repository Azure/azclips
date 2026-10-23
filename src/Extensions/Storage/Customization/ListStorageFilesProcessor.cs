// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class ListStorageFilesProcessor : FileShareWorkflowBase
{
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public StorageFileContinuationToken? ContinuationToken { get; set; }
    public long? NumResults { get; set; }
    public bool ShowNextMarker { get; set; }
    public bool ExcludeExtendedInfo { get; set; }
    internal bool DirectoriesOnly { get; set; }

    protected override async Task ExecuteAsync()
    {
        var directory = ResolveDirectory(Path);
        if (NumResults is <= 0 || Marker is not null && ContinuationToken is not null)
            throw new ArgumentException("Specify a positive result limit and one continuation marker.");
        if (ContinuationToken is { } token &&
            (token.StorageAccountName != directory.Context.StorageAccountName || token.Share != directory.Share.Name ||
             token.Path != directory.Path || token.ShareSnapshot != directory.Share.ShareSnapshot || token.Prefix != Prefix ||
             string.IsNullOrEmpty(token.NextMarker)))
            throw new ArgumentException("The continuation token belongs to a different Files listing.");
        string? marker = ContinuationToken?.NextMarker ?? Marker;
        var seenMarkers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(marker)) seenMarkers.Add(marker);
        long remaining = NumResults ?? long.MaxValue;
        do
        {
            Token.ThrowIfCancellationRequested();
            int pageSize = (int)Math.Min(5000, remaining);
            var child = CreateFileProcessor<FileProcessors.ListFilesAndDirectoriesSegmentDirectoryProcessor>(directory, processor =>
            {
                processor.Url = directory.Uri.AbsoluteUri; processor.Prefix = Prefix; processor.Marker = marker;
                processor.Maxresults = pageSize; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
                processor.IncludeExtendedInfo = !ExcludeExtendedInfo;
                processor.Include = ExcludeExtendedInfo ? null : ["Timestamps", "Etag", "Attributes", "PermissionKey"];
            });
            using var response = await child.SendAsync();
            var page = await StorageFileXml.ReadPageAsync(response.Content, directory, Token);
            if (page.Entries.Count > pageSize || page.NextMarker is { } next && !seenMarkers.Add(next))
                throw new InvalidDataException("Storage returned an oversized Files page or a repeated continuation marker.");
            marker = page.NextMarker;
            var continuation = marker is null ? null : new StorageFileContinuationToken(marker,
                directory.Context.StorageAccountName, directory.Share.Name, directory.Path, directory.Share.ShareSnapshot, Prefix);
            foreach (var entry in page.Entries)
            {
                Token.ThrowIfCancellationRequested();
                remaining--;
                if (!DirectoriesOnly || entry is StorageFileDirectory)
                    await WriteOutput(entry with { ContinuationToken = continuation }, Token);
            }
        } while (remaining > 0 && marker is not null);
        if (ShowNextMarker) await WriteOutput(new StorageFileNextMarker(marker), Token);
        else if (marker is not null) await Publisher.Publish(Events.Warning, "Next Marker: " + marker);
    }
}
