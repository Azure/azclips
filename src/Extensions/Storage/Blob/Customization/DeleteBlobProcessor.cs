// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Delete Blob answers 202 with headers only, so the generated pipeline writes no output.
    /// A blob with snapshots needs <see cref="DeleteSnapshots"/>, otherwise the service answers 409 <c>SnapshotsPresent</c>.
    /// </summary>
    public partial class DeleteBlobProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));
    }
}
