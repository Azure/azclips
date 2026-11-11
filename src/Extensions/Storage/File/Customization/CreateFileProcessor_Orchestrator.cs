// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Creates a file of <see cref="Size"/> bytes with no content (<see cref="CreateFileProcessor"/>); its directory must
    /// already exist and an existing file is replaced. The response headers are written as a <see cref="ShareFile"/>.
    /// </summary>
    public class CreateFileProcessor_Orchestrator : ProcessorBase<object, ShareFile>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>File path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));

        /// <summary>The file size in bytes.</summary>
        public long Size { get; set; }

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
                using HttpResponseMessage response = await CreateExtendedProcessor<CreateFileRequest>(p =>
                {
                    p.Url = url;
                    p.FileType = CreateFileRequest.File;
                    p.FileContentLength = Size;
                    p.MetadataPairs = metadata;
                    p.FileContentType = ContentType;
                    p.FileContentEncoding = ContentEncoding;
                    p.FileContentLanguage = ContentLanguage;
                    p.FileContentDisposition = ContentDisposition;
                    p.FileCacheControl = CacheControl;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();

                ShareFile file = ShareFile.FromHeaders(response, url);
                file.ContentLength = Size;
                await WriteOutput(file);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }

    /// <summary>Create File with metadata sent as one <c>x-ms-meta-{key}</c> header per pair; shared by create and upload.</summary>
    internal sealed class CreateFileRequest : CreateFileProcessor
    {
        /// <summary>The only <c>x-ms-type</c> the service accepts.</summary>
        internal const string File = "file";

        public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

        protected override void CreateRequestHeaders(HttpRequestMessage request)
        {
            base.CreateRequestHeaders(request);
            FileWireFormat.AddMetadata(request, MetadataPairs);
        }
    }
}
