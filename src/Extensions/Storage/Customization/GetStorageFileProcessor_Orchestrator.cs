// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GetStorageFileProcessor_Orchestrator : StorageDataPlaneProcessorBase, IFileShareOptions
{
    public string? StorageAccountName { get; set; }
    public string? ShareName { get; set; }
    public StorageFileShare? ShareObject { get; set; }
    public StorageFileDirectory? DirectoryObject { get; set; }
    public string? Path { get; set; }
    public string? ShareSnapshot { get; set; }
    public bool EnableFileBackupRequestIntent { get; set; }
    public bool? AllowTrailingDot { get; set; }
    public int? Timeout { get; set; }
    protected override StorageService Service => StorageService.Files;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public bool ExcludeExtendedInfo { get; set; }

    private async Task ExecuteAsync()
    {
        var directory = FileShareOperations.Resolve(this, this, Path);
        if (string.IsNullOrEmpty(Path))
        {
            var listing = CreateExtendedProcessor<ListStorageFilesProcessor_Orchestrator>(processor =>
            {
                processor.Context = directory.Context; processor.DirectoryObject = directory; processor.Timeout = Timeout;
                processor.AllowTrailingDot = AllowTrailingDot; processor.ExcludeExtendedInfo = ExcludeExtendedInfo;
                processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
            });
            await listing.ProcessNativeAsync();
            return;
        }
        if (ExcludeExtendedInfo)
            await Publisher.Publish(Events.Warning, "ExcludeExtendedInfo applies only when listing without Path.");
        var probe = FileShareOperations.CreateProcessor<FileProcessors.GetPropertiesDirectoryProcessor>(this, directory, processor =>
        {
            processor.Url = directory.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
            processor.DeferFailureDiagnostics = true;
        });
        try
        {
            using var response = await probe.SendAsync();
            var properties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(response), directory: true);
            await WriteOutput(directory with { Properties = properties }, Token);
            return;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // Legacy lookup permits a file read even when the directory probe is missing or denied.
        }
        var file = FileShareOperations.CreateProcessor<FileProcessors.GetPropertiesFileProcessor>(this, directory, processor =>
        {
            processor.Url = directory.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
        });
        using var fileResponse = await file.SendAsync();
        var fileProperties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(fileResponse), directory: false);
        await WriteOutput(new StorageFile(directory.Path, directory.Share) { Properties = fileProperties }, Token);
    }
}
