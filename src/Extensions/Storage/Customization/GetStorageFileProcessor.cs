// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class GetStorageFileProcessor : FileShareWorkflowBase
{
    public bool ExcludeExtendedInfo { get; set; }

    protected override async Task ExecuteAsync()
    {
        var directory = ResolveDirectory(Path);
        if (string.IsNullOrEmpty(Path))
        {
            var listing = CreateExtendedProcessor<ListStorageFilesProcessor>(processor =>
            {
                processor.Context = directory.Context; processor.DirectoryObject = directory; processor.Timeout = Timeout;
                processor.AllowTrailingDot = AllowTrailingDot; processor.ExcludeExtendedInfo = ExcludeExtendedInfo;
                processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
            });
            await listing.ProcessAsync();
            return;
        }
        if (ExcludeExtendedInfo)
            await Publisher.Publish(Events.Warning, "ExcludeExtendedInfo applies only when listing without Path.");
        var probe = CreateFileProcessor<FileProcessors.GetPropertiesDirectoryProcessor>(directory, processor =>
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
        var file = CreateFileProcessor<FileProcessors.GetPropertiesFileProcessor>(directory, processor =>
        {
            processor.Url = directory.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
        });
        using var fileResponse = await file.SendAsync();
        var fileProperties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(fileResponse), directory: false);
        await WriteOutput(new StorageFile(directory.Path, directory.Share) { Properties = fileProperties }, Token);
    }
}
