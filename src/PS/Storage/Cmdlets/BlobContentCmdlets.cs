// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Globalization;
using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

public abstract class BlobContentCmdletBase : StorageCmdletBase
{
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public StorageContext? Context { get; set; }

    [Parameter] public SwitchParameter Force { get; set; }
    [Parameter] public string? LeaseId { get; set; }
    [Parameter] public string? IfMatch { get; set; }
    [Parameter] public string? IfNoneMatch { get; set; }
    [Parameter] public DateTimeOffset? IfModifiedSince { get; set; }
    [Parameter] public DateTimeOffset? IfUnmodifiedSince { get; set; }
    [Parameter] public string? TagCondition { get; set; }
    [Parameter] [ValidateRange(1, int.MaxValue)] public int? ServerTimeoutPerRequest { get; set; }

    protected void ConfigureTarget(BlobTargetWorkflowBase command)
    {
        command.Context = Context; command.Profile = Profile; command.LeaseId = LeaseId;
        command.IfMatch = IfMatch; command.IfNoneMatch = IfNoneMatch;
        command.IfModifiedSince = IfModifiedSince; command.IfUnmodifiedSince = IfUnmodifiedSince;
        command.IfTags = TagCondition; command.Timeout = ServerTimeoutPerRequest;
    }

    protected string FileSystemPath(string path)
    {
        string result = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var provider, out _);
        if (provider.Name != "FileSystem") throw new ArgumentException("Specify a FileSystem path.");
        return result;
    }

    protected void ExecuteWrite(string command, StorageBlobPowerShellPlan plan)
    {
        StoppingToken.ThrowIfCancellationRequested();
        if (!ShouldProcess(plan.Target, plan.Action)) return;
        try { Execute(command, new() { ["Plan"] = plan }); }
        catch (StorageBlobOverwriteRequiredException conflict)
        {
            StoppingToken.ThrowIfCancellationRequested();
            if (!ShouldContinue($"Replace '{conflict.Plan.Target}'?", "Blob overwrite")) return;
            StoppingToken.ThrowIfCancellationRequested();
            Execute(command, new() { ["Plan"] = conflict.Plan.ApproveOverwrite() });
        }
    }

    protected static Dictionary<string, string>? Strings(Hashtable? table, bool properties = false)
    {
        if (table is null) return null;
        var values = new Dictionary<string, string>(properties ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is not string key || entry.Value is not string value)
                throw new ArgumentException("Specify string keys and string values.");
            if (!values.TryAdd(key, value)) throw new ArgumentException("Duplicate setting names are not supported.");
        }
        return values;
    }
}

