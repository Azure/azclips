// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class RemoveStorageBlobPowerShellProcessor_Orchestrator : BlobTargetWorkflowBase
{
    public string? DeleteSnapshots { get; set; }
    public bool PassThru { get; set; }

    public string GetConfirmationTarget()
    {
        if (Context is null && ContainerObject is null && InputObject is null)
            throw new ArgumentException("Specify a native Storage context or resource.");
        return ResolveRemovalTarget().Url;
    }

    private Target ResolveRemovalTarget()
    {
        var target = ResolveTarget();
        if (DeleteSnapshots is not null && DeleteSnapshots is not ("include" or "only") ||
            DeleteSnapshots is not null && (target.Snapshot is not null || target.VersionId is not null))
            throw new ArgumentException("Specify snapshot deletion behavior only for a base blob.");
        return target;
    }

    protected override async Task ExecuteAsync()
    {
        var target = ResolveRemovalTarget();
        string? deleteSnapshots = DeleteSnapshots;
        var child = CreateBlobProcessor<RemoveStorageBlobProcessor_Orchestrator>(target.Container, processor =>
        {
            processor.ContainerObject = target.Container;
            processor.Blob = target.Name;
            processor.Snapshot = target.Snapshot;
            processor.VersionId = target.VersionId;
            processor.DeleteSnapshots = deleteSnapshots;
            processor.PassThru = PassThru;
            processor.Timeout = Timeout;
            processor.IfTags = IfTags;
            processor.DeferFailureDiagnostics = true;
        });
        try
        {
            await child.ProcessNativeAsync();
        }
        catch (HttpRequestException exception) when (
            deleteSnapshots is null && target.Snapshot is null && target.VersionId is null &&
            exception.StatusCode == HttpStatusCode.Conflict && exception.Data["ServiceErrorCode"] is "SnapshotsPresent")
        {
            Token.ThrowIfCancellationRequested();
            // The cmdlet catches this after session disposal, on the invocation thread.
            throw new StorageBlobSnapshotsPresentException(target.Container, target.Name);
        }
    }
}
