// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class NewStorageDirectoryProcessor_Orchestrator : StorageDataPlaneProcessorBase, IFileShareOptions
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

    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    private async Task ExecuteAsync()
    {
        if (string.IsNullOrEmpty(Path) || Path.Trim('/').Length == 0)
            throw new ArgumentException("Specify the path of a new directory, not the share root.");
        var directory = FileShareOperations.Resolve(this, this, Path);
        if (directory.Share.ShareSnapshot is not null)
            throw new ArgumentException("Directories cannot be created in a share snapshot.");
        var metadata = Metadata is null ? null : new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase));
        using (var validation = new HttpRequestMessage())
            AddMetadataHeaders(validation, metadata);
        if (!ShouldProcess(directory.Uri.AbsoluteUri, "Create directory")) return;

        var child = FileShareOperations.CreateProcessor<FileProcessors.CreateDirectoryProcessor>(this, directory, processor =>
        {
            processor.Url = directory.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
            processor.Metadata = metadata; processor.FilePermission = "inherit"; processor.FileAttributes = "Directory";
            processor.FileCreationTime = "now"; processor.FileLastWriteTime = "now";
            processor.DeferFailureDiagnostics = true;
        });
        using var response = await child.SendAsync();
        Token.ThrowIfCancellationRequested();
        if (response.StatusCode != HttpStatusCode.Created)
            throw new InvalidDataException("Storage did not confirm directory creation with HTTP 201.");
        var properties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(response), directory: true);
        await WriteOutput(directory with { Properties = properties with { Metadata = metadata ?? properties.Metadata } }, Token);
    }
}
