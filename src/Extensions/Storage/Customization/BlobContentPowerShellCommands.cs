// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageBlobOverwriteRequiredException : Exception
{
    public StorageBlobPowerShellPlan Plan { get; }
    public StorageBlobOverwriteRequiredException(StorageBlobPowerShellPlan plan)
        : base("The destination blob exists. Confirmation is required before replacement.") => Plan = plan;
}

/// <summary>A locally validated, frozen operation carried across an invocation-thread confirmation.</summary>
public sealed class StorageBlobPowerShellPlan
{
    public string Target { get; }
    public string Action { get; }
    internal StorageBlobContainer Container { get; }
    internal string Name { get; }
    internal string Kind { get; }
    internal bool Force { get; }
    internal string? ETag { get; }
    internal string? CreateDirectory { get; }
    internal Action<BlobTargetWorkflowBase> Configure { get; }

    internal StorageBlobPowerShellPlan(StorageBlobContainer container, string name, string target, string action,
        string kind, bool force, Action<BlobTargetWorkflowBase> configure, string? etag = null, string? createDirectory = null)
    {
        Container = container; Name = name; Target = target; Action = action;
        Kind = kind; Force = force; Configure = configure; ETag = etag; CreateDirectory = createDirectory;
    }

    public StorageBlobPowerShellPlan ApproveOverwrite() =>
        new(Container, Name, Target, Action, Kind, true, Configure, ETag, CreateDirectory);
}

internal static class BlobPowerShellOptions
{
    internal static IReadOnlyDictionary<string, string>? Freeze(IReadOnlyDictionary<string, string>? values) =>
        values is null ? null : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values));

    internal static void Target(BlobTargetWorkflowBase source, BlobTargetWorkflowBase destination)
    {
        destination.Context = source.Context; destination.Container = source.Container; destination.Blob = source.Blob;
        destination.Snapshot = source.Snapshot; destination.VersionId = source.VersionId; destination.Profile = source.Profile;
        destination.Timeout = source.Timeout; destination.LeaseId = source.LeaseId;
        destination.IfMatch = source.IfMatch; destination.IfNoneMatch = source.IfNoneMatch;
        destination.IfModifiedSince = source.IfModifiedSince; destination.IfUnmodifiedSince = source.IfUnmodifiedSince;
        destination.IfTags = source.IfTags;
    }

    internal static void ValidateHeaders(params string?[] values)
    {
        if (values.Any(value => value?.Any(character => character < 0x20 || character > 0x7e) == true))
            throw new ArgumentException("Conditions and lease IDs must contain printable ASCII header values.");
    }
}

public abstract class BlobWritePowerShellCommandBase : BlobTargetWorkflowBase
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

public sealed class SetStorageBlobContentPowerShellCommand : BlobWritePowerShellCommandBase
{
    public string File { get; set; } = string.Empty;
    public string BlobType { get; set; } = "Block";
    public bool Force { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
    public string? Tier { get; set; }

    public StorageBlobPowerShellPlan Prepare()
    {
        if (Context is null && InputObject is null && ContainerObject is null)
            throw new ArgumentException("Specify a native Storage context or resource.");
        string path = Path.GetFullPath(File);
        if (!System.IO.File.Exists(path)) throw new FileNotFoundException("The source file must exist.", path);
        var target = ResolveTarget(Path.GetFileName(path));
        var frozen = (SetStorageBlobContentPowerShellCommand)MemberwiseClone();
        frozen.Context = target.Container.Context; frozen.Container = target.Container.Name; frozen.Blob = target.Name;
        frozen.InputObject = null; frozen.ContainerObject = null; frozen.File = path;
        frozen.Metadata = BlobPowerShellOptions.Freeze(Metadata); frozen.Tags = BlobPowerShellOptions.Freeze(Tags);
        frozen.ContentMd5 = ContentMd5?.ToArray();
        frozen.ContentType ??= StorageContentSettings.InferContentType(path);
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Uploads must target a base blob.");
        BlobPowerShellOptions.ValidateHeaders(LeaseId, IfMatch, IfNoneMatch, IfTags);
        BlobUploadWorkflowBase validation = BlobType switch
        {
            "Block" => new UploadStorageBlockBlobProcessor(),
            "Page" => new UploadStoragePageBlobProcessor(),
            "Append" => new UploadStorageAppendBlobProcessor(),
            _ => throw new ArgumentException("Specify Block, Page, or Append.")
        };
        frozen.Apply(validation);
        validation.ValidatePowerShellUpload();
        return new(target.Container, target.Name, target.Url, $"Upload {BlobType} Blob", BlobType, Force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is BlobUploadWorkflowBase upload) frozen.Apply(upload);
        });
    }

    private void Apply(BlobUploadWorkflowBase child)
    {
        BlobPowerShellOptions.Target(this, child);
        child.File = File; child.Overwrite = true; child.ValidateContent = ValidateContent;
        child.Concurrency = Concurrency; child.ChunkSize = ChunkSize;
        child.ContentType = ContentType; child.ContentEncoding = ContentEncoding; child.ContentLanguage = ContentLanguage;
        child.ContentDisposition = ContentDisposition; child.CacheControl = CacheControl; child.ContentMd5 = ContentMd5;
        child.Metadata = Metadata; child.Tags = Tags; child.Tier = Tier;
    }

    protected override Task ExecuteAsync() => ExecutePlanAsync(Plan ?? Prepare(), async plan =>
    {
        BlobUploadWorkflowBase child = plan.Kind switch
        {
            "Block" => CreateBlobProcessor<UploadStorageBlockBlobProcessor>(plan.Container, processor => ConfigureWrite(processor, plan)),
            "Page" => CreateBlobProcessor<UploadStoragePageBlobProcessor>(plan.Container, processor => ConfigureWrite(processor, plan)),
            "Append" => CreateBlobProcessor<UploadStorageAppendBlobProcessor>(plan.Container, processor => ConfigureWrite(processor, plan)),
            _ => throw new ArgumentException("Invalid upload plan.")
        };
        child.Overwrite = true;
        // Set-AzStorageBlobContent replaces Append Blobs; it never appends to an existing destination.
        if (!plan.Force)
            child.IfNoneMatch = "*";
        await child.ProcessAsync();
    });
}

