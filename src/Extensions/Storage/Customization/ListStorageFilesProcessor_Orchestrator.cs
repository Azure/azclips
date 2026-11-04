// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public class ListStorageFilesProcessor_Orchestrator : FileCliOrchestratorBase
{
    public string NumResults { get; set; } = "*";
    protected virtual bool DirectoriesOnly => false;

    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public StorageFileContinuationToken? ContinuationToken { get; set; }
    public long? ResultLimit { get; set; }
    public bool ShowNextMarker { get; set; }
    public bool ExcludeExtendedInfo { get; set; }
    internal bool FilterDirectoriesOnly { get; set; }

    protected override async Task ExecuteAsync()
    {
        ResultLimit = NumResults == "*" ? null :
            long.TryParse(NumResults, NumberStyles.None, CultureInfo.InvariantCulture, out long count) && count > 0
                ? count : throw new ArgumentException("NumResults must be a positive 64-bit integer or *.");
        ResolveCliDirectory();
        FilterDirectoriesOnly = DirectoriesOnly;
        var results = new List<JsonElement>();
        await ExecuteOperationAsync((value, token) =>
        {
            token.ThrowIfCancellationRequested();
            results.Add(value is StorageFileEntry entry ? StorageFileFrontend.Project(entry) :
                JsonSerializer.SerializeToElement((StorageFileNextMarker)value!));
            return Task.CompletedTask;
        });
        Token.ThrowIfCancellationRequested();
        await WriteOutput(results.ToArray(), Token);
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(() => ExecuteOperationAsync());

    private async Task ExecuteOperationAsync(OutputStreamDelegate? output = null)
    {
        var writeOutput = output ?? WriteOutput;
        var directory = ResolveDirectory(Path);
        if (ResultLimit is <= 0 || Marker is not null && ContinuationToken is not null)
            throw new ArgumentException("Specify a positive result limit and one continuation marker.");
        if (ContinuationToken is { } token &&
            (token.StorageAccountName != directory.Context.StorageAccountName || token.Share != directory.Share.Name ||
             token.Path != directory.Path || token.ShareSnapshot != directory.Share.ShareSnapshot || token.Prefix != Prefix ||
             string.IsNullOrEmpty(token.NextMarker)))
            throw new ArgumentException("The continuation token belongs to a different Files listing.");
        string? marker = ContinuationToken?.NextMarker ?? Marker;
        var seenMarkers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(marker)) seenMarkers.Add(marker);
        long remaining = ResultLimit ?? long.MaxValue;
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
                if (!FilterDirectoriesOnly || entry is StorageFileDirectory)
                    await writeOutput(entry with { ContinuationToken = continuation }, Token);
            }
        } while (remaining > 0 && marker is not null);
        if (ShowNextMarker) await writeOutput(new StorageFileNextMarker(marker), Token);
        else if (marker is not null) await Publisher.Publish(Events.Warning, "Next Marker: " + marker);
    }
}
