// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobWritePowerShellOrchestratorBase : BlobTargetWorkflowBase
{
    public StorageBlobPowerShellPlan? Plan { get; set; }

    protected async Task ExecutePlanAsync(StorageBlobPowerShellPlan plan, Func<StorageBlobPowerShellPlan, Task> execute)
    {
        if (!plan.Force)
        {
            var probe = CreateBlobProcessor<BlobProcessors.GetPropertiesBlobProcessor>(plan.Container, child =>
            {
                child.Url = plan.Target;
                child.DeferFailureDiagnostics = true;
                var settings = new BlobProbeSettings();
                plan.Configure(settings);
                child.Timeout = settings.Timeout; child.LeaseId = settings.LeaseId;
            });
            string? etag = null;
            try
            {
                using var response = await probe.SendAsync();
                etag = response.Headers.ETag?.ToString() ??
                    throw new InvalidDataException("Storage did not return the destination blob's ETag.");
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound &&
                exception.Data["ServiceErrorCode"] is "BlobNotFound")
            {
                // Only the exact missing blob permits a create-only write.
            }
            Token.ThrowIfCancellationRequested();
            if (etag is not null)
                throw new StorageBlobOverwriteRequiredException(new(plan.Container, plan.Name, plan.Target,
                    plan.Action, plan.Kind, false, plan.Configure, etag));
        }
        Token.ThrowIfCancellationRequested();
        await execute(plan);
    }

    protected void ConfigureWrite(BlobTargetWorkflowBase child, StorageBlobPowerShellPlan plan)
    {
        plan.Configure(child);
        if (plan.ETag is not null)
        {
            if (child.IfMatch is not null && child.IfMatch != "*" && child.IfMatch != plan.ETag)
                throw new ArgumentException("The supplied ETag does not match the confirmed destination.");
            child.IfMatch = plan.ETag;
        }
    }

    private sealed class BlobProbeSettings : BlobTargetWorkflowBase
    {
        protected override Task ExecuteAsync() => Task.CompletedTask;
    }
}
