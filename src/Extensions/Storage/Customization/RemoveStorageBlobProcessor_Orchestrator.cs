// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class RemoveStorageBlobProcessor_Orchestrator : BlobCliOrchestratorBase
{

    public string? DeleteSnapshots { get; set; }
    public bool PassThru { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    protected override Task ExecuteAsync()
    {
        ResolveCliTarget();
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = ResolveTarget();
        if (DeleteSnapshots is not null && DeleteSnapshots is not ("include" or "only") ||
            DeleteSnapshots is not null && (target.Snapshot is not null || target.VersionId is not null))
            throw new ArgumentException("Specify snapshot deletion behavior only for a base blob.");
        if (!ShouldProcess(target.Url, "Delete blob")) return;
        var child = CreateBlobProcessor<BlobProcessors.DeleteBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.DeleteSnapshots = DeleteSnapshots;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });
        using var response = await child.SendAsync();
        if (PassThru) await WriteOutput(true, Token);
    }
}
