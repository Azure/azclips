// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageBlobSnapshotsPresentException : Exception
{
    public StorageBlobContainer Container { get; }
    public string BlobName { get; }
    public Uri BlobUri { get; }

    public StorageBlobSnapshotsPresentException(StorageBlobContainer container, string blobName)
        : base("The blob has snapshots. Confirmation is required before deleting the blob and its snapshots.")
    {
        ArgumentNullException.ThrowIfNull(container);
        BlobUri = container.Context.GetBlobUri(container.Name, blobName);
        Container = container;
        BlobName = blobName;
    }
}
