// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class TestStorageBlobExistsProcessor_Orchestrator : BlobCliOrchestratorBase
{

    protected override Task ExecuteAsync()
    {
        ResolveCliTarget();
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = ResolveTarget();
        bool exists;
        try
        {
            var properties = PropertiesProcessor(target);
            properties.DeferFailureDiagnostics = true;
            using var response = await properties.SendAsync();
            exists = true;
        }
        catch (HttpRequestException exception) when (IsMissingBlob(exception) ||
            exception.StatusCode == System.Net.HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "ContainerNotFound")
        {
            exists = false;
        }
        await WriteOutput(new StorageBlobExistence(exists), Token);
    }
}
