// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions.Runtime;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public interface IFileListingProcessor : IFileCliOptions, Core.Abstractions.IProcessor
{
    string NumResults { get; set; }
    string? Prefix { get; set; }
    string? Marker { get; set; }
    StorageFileContinuationToken? ContinuationToken { get; set; }
    long? ResultLimit { get; set; }
    bool ShowNextMarker { get; set; }
    bool ExcludeExtendedInfo { get; set; }
}

internal static class FileListingOperations
{
    internal static async Task ExecuteCliAsync<T>(T owner, bool directoriesOnly)
        where T : StorageDataPlaneProcessorBase, IFileListingProcessor
    {
        owner.ResultLimit = owner.NumResults == "*" ? null :
            long.TryParse(owner.NumResults, NumberStyles.None, CultureInfo.InvariantCulture, out long count) && count > 0
                ? count : throw new ArgumentException("NumResults must be a positive 64-bit integer or *.");
        FileCliOperations.ResolveDirectory(owner);
        var results = new List<JsonElement>();
        await ExecuteAsync(owner, directoriesOnly, (value, token) =>
        {
            token.ThrowIfCancellationRequested();
            results.Add(value is StorageFileEntry entry ? StorageFileFrontend.Project(entry) :
                JsonSerializer.SerializeToElement((StorageFileNextMarker)value!));
            return Task.CompletedTask;
        });
        owner.Token.ThrowIfCancellationRequested();
        await owner.WriteOutput(results.ToArray(), owner.Token);
    }

    internal static async Task ExecuteAsync<T>(T owner, bool directoriesOnly, OutputStreamDelegate? output = null)
        where T : StorageDataPlaneProcessorBase, IFileListingProcessor
    {
        var writeOutput = output ?? owner.WriteOutput;
        var directory = FileShareOperations.Resolve(owner, owner, owner.Path);
        if (owner.ResultLimit is <= 0 || owner.Marker is not null && owner.ContinuationToken is not null)
            throw new ArgumentException("Specify a positive result limit and one continuation marker.");
        if (owner.ContinuationToken is { } token &&
            (token.StorageAccountName != directory.Context.StorageAccountName || token.Share != directory.Share.Name ||
             token.Path != directory.Path || token.ShareSnapshot != directory.Share.ShareSnapshot || token.Prefix != owner.Prefix ||
             string.IsNullOrEmpty(token.NextMarker)))
            throw new ArgumentException("The continuation token belongs to a different Files listing.");
        string? marker = owner.ContinuationToken?.NextMarker ?? owner.Marker;
        var seenMarkers = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(marker)) seenMarkers.Add(marker);
        long remaining = owner.ResultLimit ?? long.MaxValue;
        do
        {
            owner.Token.ThrowIfCancellationRequested();
            int pageSize = (int)Math.Min(5000, remaining);
            var child = FileShareOperations.CreateProcessor<FileProcessors.ListFilesAndDirectoriesSegmentDirectoryProcessor>(owner, directory, processor =>
            {
                processor.Url = directory.Uri.AbsoluteUri; processor.Prefix = owner.Prefix; processor.Marker = marker;
                processor.Maxresults = pageSize; processor.Timeout = owner.Timeout; processor.AllowTrailingDot = owner.AllowTrailingDot;
                processor.IncludeExtendedInfo = !owner.ExcludeExtendedInfo;
                processor.Include = owner.ExcludeExtendedInfo ? null : ["Timestamps", "Etag", "Attributes", "PermissionKey"];
            });
            using var response = await child.SendAsync();
            var page = await StorageFileXml.ReadPageAsync(response.Content, directory, owner.Token);
            if (page.Entries.Count > pageSize || page.NextMarker is { } next && !seenMarkers.Add(next))
                throw new InvalidDataException("Storage returned an oversized Files page or a repeated continuation marker.");
            marker = page.NextMarker;
            var continuation = marker is null ? null : new StorageFileContinuationToken(marker,
                directory.Context.StorageAccountName, directory.Share.Name, directory.Path, directory.Share.ShareSnapshot, owner.Prefix);
            foreach (var entry in page.Entries)
            {
                owner.Token.ThrowIfCancellationRequested();
                remaining--;
                if (!directoriesOnly || entry is StorageFileDirectory)
                    await writeOutput(entry with { ContinuationToken = continuation }, owner.Token);
            }
        } while (remaining > 0 && marker is not null);
        if (owner.ShowNextMarker) await writeOutput(new StorageFileNextMarker(marker), owner.Token);
        else if (marker is not null) await owner.Publisher.Publish(Events.Warning, "Next Marker: " + marker);
    }
}
