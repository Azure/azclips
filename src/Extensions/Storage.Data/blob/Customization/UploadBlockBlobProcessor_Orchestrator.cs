// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Text;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Uploads a local file as a block blob. Up to <see cref="MaxSinglePutSize"/> it is one Put Blob
    /// (<see cref="UploadBlockBlobProcessor"/>); above that, <see cref="BlockSize"/> blocks are staged
    /// (<see cref="StageBlockBlockBlobProcessor"/>) and committed (<see cref="CommitBlockListBlockBlobProcessor"/>).
    /// An existing blob is never overwritten: the write that creates the blob sends <c>If-None-Match: *</c>.
    /// </summary>
    /// <remarks>
    /// The service sets only upper limits (5000 MiB per Put Blob, 4000 MiB per block, 50,000 blocks); the switch point
    /// and block size are client tuning. 256 MiB matches Azure CLI and bounds memory, since request bodies are byte arrays.
    /// </remarks>
    public class UploadBlockBlobProcessor_Orchestrator : ProcessorBase<object, BlobUploadResult>, ISasAuthorized
    {
        internal const int MaxSinglePutSize = 256 * 1024 * 1024;
        internal const int BlockSize = 256 * 1024 * 1024;
        private const int MaxBlockCount = 50_000;
        private const string BlockBlob = "BlockBlob";
        private const string NoOverwrite = "*";

        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        /// <summary>Path of the local file to upload.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>Tags as <c>key=value</c> pairs.</summary>
        public string[]? Tags { get; set; }

        public string? BlobContentType { get; set; }
        public string? BlobContentEncoding { get; set; }
        public string? BlobContentLanguage { get; set; }
        public string? BlobContentDisposition { get; set; }
        public string? BlobCacheControl { get; set; }
        public string? Tier { get; set; }
        public string? EncryptionScope { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = BlobWireFormat.ParsePairs(Metadata, nameof(Metadata));
                Dictionary<string, string> tags = BlobWireFormat.ParsePairs(Tags, nameof(Tags));

                using var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using HttpResponseMessage response = file.Length <= MaxSinglePutSize
                    ? await PutBlobAsync(file, metadata, tags)
                    : await PutBlocksAsync(file, metadata, tags);
                await WriteOutput(BlobUploadResult.From(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private async Task<HttpResponseMessage> PutBlobAsync(FileStream file, Dictionary<string, string> metadata, Dictionary<string, string> tags)
        {
            byte[] data = new byte[file.Length];
            file.ReadExactly(data);
            return await CreateExtendedProcessor<PutBlobRequest>(p =>
            {
                p.Url = Endpoint;
                p.BlobType = BlockBlob;
                p.ContentLength = data.Length;
                p.Body = data;
                p.IfNoneMatch = NoOverwrite;
                p.MetadataPairs = metadata;
                p.BlobTagsString = BlobWireFormat.FormatTags(tags);
                p.BlobContentType = BlobContentType;
                p.BlobContentEncoding = BlobContentEncoding;
                p.BlobContentLanguage = BlobContentLanguage;
                p.BlobContentDisposition = BlobContentDisposition;
                p.BlobCacheControl = BlobCacheControl;
                p.Tier = Tier;
                p.EncryptionScope = EncryptionScope;
                p.Timeout = Timeout;
                p.ClientRequestId = ClientRequestId;
            }).SendAsync();
        }

        private async Task<HttpResponseMessage> PutBlocksAsync(FileStream file, Dictionary<string, string> metadata, Dictionary<string, string> tags)
        {
            long blockCount = (file.Length + BlockSize - 1) / BlockSize;
            if (blockCount > MaxBlockCount)
            {
                throw new InvalidOperationException($"'{FilePath}' needs {blockCount} blocks of {BlockSize} bytes; a block blob allows {MaxBlockCount}.");
            }

            // Block IDs must have one length per blob; a per-upload prefix keeps them apart from another upload's uncommitted blocks.
            string uploadId = Guid.NewGuid().ToString("N");
            List<byte[]> blockIds = [];
            byte[] buffer = new byte[BlockSize];
            for (int index = 0; index < blockCount; index++)
            {
                int read = file.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
                byte[] block = read == buffer.Length ? buffer : buffer[..read];
                byte[] blockId = Encoding.ASCII.GetBytes($"{uploadId}{index:D6}");
                using HttpResponseMessage staged = await CreateExtendedProcessor<StageBlockBlockBlobProcessor>(p =>
                {
                    p.Url = Endpoint;
                    p.BlockId = blockId;
                    p.ContentLength = read;
                    p.Body = block;
                    p.EncryptionScope = EncryptionScope;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                blockIds.Add(blockId);
            }

            return await CreateExtendedProcessor<CommitBlockListRequest>(p =>
            {
                p.Url = Endpoint;
                p.Latest = blockIds;
                p.IfNoneMatch = NoOverwrite;
                p.MetadataPairs = metadata;
                p.BlobTagsString = BlobWireFormat.FormatTags(tags);
                p.BlobContentType = BlobContentType;
                p.BlobContentEncoding = BlobContentEncoding;
                p.BlobContentLanguage = BlobContentLanguage;
                p.BlobContentDisposition = BlobContentDisposition;
                p.BlobCacheControl = BlobCacheControl;
                p.Tier = Tier;
                p.EncryptionScope = EncryptionScope;
                p.Timeout = Timeout;
                p.ClientRequestId = ClientRequestId;
            }).SendAsync();
        }

        private sealed class PutBlobRequest : UploadBlockBlobProcessor
        {
            public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

            protected override void CreateRequestHeaders(HttpRequestMessage request)
            {
                base.CreateRequestHeaders(request);
                BlobWireFormat.AddMetadata(request, MetadataPairs);
            }
        }

        private sealed class CommitBlockListRequest : CommitBlockListBlockBlobProcessor
        {
            public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

            protected override void CreateRequestHeaders(HttpRequestMessage request)
            {
                base.CreateRequestHeaders(request);
                BlobWireFormat.AddMetadata(request, MetadataPairs);
            }
        }
    }
}