[Cmdlet(VerbsCommon.Set, "AzStorageBlobContent", DefaultParameterSetName = "Name", SupportsShouldProcess = true)]
[OutputType(typeof(StorageBlob))]
public sealed class SetAzStorageBlobContentCmdlet : BlobContentCmdletBase
{
    [Alias("FullName")]
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = "Name")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Container")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Blob")]
    [ValidateNotNullOrEmpty] public string File { get; set; } = string.Empty;
    [Parameter(Mandatory = true, ParameterSetName = "Name")] [Alias("Name", "N")] public string? Container { get; set; }
    [Parameter(ParameterSetName = "Name")]
    [Parameter(ParameterSetName = "Container")]
    public string? Blob { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Container")]
    [Alias("CloudBlobContainer")] public StorageBlobContainer? ContainerObject { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Blob")]
    [Alias("CloudBlob", "ICloudBlob")] public StorageBlob? InputObject { get; set; }
    [Parameter] [ValidateSet("Block", "Page", "Append")] public string BlobType { get; set; } = "Block";
    [Parameter] public Hashtable? Properties { get; set; }
    [Parameter] public Hashtable? Metadata { get; set; }
    [Parameter] [Alias("Tags")] public Hashtable? Tag { get; set; }
    [Parameter] [ValidateSet("Hot", "Cool", "Cold", "Archive")] public string? StandardBlobTier { get; set; }
    [Parameter] [ValidateSet("P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80")]
    public string? PremiumPageBlobTier { get; set; }
    [Parameter] [ValidateRange(1, 4)] public int ConcurrentTaskCount { get; set; } = 2;
    [Parameter] [ValidateRange(1, 8 * 1024 * 1024)] public int ChunkSize { get; set; } = 4 * 1024 * 1024;
    [Parameter] public SwitchParameter ValidateContent { get; set; }

    protected override void ProcessRecord()
    {
        if (StandardBlobTier is not null && PremiumPageBlobTier is not null)
            throw new ArgumentException("Specify a standard Block tier or a premium Page tier, not both.");
        var properties = Strings(Properties, properties: true);
        string? Setting(string name) => properties?.GetValueOrDefault(name);
        if (properties?.Keys.Any(key => !new[] { "ContentType", "ContentEncoding", "ContentLanguage", "ContentDisposition",
            "CacheControl", "ContentMD5" }.Contains(key, StringComparer.OrdinalIgnoreCase)) == true)
            throw new ArgumentException("An unsupported blob content property was specified.");
        byte[]? md5 = null;
        if (Setting("ContentMD5") is { } encoded)
        {
            md5 = Convert.FromBase64String(encoded);
            if (md5.Length != 16) throw new ArgumentException("ContentMD5 must encode exactly 16 bytes.");
        }
        var command = new SetStorageBlobContentProcessor_Orchestrator
        {
            File = FileSystemPath(File), Container = Container, Blob = Blob, ContainerObject = ContainerObject,
            InputObject = InputObject, BlobType = BlobType.Equals("Page", StringComparison.OrdinalIgnoreCase) ? "Page" :
                BlobType.Equals("Append", StringComparison.OrdinalIgnoreCase) ? "Append" : "Block",
            Force = Force.IsPresent, ValidateContent = ValidateContent.IsPresent, Concurrency = ConcurrentTaskCount,
            ChunkSize = ChunkSize, ContentType = Setting("ContentType"), ContentEncoding = Setting("ContentEncoding"),
            ContentLanguage = Setting("ContentLanguage"), ContentDisposition = Setting("ContentDisposition"),
            CacheControl = Setting("CacheControl"), ContentMd5 = md5, Metadata = Strings(Metadata), Tags = Strings(Tag),
            Tier = StandardBlobTier is null ? PremiumPageBlobTier?.ToUpperInvariant() :
                CultureInfo.InvariantCulture.TextInfo.ToTitleCase(StandardBlobTier.ToLowerInvariant())
        };
        ConfigureTarget(command);
        ExecuteWrite("Set-AzStorageBlobContent", command.Prepare());
    }
}

[Cmdlet(VerbsCommon.Get, "AzStorageBlobContent", DefaultParameterSetName = "Name", SupportsShouldProcess = true)]
[OutputType(typeof(StorageBlob))]
public sealed class GetAzStorageBlobContentCmdlet : BlobContentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Container")] public string? Blob { get; set; }
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Name")] [Alias("Name", "N")] public string? Container { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Blob")]
    [Alias("CloudBlob", "ICloudBlob")] public StorageBlob? InputObject { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Container")]
    [Alias("CloudBlobContainer")] public StorageBlobContainer? ContainerObject { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = "Uri")] [Alias("Uri", "BlobUri")] public string? AbsoluteUri { get; set; }
    [Parameter] [Alias("Path")] public string? Destination { get; set; }
    [Parameter] public DateTimeOffset? SnapshotTime { get; set; }
    [Parameter] public string? VersionId { get; set; }
    [Parameter] public SwitchParameter CheckMd5 { get; set; }
    [Parameter] public SwitchParameter ValidateContent { get; set; }
    [Parameter] [ValidateRange(1, 4)] public int ConcurrentTaskCount { get; set; } = 2;
    [Parameter] [ValidateRange(1, 8 * 1024 * 1024)] public int ChunkSize { get; set; } = 4 * 1024 * 1024;

    protected override void ProcessRecord()
    {
        bool force = Force.IsPresent;
        var command = new GetStorageBlobContentProcessor_Orchestrator
        {
            Container = Container, Blob = Blob, InputObject = InputObject, ContainerObject = ContainerObject,
            BlobUri = AbsoluteUri, Destination = Destination is null ? null : FileSystemPath(Destination),
            Snapshot = SnapshotTime?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), VersionId = VersionId,
            CheckMd5 = CheckMd5.IsPresent, ValidateContent = ValidateContent.IsPresent,
            Concurrency = ConcurrentTaskCount, ChunkSize = ChunkSize, Force = force
        };
        ConfigureTarget(command);
        var plan = command.Prepare(FileSystemPath("."));
        StoppingToken.ThrowIfCancellationRequested();
        if (!ShouldProcess(plan.Target, plan.Action)) return;
        if (!force && System.IO.File.Exists(plan.Target))
        {
            if (!ShouldContinue($"Replace '{plan.Target}'?", "Blob download overwrite")) return;
            plan = plan.ApproveOverwrite();
        }
        StoppingToken.ThrowIfCancellationRequested();
        Execute("Get-AzStorageBlobContent", new() { ["Plan"] = plan });
    }
}
