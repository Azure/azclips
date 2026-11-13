// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Uploads a local file: Create File (<see cref="CreateFileRequest"/>) sizes it, then Put Range
    /// (<see cref="UploadRangeFileProcessor"/>) writes it in <see cref="RangeSize"/> ranges. An existing file is replaced,
    /// as Create File does; a failed upload leaves the file partly written.
    /// </summary>
    public class UploadFileProcessor_Orchestrator : ProcessorBase<object, FileUploadResult>, ISasAuthorized
    {
        /// <summary>The service's maximum Put Range size.</summary>
        internal const int RangeSize = 4 * 1024 * 1024;
        private const string RangeUpdate = "update";

        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>File path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));

        /// <summary>Path of the local file to upload.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        public string? ContentType { get; set; }
        public string? ContentEncoding { get; set; }
        public string? ContentLanguage { get; set; }
        public string? ContentDisposition { get; set; }
        public string? CacheControl { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = FileWireFormat.ParsePairs(Metadata, nameof(Metadata));
                string url = Endpoint;
                using FileStream file = LocalFile.OpenRead(Source);
                long length = file.Length;

                HttpResponseMessage last = await CreateExtendedProcessor<CreateFileRequest>(p =>
                {
                    p.Url = url;
                    p.FileType = CreateFileRequest.File;
                    p.FileContentLength = length;
                    p.MetadataPairs = metadata;
                    p.FileContentType = ContentType;
                    p.FileContentEncoding = ContentEncoding;
                    p.FileContentLanguage = ContentLanguage;
                    p.FileContentDisposition = ContentDisposition;
                    p.FileCacheControl = CacheControl;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();

                try
                {
                    long offset = 0;
                    foreach (byte[] range in LocalFile.ReadChunks(file, RangeSize))
                    {
                        long start = offset;
                        HttpResponseMessage written = await CreateExtendedProcessor<UploadRangeFileProcessor>(p =>
                        {
                            p.Url = url;
                            p.Range = $"bytes={start}-{start + range.Length - 1}";
                            p.FileRangeWrite = RangeUpdate;
                            p.ContentLength = range.Length;
                            p.Body = range;
                            p.Timeout = Timeout;
                            p.ClientRequestId = ClientRequestId;
                        }).SendAsync();
                        last.Dispose();
                        last = written;
                        offset += range.Length;
                    }

                    await WriteOutput(FileUploadResult.From(last, length));
                }
                finally
                {
                    last.Dispose();
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
