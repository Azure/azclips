// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageFilesProcessor_Orchestrator : StorageDataPlaneProcessorBase, IFileListingProcessor
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
    public string AuthMode { get; set; } = "login";
    public string? FileEndpoint { get; set; }
    public bool DisallowTrailingDot { get; set; }
    public string NumResults { get; set; } = "*";
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public StorageFileContinuationToken? ContinuationToken { get; set; }
    public long? ResultLimit { get; set; }
    public bool ShowNextMarker { get; set; }
    public bool ExcludeExtendedInfo { get; set; }
    internal bool FilterDirectoriesOnly { get; set; }

    protected override StorageService Service => StorageService.Files;
    public override Task ProcessAsync() => ProcessWorkflowAsync(() =>
    {
        FilterDirectoriesOnly = false;
        return FileListingOperations.ExecuteCliAsync(this, FilterDirectoriesOnly);
    });

    internal override Task ProcessNativeAsync() =>
        ProcessWorkflowAsync(() => FileListingOperations.ExecuteAsync(this, FilterDirectoriesOnly));
}
