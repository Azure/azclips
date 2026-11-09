// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Core.Abstractions.Runtime;
using Core.Abstractions;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class BlobUploadOperations
{

    internal static void ValidatePowerShellUpload(StorageDataPlaneProcessorBase owner, IBlobUploadProcessor options)
    {
        bool page = options is UploadStoragePageBlobWorkflow;
        _ = ResolveUpload(options, owner, page ? 4 * 1024 * 1024 : StorageWireFormat.MaximumChunkBytes,
            page ? ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"] :
            options is UploadStorageAppendBlobWorkflow ? [] : ["Hot", "Cool", "Cold", "Archive"]);
        long length = new FileInfo(options.File).Length;
        if (page && (options.ChunkSize % 512 != 0 || length % 512 != 0 || length > UploadStoragePageBlobWorkflow.MaximumBlobBytes))
            throw new ArgumentException("Page Blob source length and chunks must be aligned to 512 bytes, with source at most 8 TiB.");
        if (options is UploadStorageBlockBlobWorkflow)
            _ = UploadStorageBlockBlobWorkflow.BlockSize(length, options.ChunkSize);
        else if (!page && length != 0 && (length - 1) / options.ChunkSize + 1 > 50000)
            throw new ArgumentException("The source requires more than 50,000 blocks; increase the chunk size.");
    }

    internal static Upload ResolveUpload(IBlobUploadProcessor options, StorageDataPlaneProcessorBase owner, int maximumChunkBytes, IReadOnlyCollection<string> allowedTiers, bool appendToExisting = false)
    {
        if (string.IsNullOrWhiteSpace(options.File))
            throw new ArgumentException("Specify a source file.");
        string sourcePath = Path.GetFullPath(options.File);
        var target = BlobTargetOperations.Resolve(options, owner, Path.GetFileName(sourcePath));
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Uploads must target a base blob, not a snapshot or version.");
        if (options.Concurrency is < 1 or > 4 || options.ChunkSize < 1 || options.ChunkSize > maximumChunkBytes)
            throw new ArgumentException($"Use 1-4 workers and chunks of 1 through {maximumChunkBytes} bytes.");
        if (options.ContentMd5 is not null && options.ContentMd5.Length != 16)
            throw new ArgumentException("Content-MD5 must contain exactly 16 bytes.");
        if (options.Tier is not null && !allowedTiers.Contains(options.Tier, StringComparer.Ordinal))
            throw new ArgumentException("Specify a tier supported by the requested blob type.");
        foreach (string? value in new[] { options.ContentType, options.ContentEncoding, options.ContentLanguage, options.ContentDisposition, options.CacheControl })
            if (value?.Any(character => character < 0x20 || character > 0x7e) == true)
                throw new ArgumentException("Content settings must contain printable ASCII header values.");
        using (var validation = new HttpRequestMessage())
            StorageDataPlaneProcessorBase.AddMetadataHeaders(validation, options.Metadata);
        string? tags = EncodeTags(options.Tags);
        if (!appendToExisting && !options.Overwrite && (options.IfMatch is not null || options.IfNoneMatch is not null && options.IfNoneMatch != "*"))
            throw new ArgumentException("ETag overwrite conditions require Overwrite; otherwise the upload requires an absent blob.");
        return new Upload(target, sourcePath, tags, appendToExisting || options.Overwrite ? options.IfNoneMatch : "*");
    }

    internal static async Task FinishUploadAsync<T>(T owner, BlobTarget target, long length, string etag, string blobType, string? versionId,
        bool includeRequestedTags = true)
        where T : StorageDataPlaneProcessorBase, IBlobUploadProcessor
    {
        // Do not return a concurrent replacement or reuse the original write-only conditions.
        var properties = BlobContainerOperations.CreateProcessor<BlobProcessors.GetPropertiesBlobProcessor>(owner, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = owner.Timeout; processor.LeaseId = owner.LeaseId; processor.IfMatch = etag;
        });
        using var response = await properties.SendAsync();
        var result = StorageBlob.FromHeaders(target.Container, target.Name,
            StorageResourceProperties.FromResponse(response), null, versionId);
        if (result.Properties.ContentLength != length || result.Properties.BlobType != blobType)
            throw new InvalidDataException("Storage returned an unexpected uploaded blob length or type.");
        if (includeRequestedTags && owner.Tags is not null)
            result = result with { Tags = new ReadOnlyDictionary<string, string>(owner.Tags.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)) };
        owner.Token.ThrowIfCancellationRequested();
        await PublishUploadProgressAsync(owner, length, length, completed: true);
        await owner.WriteOutput(result, owner.Token);
    }

    internal static Task PublishUploadProgressAsync(StorageDataPlaneProcessorBase owner, long transferred, long total, bool completed) =>
        owner.SuppressProgress ? Task.CompletedTask : owner.Publisher.Publish(Events.Progress, owner.Token, new EventData
        {
            Id = Events.Progress,
            Message = completed ? "Blob upload completed." : $"Transferred {transferred} of {total} bytes.",
            Value = completed ? 100 : total == 0 ? 0 : Math.Min(99, transferred * 100.0 / total),
            Cancel = owner.Cancel
        });

    internal static string RequiredEtag(HttpResponseMessage response) =>
        response.Headers.ETag?.ToString() ?? throw new InvalidDataException("Storage did not return the uploaded blob's ETag.");

    internal static string? ReadVersionId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-ms-version-id", out var values)) return null;
        string[] items = values.Take(2).ToArray();
        if (items.Length != 1 || string.IsNullOrWhiteSpace(items[0]))
            throw new InvalidDataException("Storage returned an invalid uploaded blob version ID.");
        return items[0];
    }

    internal static string? EncodeTags(IReadOnlyDictionary<string, string>? tags)
    {
        if (tags is null) return null;
        if (tags.Count > 10) throw new ArgumentException("A blob supports at most ten tags.");
        foreach (var (key, value) in tags)
        {
            if (key.Length is < 1 or > 128 || value is null || value.Length > 256 ||
                (key + value).Any(character => !char.IsAsciiLetterOrDigit(character) && !" +-./:=_".Contains(character)))
                throw new ArgumentException("Blob tags require valid key/value lengths and supported tag characters.");
        }
        return string.Join("&", tags.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
    }
    internal sealed record Upload(BlobTarget Target, string SourcePath, string? Tags, string? IfNoneMatch);
}
