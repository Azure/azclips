// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobUploadWorkflowBase : BlobTargetWorkflowBase
{
    public string File { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
    public string? Tier { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    protected sealed record Upload(Target Target, string SourcePath, string? Tags, string? IfNoneMatch);

    public void ValidatePowerShellUpload()
    {
        bool page = this is UploadStoragePageBlobProcessor;
        _ = ResolveUpload(page ? 4 * 1024 * 1024 : StorageWireFormat.MaximumChunkBytes,
            page ? ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"] :
            this is UploadStorageAppendBlobProcessor ? [] : ["Hot", "Cool", "Cold", "Archive"]);
        long length = new FileInfo(File).Length;
        if (page && (ChunkSize % 512 != 0 || length % 512 != 0 || length > UploadStoragePageBlobProcessor.MaximumBlobBytes))
            throw new ArgumentException("Page Blob source length and chunks must be aligned to 512 bytes, with source at most 8 TiB.");
        if (!page && length != 0 && (length - 1) / ChunkSize + 1 > 50000)
            throw new ArgumentException("The source requires more than 50,000 blocks; increase the chunk size.");
    }

    protected Upload ResolveUpload(int maximumChunkBytes, IReadOnlyCollection<string> allowedTiers, bool appendToExisting = false)
    {
        if (string.IsNullOrWhiteSpace(File))
            throw new ArgumentException("Specify a source file.");
        string sourcePath = Path.GetFullPath(File);
        var target = ResolveTarget(Path.GetFileName(sourcePath));
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Uploads must target a base blob, not a snapshot or version.");
        if (Concurrency is < 1 or > 4 || ChunkSize < 1 || ChunkSize > maximumChunkBytes)
            throw new ArgumentException($"Use 1-4 workers and chunks of 1 through {maximumChunkBytes} bytes.");
        if (ContentMd5 is not null && ContentMd5.Length != 16)
            throw new ArgumentException("Content-MD5 must contain exactly 16 bytes.");
        if (Tier is not null && !allowedTiers.Contains(Tier, StringComparer.Ordinal))
            throw new ArgumentException("Specify a tier supported by the requested blob type.");
        foreach (string? value in new[] { ContentType, ContentEncoding, ContentLanguage, ContentDisposition, CacheControl })
            if (value?.Any(character => character < 0x20 || character > 0x7e) == true)
                throw new ArgumentException("Content settings must contain printable ASCII header values.");
        using (var validation = new HttpRequestMessage())
            AddMetadataHeaders(validation, Metadata);
        string? tags = EncodeTags(Tags);
        if (!appendToExisting && !Overwrite && (IfMatch is not null || IfNoneMatch is not null && IfNoneMatch != "*"))
            throw new ArgumentException("ETag overwrite conditions require Overwrite; otherwise the upload requires an absent blob.");
        return new Upload(target, sourcePath, tags, appendToExisting || Overwrite ? IfNoneMatch : "*");
    }

    protected async Task FinishUploadAsync(Target target, long length, string etag, string blobType, bool includeRequestedTags = true)
    {
        // Do not return a concurrent replacement or reuse the original write-only conditions.
        var properties = CreateBlobProcessor<BlobProcessors.GetPropertiesBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId; processor.IfMatch = etag;
        });
        using var response = await properties.SendAsync();
        var result = StorageBlob.FromHeaders(target.Container, target.Name,
            StorageResourceProperties.FromResponse(response), null, null);
        if (result.Properties.ContentLength != length || result.Properties.BlobType != blobType)
            throw new InvalidDataException("Storage returned an unexpected uploaded blob length or type.");
        if (includeRequestedTags && Tags is not null)
            result = result with { Tags = new ReadOnlyDictionary<string, string>(Tags.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)) };
        Token.ThrowIfCancellationRequested();
        await PublishUploadProgressAsync(length, length, completed: true);
        await WriteOutput(result, Token);
    }

    protected Task PublishUploadProgressAsync(long transferred, long total, bool completed) =>
        SuppressProgress ? Task.CompletedTask : Publisher.Publish(Events.Progress, Token, new EventData
        {
            Id = Events.Progress,
            Message = completed ? "Blob upload completed." : $"Transferred {transferred} of {total} bytes.",
            Value = completed ? 100 : total == 0 ? 0 : Math.Min(99, transferred * 100.0 / total),
            Cancel = Cancel
        });

    protected static Task<byte[]> ReadChunkAsync(FileStream source, long offset, int length, CancellationToken token) =>
        StorageWireFormat.ReadFileChunkAsync(source, offset, length, token);

    protected static string RequiredEtag(HttpResponseMessage response) =>
        response.Headers.ETag?.ToString() ?? throw new InvalidDataException("Storage did not return the uploaded blob's ETag.");

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
}
