// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStoragePageBlobWorkflow : StorageDataPlaneProcessorBase, IBlobUploadProcessor
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    public string? Blob { get; set; }
    public StorageBlob? InputObject { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }
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
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    internal const int MaximumPageBytes = 4 * 1024 * 1024;
    internal const long MaximumBlobBytes = 8L * 1024 * 1024 * 1024 * 1024;

    public UploadStoragePageBlobWorkflow() => ChunkSize = MaximumPageBytes;

    private async Task ExecuteAsync()
    {
        var (target, sourcePath, tags, noneMatch) = BlobUploadOperations.ResolveUpload(this, this, MaximumPageBytes,
            ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"]);
        if (ChunkSize % 512 != 0)
            throw new ArgumentException("Page Blob chunks must be aligned to 512 bytes.");
        if (!ShouldProcess(target.Url, "Upload Page Blob")) return;

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        if (length % 512 != 0 || length > MaximumBlobBytes)
            throw new ArgumentException("Page Blob source length must be a multiple of 512 bytes and at most 8 TiB.");
        var create = BlobContainerOperations.CreateProcessor<BlobProcessors.CreatePageBlobProcessor>(this, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.BlobType = "PageBlob";
            processor.Size = length; processor.ContentLength = 0;
            processor.BlobContentType = ContentType; processor.BlobContentEncoding = ContentEncoding;
            processor.BlobContentLanguage = ContentLanguage; processor.BlobContentDisposition = ContentDisposition;
            processor.BlobCacheControl = CacheControl; processor.BlobContentMd5 = ContentMd5;
            processor.Metadata = Metadata; processor.BlobTagsString = tags; processor.Tier = Tier;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = noneMatch;
            processor.IfModifiedSince = BlobTargetOperations.Date(IfModifiedSince); processor.IfUnmodifiedSince = BlobTargetOperations.Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });
        string etag;
        string? versionId;
        using (var response = await create.SendAsync())
        {
            etag = BlobUploadOperations.RequiredEtag(response);
            versionId = BlobUploadOperations.ReadVersionId(response);
        }
        long transferred = 0;
        using var writeGate = new SemaphoreSlim(1);
        bool writeFailed = false;
        await Parallel.ForEachAsync(PageOffsets(length, ChunkSize),
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = Token },
            async (offset, cancellationToken) =>
            {
                byte[] bytes = await StorageWireFormat.ReadFileChunkAsync(source, offset, (int)Math.Min(ChunkSize, length - offset), cancellationToken);
                await writeGate.WaitAsync(cancellationToken);
                bool succeeded = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (writeFailed) throw new IOException("A previous Page Blob range failed; upload is incomplete.");
                    if (!bytes.AsSpan().ContainsAnyExcept((byte)0))
                    {
                        transferred += bytes.Length;
                        await BlobUploadOperations.PublishUploadProgressAsync(this, transferred, length, completed: false);
                        succeeded = true;
                        return;
                    }
                    // Range writes change the ETag. Serialize mutations while allowing bounded read-ahead.
                    var page = BlobContainerOperations.CreateProcessor<BlobProcessors.UploadPagesPageBlobProcessor>(this, target.Container, processor =>
                    {
                        processor.Url = target.Url; processor.Timeout = Timeout; processor.Body = bytes;
                        processor.ContentLength = bytes.Length; processor.PageWrite = "update";
                        processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{offset + bytes.Length - 1}");
                        processor.TransactionalContentMd5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
                        processor.LeaseId = LeaseId; processor.IfMatch = etag;
                    });
                    using var response = await page.SendAsync();
                    etag = BlobUploadOperations.RequiredEtag(response);
                    versionId = BlobUploadOperations.ReadVersionId(response);
                    transferred += bytes.Length;
                    await BlobUploadOperations.PublishUploadProgressAsync(this, transferred, length, completed: false);
                    succeeded = true;
                }
                finally
                {
                    if (!succeeded) writeFailed = true;
                    writeGate.Release();
                }
            });
        Token.ThrowIfCancellationRequested();
        if (source.Length != length) throw new IOException("The source file length changed during upload.");
        await BlobUploadOperations.FinishUploadAsync(this, target, length, etag, "PageBlob", versionId);
    }

    private static IEnumerable<long> PageOffsets(long length, int chunkSize)
    {
        for (long offset = 0; offset < length; offset += chunkSize) yield return offset;
    }
}
