// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStorageBlockBlobWorkflow : BlobUploadWorkflowBase
{
    protected override async Task ExecuteAsync()
    {
        var upload = ResolveUpload(StorageWireFormat.MaximumChunkBytes, ["Hot", "Cool", "Cold", "Archive"]);
        var (target, sourcePath, tags, noneMatch) = upload;
        if (!ShouldProcess(target.Url, "Upload Block Blob")) return;

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        long blockSize = BlockSize(length, ChunkSize);
        long count = length == 0 ? 0 : (length - 1) / blockSize + 1;
        string etag;
        string? versionId;
        if (length <= ChunkSize)
        {
            byte[] bytes = await ReadChunkAsync(source, 0, (int)length, Token);
            var child = CreateBlobProcessor<BlobProcessors.UploadBlockBlobProcessor>(target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.BlobType = "BlockBlob";
                processor.Body = bytes; processor.ContentLength = bytes.Length;
                processor.TransactionalContentMd5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
                processor.BlobContentType = ContentType; processor.BlobContentEncoding = ContentEncoding;
                processor.BlobContentLanguage = ContentLanguage; processor.BlobContentDisposition = ContentDisposition;
                processor.BlobCacheControl = CacheControl; processor.BlobContentMd5 = ContentMd5;
                processor.Metadata = Metadata; processor.BlobTagsString = tags; processor.Tier = Tier;
                processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = noneMatch;
                processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
                processor.IfTags = IfTags;
            });
            using var response = await child.SendAsync();
            etag = RequiredEtag(response);
            versionId = ReadVersionId(response);
        }
        else
        {
            byte[] uploadId = Guid.NewGuid().ToByteArray();
            var ids = new byte[(int)count][];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = new byte[24];
                uploadId.CopyTo(ids[index], 0);
                BinaryPrimitives.WriteInt64BigEndian(ids[index].AsSpan(16), index);
            }
            long transferred = 0;
            using var progressLock = new SemaphoreSlim(1);
            await Parallel.ForEachAsync(Enumerable.Range(0, (int)Math.Min(Concurrency, count)),
                new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = Token },
                async (worker, cancellationToken) =>
                {
                    for (int index = worker; index < ids.Length; index += Concurrency)
                    {
                        long offset = index * blockSize;
                        var range = new StorageFileRange(source, offset, Math.Min(blockSize, length - offset));
                        byte[]? md5 = ValidateContent ? await StorageWireFormat.ContentMd5Async(range, cancellationToken) : null;
                        var stage = CreateBlobProcessor<BlobProcessors.StageBlockBlockBlobProcessor>(target.Container, processor =>
                        {
                            processor.Url = target.Url; processor.Timeout = Timeout; processor.BlockId = ids[index];
                            processor.BodyRange = range; processor.ContentLength = range.Length; processor.LeaseId = LeaseId;
                            processor.TransactionalContentMd5 = md5;
                        });
                        cancellationToken.ThrowIfCancellationRequested();
                        using var response = await stage.SendAsync();
                        await progressLock.WaitAsync(cancellationToken);
                        try
                        {
                            transferred += range.Length;
                            await PublishUploadProgressAsync(transferred, length, completed: false);
                        }
                        finally { progressLock.Release(); }
                    }
                });
            Token.ThrowIfCancellationRequested();
            if (source.Length != length) throw new IOException("The source file length changed during upload.");
            var commit = CreateBlobProcessor<BlobProcessors.CommitBlockListBlockBlobProcessor>(target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.Uncommitted = ids;
                processor.BlobContentType = ContentType; processor.BlobContentEncoding = ContentEncoding;
                processor.BlobContentLanguage = ContentLanguage; processor.BlobContentDisposition = ContentDisposition;
                processor.BlobCacheControl = CacheControl; processor.BlobContentMd5 = ContentMd5;
                processor.Metadata = Metadata; processor.BlobTagsString = tags; processor.Tier = Tier;
                processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = noneMatch;
                processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
                processor.IfTags = IfTags;
            });
            using var response = await commit.SendAsync();
            etag = RequiredEtag(response);
            versionId = ReadVersionId(response);
        }
        await FinishUploadAsync(target, length, etag, "BlockBlob", versionId);
    }

    internal const int MaximumBlocks = 50000;

    /// <summary>
    /// The block size for a source length: the chunk size, raised to 100 MiB above 50,000 blocks of 4 MiB and to
    /// 4000 MiB above 50,000 blocks of 100 MiB (azure-cli _adjust_block_blob_size), and to the next of those sizes
    /// while the chunk size would need more than 50,000 blocks.
    /// </summary>
    internal static long BlockSize(long length, int chunkSize)
    {
        long size = chunkSize;
        if (length > MaximumBlocks * 4L * MiB) size = Math.Max(size, 100 * MiB);
        if (length > MaximumBlocks * 100L * MiB) size = StorageWireFormat.MaximumBlockBytes;
        if (length > MaximumBlocks * size) size = Math.Max(size, 100 * MiB);
        if (length > MaximumBlocks * size) size = StorageWireFormat.MaximumBlockBytes;
        if (length > MaximumBlocks * size)
            throw new ArgumentException("The source exceeds the Block Blob limit of 50,000 blocks of 4000 MiB.");
        return size;
    }

    private const long MiB = 1024 * 1024;
}
