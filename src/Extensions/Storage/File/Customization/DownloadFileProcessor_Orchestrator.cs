// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Downloads a file to a local path with one Get File (<see cref="DownloadFileProcessor"/>), streaming the body to disk.
    /// The generated processor would buffer the body and deserialize it; this writes it to <see cref="Destination"/> instead.
    /// An existing local file is never overwritten.
    /// </summary>
    public class DownloadFileProcessor_Orchestrator : ProcessorBase<object, FileDownloadResult>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>File path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));

        /// <summary>Path of the local file to create.</summary>
        public string Destination { get; set; } = string.Empty;

        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                string path = Path.GetFullPath(Destination);
                if (File.Exists(path))
                {
                    throw new IOException($"'{path}' already exists.");
                }

                using HttpResponseMessage response = await CreateExtendedProcessor<DownloadFileProcessor>(p =>
                {
                    p.Url = Endpoint;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();

                await using Stream content = await response.Content.ReadAsStreamAsync(Publisher.Token);
                long written = await LocalFile.WriteNewAsync(path, content, Publisher.Token);
                await WriteOutput(FileDownloadResult.From(response, path, written));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
