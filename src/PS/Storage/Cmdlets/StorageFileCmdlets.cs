// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

public abstract class StorageFileCmdletBase : StorageCmdletBase
{
    protected const string NameSet = "ShareName";
    protected const string ShareSet = "Share";
    protected const string DirectorySet = "Directory";

    [Parameter]
    [ValidateNotNull]
    public StorageContext? Context { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = NameSet)]
    [ValidateNotNullOrEmpty]
    public string? ShareName { get; set; }

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ShareSet)]
    [Alias("ShareClient")]
    [ValidateNotNull]
    public StorageFileShare? ShareObject { get; set; }

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = DirectorySet)]
    [Alias("ShareDirectoryClient")]
    [ValidateNotNull]
    public StorageFileDirectory? DirectoryObject { get; set; }

    [Parameter]
    public SwitchParameter EnableFileBackupRequestIntent { get; set; }

    [Parameter]
    public SwitchParameter DisAllowTrailingDot { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? ShareSnapshot { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ServerTimeoutPerRequest { get; set; }

    protected StorageFilePowerShellPreflight Preflight(string? path, StorageFile? file = null) => new()
    {
        Context = Context, ShareName = ShareName, ShareObject = ShareObject, DirectoryObject = DirectoryObject,
        Path = path, FileObject = file, ShareSnapshot = ShareSnapshot, Profile = Profile,
        EnableFileBackupRequestIntent = EnableFileBackupRequestIntent.IsPresent,
        AllowTrailingDot = !DisAllowTrailingDot.IsPresent, Timeout = ServerTimeoutPerRequest
    };

    protected Dictionary<string, object> Parameters(StorageFileShare share, string? path)
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(Context)] = share.Context,
            [nameof(ShareObject)] = share,
            ["AllowTrailingDot"] = !DisAllowTrailingDot.IsPresent
        };
        if (path is not null) parameters.Add("Path", path);
        if (ServerTimeoutPerRequest is not null) parameters.Add("Timeout", ServerTimeoutPerRequest.Value);
        return parameters;
    }

    protected static IReadOnlyDictionary<string, string>? FreezeMetadata(Hashtable? metadata)
    {
        if (metadata is null) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in metadata)
        {
            if (entry.Key is not string key || entry.Value is not string value || string.IsNullOrEmpty(key) ||
                !result.TryAdd(key, value))
                throw new ArgumentException("Metadata requires unique, nonempty string keys and string values.");
        }
        return new ReadOnlyDictionary<string, string>(result);
    }
}

[Cmdlet(VerbsCommon.Get, "AzStorageFile", DefaultParameterSetName = "ShareName")]
[OutputType(typeof(StorageFile), typeof(StorageFileDirectory))]
public sealed class GetAzStorageFileCmdlet : StorageFileCmdletBase
{
    [Parameter(Position = 1)]
    public string? Path { get; set; }

    [Parameter]
    public SwitchParameter ExcludeExtendedInfo { get; set; }

    protected override void ProcessRecord()
    {
        var directory = Preflight(Path).GetDirectoryTarget();
        var parameters = Parameters(directory.Share, string.IsNullOrEmpty(Path) ? null : directory.Path);
        if (string.IsNullOrEmpty(Path))
            parameters.Add(nameof(DirectoryObject), directory);
        parameters.Add(nameof(ExcludeExtendedInfo), ExcludeExtendedInfo.IsPresent);
        Execute("Get-AzStorageFile", parameters);
    }
}

[Cmdlet(VerbsCommon.New, "AzStorageDirectory", DefaultParameterSetName = "ShareName", SupportsShouldProcess = true)]
[OutputType(typeof(StorageFileDirectory))]
public sealed class NewAzStorageDirectoryCmdlet : StorageFileCmdletBase
{
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    [Parameter]
    public Hashtable? Metadata { get; set; }

    protected override void ProcessRecord()
    {
        var directory = Preflight(Path).GetDirectoryTarget();
        if (directory.Path.Length == 0 || directory.Share.ShareSnapshot is not null)
            throw new ArgumentException("Specify a new directory in a live share, not the root or a snapshot.");
        var parameters = Parameters(directory.Share, directory.Path);
        var metadata = FreezeMetadata(Metadata);
        if (metadata is not null) parameters.Add(nameof(Metadata), metadata);
        if (!ShouldProcess(directory.Uri.AbsoluteUri, "Create directory")) return;
        StoppingToken.ThrowIfCancellationRequested();
        Execute("New-AzStorageDirectory", parameters);
    }
}