public sealed class GetStorageBlobContentPowerShellCommand : BlobTargetWorkflowBase
{
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
        var target = ResolveTarget(resourceUri: BlobUri);
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
        var frozen = (GetStorageBlobContentPowerShellCommand)MemberwiseClone();
        frozen.Context = target.Container.Context; frozen.Container = target.Container.Name; frozen.Blob = target.Name;
        frozen.Snapshot = target.Snapshot; frozen.VersionId = target.VersionId;
        frozen.InputObject = null; frozen.ContainerObject = null; frozen.Destination = destination;
        return new(target.Container, target.Name, destination, "Download blob", "Download", Force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is DownloadStorageBlobProcessor download)
            {
                download.Destination = destination; download.CheckMd5 = frozen.CheckMd5;
                download.ValidateContent = frozen.ValidateContent; download.Concurrency = frozen.Concurrency;
                download.ChunkSize = frozen.ChunkSize;
            }
        }, createDirectory: createDirectory);
    }

    protected override async Task ExecuteAsync()
    {
        var plan = Plan ?? throw new ArgumentException("Specify a locally prepared download plan.");
        Token.ThrowIfCancellationRequested();
        if (plan.CreateDirectory is not null) Directory.CreateDirectory(plan.CreateDirectory);
        var child = CreateBlobProcessor<DownloadStorageBlobProcessor>(plan.Container, processor =>
        {
            plan.Configure(processor); processor.Overwrite = plan.Force;
        });
        await child.ProcessAsync();
    }
}

public sealed class StartStorageBlobCopyPowerShellCommand : BlobWritePowerShellCommandBase
{
    public StartStorageBlobCopyProcessor? Request { get; set; }

    public StorageBlobPowerShellPlan Prepare(bool force)
    {
        var request = Request ?? throw new ArgumentException("Specify copy settings.");
        var frozen = request.FreezePowerShellRequest();
        if (frozen.SourceContext is null && frozen.SourceBlobObject is null && frozen.SourceContainerObject is null &&
            frozen.SourceFileObject is null && frozen.SourceDirectoryObject is null && frozen.SourceShareObject is null)
        {
            var destination = frozen.InputObject?.Context ?? frozen.ContainerObject?.Context ?? frozen.Context ??
                throw new ArgumentException("Specify a native destination context or resource.");
            Uri? sourceUri = frozen.SourceUri is null ? null : new Uri(frozen.SourceUri, UriKind.Absolute);
            bool files = frozen.SourceShareName is not null || frozen.SourcePath is not null ||
                sourceUri?.IdnHost.EndsWith(".file.core.windows.net", StringComparison.OrdinalIgnoreCase) == true;
            string account = frozen.SourceStorageAccountName ?? (sourceUri is null ? destination.StorageAccountName :
                StorageContext.ValidatePublicUri(new UriBuilder(sourceUri) { Query = "" }.Uri,
                    files ? StorageService.Files : StorageService.Blob));
            frozen.SourceContext = new StorageContext(account, destination.Profile,
                enableFileBackupRequestIntent: files && frozen.EnableSourceFileBackupRequestIntent);
        }
        string target = frozen.ValidatePowerShellCopy();
        var container = frozen.ContainerObject!;
        return new(container, frozen.Blob!, target, "Start blob copy", "Copy", force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is StartStorageBlobCopyProcessor copy) frozen.ApplyPowerShellRequest(copy);
        });
    }

    protected override Task ExecuteAsync() => ExecutePlanAsync(Plan ?? throw new ArgumentException("Specify a prepared copy plan."),
        async plan =>
        {
            var child = CreateBlobProcessor<StartStorageBlobCopyProcessor>(plan.Container, processor => ConfigureWrite(processor, plan));
            child.Overwrite = plan.Force;
            await child.ProcessAsync();
        });
}
