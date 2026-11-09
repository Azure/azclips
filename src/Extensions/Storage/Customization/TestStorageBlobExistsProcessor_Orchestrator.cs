// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class TestStorageBlobExistsProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobCliProcessor
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    public string? Blob { get; set; }
    public StorageBlob? InputObject { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }
    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    private Task ExecuteAsync()
    {
        BlobCliOperations.ResolveTarget(this);
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = BlobTargetOperations.Resolve(this, this);
        bool exists;
        try
        {
            var properties = BlobTargetOperations.PropertiesProcessor(this, target);
            properties.DeferFailureDiagnostics = true;
            using var response = await properties.SendAsync();
            exists = true;
        }
        catch (HttpRequestException exception) when (BlobTargetOperations.IsMissingBlob(exception) ||
            exception.StatusCode == System.Net.HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "ContainerNotFound")
        {
            exists = false;
        }
        await WriteOutput(new StorageBlobExistence(exists), Token);
    }
}
