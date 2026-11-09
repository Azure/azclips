// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GetStorageBlobContentProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobTargetOptions
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
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public string? BlobUri { get; set; }
    public string? Destination { get; set; }
    public bool Force { get; set; }
    public bool CheckMd5 { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;
    public StorageBlobPowerShellPlan? Plan { get; set; }

    public StorageBlobPowerShellPlan Prepare(string currentDirectory)
    {
        if (Context is null && InputObject is null && ContainerObject is null)
            throw new ArgumentException("Specify a native Storage context or resource.");
        var target = BlobTargetOperations.Resolve(this, this, resourceUri: BlobUri);
        StorageDownload.ValidateOptions(Concurrency, ChunkSize, null, null);
        BlobPowerShellOptions.ValidateHeaders(LeaseId, IfMatch, IfNoneMatch, IfTags);
        string destination = Path.GetFullPath(Destination ?? currentDirectory);
        string? createDirectory = null;
        if (Directory.Exists(destination))
        {
            string root = Path.EndsInDirectorySeparator(destination) ? destination : destination + Path.DirectorySeparatorChar;
            var segments = target.Name.Replace('\\', '/').Split('/');
            if (segments.Any(segment => segment is "" or "." or ".."))
                throw new ArgumentException("The blob name cannot safely be mapped beneath the destination directory.");
            string[] names = segments.Select(segment =>
            {
                string encoded = string.Concat(segment.TrimEnd(' ').Select(character =>
                    Path.GetInvalidFileNameChars().Contains(character) ?
                        "%" + ((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture) : character.ToString()));
                string stem = encoded.Split('.')[0].TrimEnd(' ');
                if (OperatingSystem.IsWindows() && System.Text.RegularExpressions.Regex.IsMatch(stem,
                    @"^(CON|PRN|AUX|NUL|CLOCK\$|CONIN\$|CONOUT\$|COM[1-9]|LPT[1-9])$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                    encoded = Path.GetFileNameWithoutExtension(encoded) + " (1)" + Path.GetExtension(encoded);
                StorageDownload.ValidateFileName(encoded);
                return encoded;
            }).ToArray();
            string name = names[^1];
            if (target.Snapshot is not null)
            {
                if (!DateTimeOffset.TryParse(target.Snapshot, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var snapshot))
                    throw new ArgumentException("Specify a valid snapshot timestamp.");
                name = Path.GetFileNameWithoutExtension(name) + " (" +
                    snapshot.UtcDateTime.ToString("yyyy-MM-dd HHmmss", System.Globalization.CultureInfo.InvariantCulture) +
                    ")" + Path.GetExtension(name);
            }
            StorageDownload.ValidateFileName(name);
            names[^1] = name;
            destination = Path.GetFullPath(Path.Combine(root, Path.Combine(names)));
            if (!destination.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException("The blob name escapes the destination directory.");
            createDirectory = Path.GetDirectoryName(destination);
        }
        if (createDirectory is null) destination = StorageDownload.ResolveDestination(destination);
        else if (Directory.Exists(destination)) throw new ArgumentException("The destination must identify a file.");
        var frozen = (GetStorageBlobContentProcessor_Orchestrator)MemberwiseClone();
        frozen.Context = target.Container.Context; frozen.Container = target.Container.Name; frozen.Blob = target.Name;
        frozen.Snapshot = target.Snapshot; frozen.VersionId = target.VersionId;
        frozen.InputObject = null; frozen.ContainerObject = null; frozen.Destination = destination;
        return new(target.Container, target.Name, destination, "Download blob", "Download", Force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is DownloadStorageBlobWorkflow download)
            {
                download.Destination = destination; download.CheckMd5 = frozen.CheckMd5;
                download.ValidateContent = frozen.ValidateContent; download.Concurrency = frozen.Concurrency;
                download.ChunkSize = frozen.ChunkSize;
            }
        }, createDirectory: createDirectory);
    }

    private async Task ExecuteAsync()
    {
        var plan = Plan ?? throw new ArgumentException("Specify a locally prepared download plan.");
        Token.ThrowIfCancellationRequested();
        if (plan.CreateDirectory is not null) Directory.CreateDirectory(plan.CreateDirectory);
        var child = BlobContainerOperations.CreateProcessor<DownloadStorageBlobWorkflow>(this, plan.Container, processor =>
        {
            plan.Configure(processor); processor.Overwrite = plan.Force;
        });
        await child.ProcessAsync();
    }
}
