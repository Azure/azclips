// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageFilePowerShellPreflight : StorageDataPlaneProcessorBase, IFileShareOptions
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

    public StorageFileDirectory GetDirectoryTarget()
    {
        if (Context is null && ShareObject is null && DirectoryObject is null)
            throw new ArgumentException("Specify a native Storage context or Files resource.");
        return FileShareOperations.Resolve(this, this, Path);
    }

    private Task ExecuteAsync() => throw new NotSupportedException("Use the synchronous preflight method.");
}
