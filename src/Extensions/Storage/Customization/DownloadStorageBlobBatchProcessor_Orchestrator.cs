// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class DownloadStorageBlobBatchProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobContainerOptions
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? Pattern { get; set; }
    public bool NoProgress { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }

    public string? Destination { get; set; }
    public bool Overwrite { get; set; }
    public bool DryRun { get; set; }
    public bool CheckMd5 { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }
    internal Func<string, bool>? NameFilter { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    private async Task ExecuteAsync()
    {
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var container = BlobContainerOperations.Resolve(this, this, defaultAccount: account);
        StorageBlobCliAddress.ValidateContext(account, container.Context);
        var filter = StorageCliPattern.Compile(Pattern);
        var modified = BlobCliOperations.ParseDate(IfModifiedSinceInput);
        var unmodified = BlobCliOperations.ParseDate(IfUnmodifiedSinceInput);
        ContainerObject = container;
        Context = container.Context;
        IfModifiedSince = modified;
        IfUnmodifiedSince = unmodified;
        NameFilter = filter;
        SuppressProgress = NoProgress;
        var names = new List<string>();
        await ExecuteOperationAsync(async (value, token) =>
        {
            token.ThrowIfCancellationRequested();
            string name = value as string ?? throw new InvalidDataException("A batch download returned an unsupported result.");
            names.Add(name);
            await Publisher.Publish(Events.Information, "Downloaded blob " + JsonSerializer.Serialize(name));
        });
        if (!DryRun) await WriteOutput(names.ToArray(), Token);
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(() => ExecuteOperationAsync());

    private async Task ExecuteOperationAsync(OutputStreamDelegate? output = null)
    {
        var writeOutput = output ?? WriteOutput;
        var container = BlobContainerOperations.Resolve(this, this);
        if (Concurrency is < 1 or > 4 || ChunkSize is < 1 or > StorageWireFormat.MaximumChunkBytes)
            throw new ArgumentException("Use 1-4 workers and chunks of 1 byte through 8 MiB.");
        if (string.IsNullOrWhiteSpace(Destination))
            throw new ArgumentException("Specify an existing destination directory.");
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Destination));
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The batch destination directory must exist.");
        if (!ShouldProcess(container.Uri.AbsoluteUri, "Download blob batch")) return;

        var plan = new List<Download>();
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var listing = CreateExtendedProcessor<ListStorageBlobsProcessor_Orchestrator>(processor =>
        {
            processor.ContainerObject = container; processor.Context = container.Context; processor.Timeout = Timeout;
            processor.NameFilter = NameFilter;
            processor.EntryHandler = (entry, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (entry is not StorageBlob blob)
                    throw new InvalidDataException("A flat blob listing returned a non-blob entry.");
                _ = blob.Uri;
                string path = ResolveDownloadPath(root, blob.Name);
                if (!paths.Add(path))
                    throw new InvalidDataException("Multiple blobs resolve to the same local download path; narrow the selection.");
                plan.Add(new Download(blob.Name, path));
                return Task.CompletedTask;
            };
        });
        await listing.ProcessNativeAsync();
        foreach (var item in plan)
        {
            Token.ThrowIfCancellationRequested();
            if (Directory.Exists(item.Path))
                throw new IOException("A batch file destination is already a directory.");
            if (!DryRun && !Overwrite && File.Exists(item.Path))
                throw new IOException("A batch destination file already exists; specify overwrite or choose another directory.");
            string? parent = Path.GetDirectoryName(item.Path);
            while (parent is not null && !paths.Comparer.Equals(parent, root))
            {
                if (paths.Contains(parent))
                    throw new InvalidDataException("Batch download paths conflict as files and directories; narrow the selection.");
                if (File.Exists(parent))
                    throw new IOException("A batch destination's parent path is already a file.");
                parent = Path.GetDirectoryName(parent);
            }
        }

        if (DryRun)
        {
            await Publisher.Publish(Events.Warning, $"Batch dry run: {plan.Count} selected blobs; no files will be written.");
            foreach (var item in plan)
            {
                Token.ThrowIfCancellationRequested();
                await Publisher.Publish(Events.Warning, "Would download blob " + JsonSerializer.Serialize(item.Name));
            }
            return;
        }

        int downloaded = 0, skipped = 0;
        foreach (var item in plan)
        {
            Token.ThrowIfCancellationRequested();
            DownloadStorageBlobWorkflow? child = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Path)!);
                child = CreateExtendedProcessor<DownloadStorageBlobWorkflow>(processor =>
                {
                    processor.Context = container.Context; processor.ContainerObject = container; processor.Blob = item.Name;
                    processor.Destination = item.Path; processor.Overwrite = Overwrite; processor.Timeout = Timeout;
                    processor.Concurrency = Concurrency; processor.ChunkSize = ChunkSize;
                    processor.CheckMd5 = CheckMd5; processor.ValidateContent = ValidateContent;
                    processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
                    processor.IfModifiedSince = IfModifiedSince; processor.IfUnmodifiedSince = IfUnmodifiedSince;
                    processor.IfTags = IfTags; processor.SuppressOutput = true; processor.DeferFailureDiagnostics = true;
                    processor.ProgressHandler = (bytes, total, complete) =>
                        PublishProgressAsync(plan.Count, downloaded, skipped, complete ? 1 : total == 0 ? 0 : bytes / (double)total);
                });
                await child.ProcessAsync();
                downloaded++;
                await writeOutput(item.Name, Token);
            }
            catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotModified &&
                child?.DestinationOpened != true)
            {
                skipped++;
                await Publisher.Publish(Events.Warning, "Skipped blob " + JsonSerializer.Serialize(item.Name) +
                    ": the download precondition was not met.");
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                if (exception is HttpRequestException { StatusCode: HttpStatusCode.PreconditionFailed } && child?.DestinationOpened == true)
                {
                    File.Delete(item.Path);
                    await Publisher.Publish(Events.Warning, "Blob " + JsonSerializer.Serialize(item.Name) +
                        " changed during the download; the partial local file was deleted.");
                }
                await Publisher.Publish(Events.Warning,
                    $"Batch stopped after {downloaded} downloads and {skipped} skips at blob {JsonSerializer.Serialize(item.Name)}; local output may be partial.");
                if (exception is IOException or UnauthorizedAccessException)
                    await Publisher.Publish(Events.ResourceOperationFailed, "The batch download could not write a local destination.");
                throw;
            }
            await PublishProgressAsync(plan.Count, downloaded, skipped, fraction: 0);
        }
        Token.ThrowIfCancellationRequested();
        await PublishProgressAsync(plan.Count, downloaded, skipped, fraction: 0, completed: true);
    }

    private static string ResolveDownloadPath(string root, string name)
    {
        if (OperatingSystem.IsWindows()) name = name.Replace('\\', '/');
        var segments = new List<string>();
        foreach (string segment in name.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new InvalidDataException("A blob name resolves outside the batch destination; narrow the selection.");
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            DownloadStorageBlobWorkflow.ValidateFileName(segment);
            segments.Add(segment);
        }
        if (segments.Count == 0)
            throw new InvalidDataException("A blob name does not identify a local file.");
        // Lexical containment deliberately preserves existing user-created symlink/junction layouts.
        string path = Path.GetFullPath(Path.Combine(root, Path.Combine(segments.ToArray())));
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("A blob name resolves outside the batch destination; narrow the selection.");
        return path;
    }

    private Task PublishProgressAsync(int count, int downloaded, int skipped, double fraction, bool completed = false) =>
        SuppressProgress ? Task.CompletedTask : Publisher.Publish(Events.Progress, Token, new EventData
        {
            Id = Events.Progress,
            Message = completed ? $"Batch download completed: {downloaded} downloaded, {skipped} skipped." :
                $"Batch download: {downloaded} downloaded, {skipped} skipped, {count} selected.",
            Value = completed ? 100 : count == 0 ? 0 : Math.Min(99, (downloaded + (double)skipped + fraction) * 100 / count),
            Cancel = Cancel
        });

    private sealed record Download(string Name, string Path);
}
