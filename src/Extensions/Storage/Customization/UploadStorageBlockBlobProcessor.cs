// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStorageBlockBlobProcessor : BlobUploadWorkflowBase
{
    protected override async Task ExecuteAsync()
    {
        var upload = ResolveUpload(StorageWireFormat.MaximumChunkBytes, ["Hot", "Cool", "Cold", "Archive"]);
        var (target, sourcePath, tags, noneMatch) = upload;
        if (!ShouldProcess(target.Url, "Upload Block Blob")) return;

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        long count = length == 0 ? 0 : (length - 1) / ChunkSize + 1;
        if (count > 50000)
            throw new ArgumentException("The source requires more than 50,000 blocks; increase the chunk size (up to 8 MiB).");
        string etag;
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
                    byte[] buffer = new byte[(int)Math.Min(ChunkSize, length - (long)worker * ChunkSize)];
                    for (int index = worker; index < ids.Length; index += Concurrency)
                    {
                        long offset = (long)index * ChunkSize;
                        int size = (int)Math.Min(ChunkSize, length - offset);
                        byte[] bytes = size == buffer.Length ? buffer : new byte[size];
                        await StorageWireFormat.ReadFileChunkAsync(source, offset, bytes.AsMemory(), cancellationToken);
                        var stage = CreateBlobProcessor<BlobProcessors.StageBlockBlockBlobProcessor>(target.Container, processor =>
                        {
                            processor.Url = target.Url; processor.Timeout = Timeout; processor.BlockId = ids[index];
                            processor.Body = bytes; processor.ContentLength = bytes.Length; processor.LeaseId = LeaseId;
                            processor.TransactionalContentMd5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
                        });
                        cancellationToken.ThrowIfCancellationRequested();
                        using var response = await stage.SendAsync();
                        await progressLock.WaitAsync(cancellationToken);
                        try
                        {
                            transferred += bytes.Length;
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
        }
        await FinishUploadAsync(target, length, etag, "BlockBlob");
    }
}
