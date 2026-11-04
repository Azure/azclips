// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageBlobOverwriteRequiredException : Exception
{
    public StorageBlobPowerShellPlan Plan { get; }
    public StorageBlobOverwriteRequiredException(StorageBlobPowerShellPlan plan)
        : base("The destination blob exists. Confirmation is required before replacement.") => Plan = plan;
}
