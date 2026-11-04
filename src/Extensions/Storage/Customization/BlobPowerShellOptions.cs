// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class BlobPowerShellOptions
{
    internal static IReadOnlyDictionary<string, string>? Freeze(IReadOnlyDictionary<string, string>? values) =>
        values is null ? null : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values));

    internal static void Target(BlobTargetWorkflowBase source, BlobTargetWorkflowBase destination)
    {
        destination.Context = source.Context; destination.Container = source.Container; destination.Blob = source.Blob;
        destination.Snapshot = source.Snapshot; destination.VersionId = source.VersionId; destination.Profile = source.Profile;
        destination.Timeout = source.Timeout; destination.LeaseId = source.LeaseId;
        destination.IfMatch = source.IfMatch; destination.IfNoneMatch = source.IfNoneMatch;
        destination.IfModifiedSince = source.IfModifiedSince; destination.IfUnmodifiedSince = source.IfUnmodifiedSince;
        destination.IfTags = source.IfTags;
    }

    internal static void ValidateHeaders(params string?[] values)
    {
        if (values.Any(value => value?.Any(character => character < 0x20 || character > 0x7e) == true))
            throw new ArgumentException("Conditions and lease IDs must contain printable ASCII header values.");
    }
}
