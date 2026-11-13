// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Downloads a blob to a local file with one Get Blob (<see cref="DownloadBlobProcessor"/>), streaming the body to disk.
    /// The generated processor would read the body as a string and deserialize it; this writes it to <see cref="FilePath"/> instead.
    /// An existing file is never overwritten.
    /// </summary>
    public class DownloadBlobProcessor_Orchestrator : ProcessorBase<object, BlobDownloadResult>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        /// <summary>Path of the local file to create.</summary>
        public string FilePath { get; set; } = string.Empty;

        public string? Snapshot { get; set; }
        public string? VersionId { get; set; }
        public string? LeaseId { get; set; }
        public string? EncryptionKey { get; set; }
        public string? EncryptionKeySha256 { get; set; }
        public string? EncryptionAlgorithm { get; set; }
        public string? IfModifiedSince { get; set; }
        public string? IfUnmodifiedSince { get; set; }
        public string? IfMatch { get; set; }
        public string? IfNoneMatch { get; set; }
        public string? IfTags { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                string path = Path.GetFullPath(FilePath);
                if (File.Exists(path))
                {
                    throw new IOException($"'{path}' already exists.");
                }

                using HttpResponseMessage response = await CreateExtendedProcessor<DownloadBlobProcessor>(p =>
                {
                    p.Url = Endpoint;
                    p.Snapshot = Snapshot;
                    p.VersionId = VersionId;
                    p.LeaseId = LeaseId;
                    p.EncryptionKey = EncryptionKey;
                    p.EncryptionKeySha256 = EncryptionKeySha256;
                    p.EncryptionAlgorithm = EncryptionAlgorithm;
                    p.IfModifiedSince = IfModifiedSince;
                    p.IfUnmodifiedSince = IfUnmodifiedSince;
                    p.IfMatch = IfMatch;
                    p.IfNoneMatch = IfNoneMatch;
                    p.IfTags = IfTags;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();

                await using Stream content = await response.Content.ReadAsStreamAsync(Publisher.Token);
                long written = await BlobFile.WriteNewAsync(path, content, Publisher.Token);
                await WriteOutput(BlobDownloadResult.From(response, path, written));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
