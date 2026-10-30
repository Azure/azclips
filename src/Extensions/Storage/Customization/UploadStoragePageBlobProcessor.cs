// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStoragePageBlobProcessor : BlobUploadWorkflowBase
{
    internal const int MaximumPageBytes = 4 * 1024 * 1024;
    internal const long MaximumBlobBytes = 8L * 1024 * 1024 * 1024 * 1024;

    public UploadStoragePageBlobProcessor() => ChunkSize = MaximumPageBytes;

    protected override async Task ExecuteAsync()
    {
        var (target, sourcePath, tags, noneMatch) = ResolveUpload(MaximumPageBytes,
            ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"]);
        if (ChunkSize % 512 != 0)
            throw new ArgumentException("Page Blob chunks must be aligned to 512 bytes.");
        if (!ShouldProcess(target.Url, "Upload Page Blob")) return;

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        if (length % 512 != 0 || length > MaximumBlobBytes)
            throw new ArgumentException("Page Blob source length must be a multiple of 512 bytes and at most 8 TiB.");
        var create = CreateBlobProcessor<BlobProcessors.CreatePageBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.BlobType = "PageBlob";
            processor.Size = length; processor.ContentLength = 0;
            processor.BlobContentType = ContentType; processor.BlobContentEncoding = ContentEncoding;
            processor.BlobContentLanguage = ContentLanguage; processor.BlobContentDisposition = ContentDisposition;
            processor.BlobCacheControl = CacheControl; processor.BlobContentMd5 = ContentMd5;
            processor.Metadata = Metadata; processor.BlobTagsString = tags; processor.Tier = Tier;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = noneMatch;
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });
        string etag;
        using (var response = await create.SendAsync()) etag = RequiredEtag(response);
        long transferred = 0;
        using var writeGate = new SemaphoreSlim(1);
        bool writeFailed = false;
        await Parallel.ForEachAsync(PageOffsets(length, ChunkSize),
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = Token },
            async (offset, cancellationToken) =>
            {
                byte[] bytes = await ReadChunkAsync(source, offset, (int)Math.Min(ChunkSize, length - offset), cancellationToken);
                await writeGate.WaitAsync(cancellationToken);
                bool succeeded = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (writeFailed) throw new IOException("A previous Page Blob range failed; upload is incomplete.");
                    if (!bytes.AsSpan().ContainsAnyExcept((byte)0))
                    {
                        transferred += bytes.Length;
                        await PublishUploadProgressAsync(transferred, length, completed: false);
                        succeeded = true;
                        return;
                    }
                    // Range writes change the ETag. Serialize mutations while allowing bounded read-ahead.
                    var page = CreateBlobProcessor<BlobProcessors.UploadPagesPageBlobProcessor>(target.Container, processor =>
                    {
                        processor.Url = target.Url; processor.Timeout = Timeout; processor.Body = bytes;
                        processor.ContentLength = bytes.Length; processor.PageWrite = "update";
                        processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{offset + bytes.Length - 1}");
                        processor.TransactionalContentMd5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
                        processor.LeaseId = LeaseId; processor.IfMatch = etag;
                    });
                    using var response = await page.SendAsync();
                    etag = RequiredEtag(response);
                    transferred += bytes.Length;
                    await PublishUploadProgressAsync(transferred, length, completed: false);
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
        await FinishUploadAsync(target, length, etag, "PageBlob");
    }

    private static IEnumerable<long> PageOffsets(long length, int chunkSize)
    {
        for (long offset = 0; offset < length; offset += chunkSize) yield return offset;
    }
}
