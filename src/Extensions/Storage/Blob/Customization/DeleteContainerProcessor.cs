// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Delete Container answers 202 with headers only, so the generated pipeline writes no output.
    /// The container and all its blobs are removed asynchronously; the name can't be reused for at least 30 seconds.
    /// </summary>
    public partial class DeleteContainerProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName));
    }
}
