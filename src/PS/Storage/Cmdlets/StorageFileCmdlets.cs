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
    protected const string FileSet = "File";

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

    protected bool IsWhatIf => MyInvocation.BoundParameters.TryGetValue("WhatIf", out var supplied)
        ? LanguagePrimitives.IsTrue(supplied)
        : LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference"));

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

[Cmdlet(VerbsCommon.Set, "AzStorageFileContent", DefaultParameterSetName = "ShareName", SupportsShouldProcess = true)]
[OutputType(typeof(StorageFile))]
public sealed class SetAzStorageFileContentCmdlet : StorageFileCmdletBase
{
    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName")]
    [ValidateNotNullOrEmpty]
    public string Source { get; set; } = string.Empty;

    [Parameter(Position = 2)]
    public string? Path { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public SwitchParameter ValidateContent { get; set; }

    [Parameter]
    [ValidateRange(1, 4)]
    public int ConcurrentTaskCount { get; set; } = 2;

    [Parameter]
    [ValidateRange(1, 4 * 1024 * 1024)]
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;

    [Parameter]
    public Hashtable? Metadata { get; set; }

    [Parameter]
    public string? ContentType { get; set; }

    [Parameter]
    public string? ContentEncoding { get; set; }

    [Parameter]
    public string? ContentLanguage { get; set; }

    [Parameter]
    public string? ContentDisposition { get; set; }

    [Parameter]
    public string? CacheControl { get; set; }

    [Parameter]
    [ValidateCount(16, 16)]
    public byte[]? ContentMD5 { get; set; }

    protected override void ProcessRecord()
    {
        string source = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Source);
        var candidate = Preflight(Path).GetFileTarget(source);
        var baseDirectory = Preflight(null).GetDirectoryTarget();
        string? relative = Path;
        bool force = Force.IsPresent;
        var parameters = Parameters(baseDirectory.Share, relative);
        if (baseDirectory.Path.Length != 0)
            parameters.Add(nameof(DirectoryObject), baseDirectory);
        parameters.Add(nameof(Source), source);
        parameters.Add(nameof(Force), force);
        parameters.Add(nameof(PassThru), PassThru.IsPresent);
        parameters.Add(nameof(ValidateContent), ValidateContent.IsPresent);
        parameters.Add("Concurrency", ConcurrentTaskCount);
        parameters.Add(nameof(ChunkSize), ChunkSize);
        Add(nameof(ContentType), ContentType);
        Add(nameof(ContentEncoding), ContentEncoding);
        Add(nameof(ContentLanguage), ContentLanguage);
        Add(nameof(ContentDisposition), ContentDisposition);
        Add(nameof(CacheControl), CacheControl);
        Add("ContentMd5", ContentMD5?.ToArray());
        Add(nameof(Metadata), FreezeMetadata(Metadata));
        if (IsWhatIf)
        {
            ShouldProcess(candidate.Uri.AbsoluteUri, "Upload file (remote directory resolution deferred)");
            return;
        }
        StoppingToken.ThrowIfCancellationRequested();
        StorageFileUploadPreflightException approved;
        try
        {
            Execute("Set-AzStorageFileContent", parameters);
            throw new InvalidOperationException("The upload preflight completed without resolving an approval target.");
        }
        catch (StorageFileUploadPreflightException resolution)
        {
            approved = resolution;
        }
        StoppingToken.ThrowIfCancellationRequested();
        if (approved.File.Share.Name != candidate.Share.Name ||
            approved.File.Share.ShareSnapshot != candidate.Share.ShareSnapshot ||
            approved.File.Context.StorageAccountName != candidate.Context.StorageAccountName ||
            approved.File.Context.Profile != candidate.Context.Profile ||
            approved.File.Context.EnableFileBackupRequestIntent != candidate.Context.EnableFileBackupRequestIntent ||
            approved.File.Path != candidate.Path && approved.File.Path != candidate.Path.TrimEnd('/') + "/" + System.IO.Path.GetFileName(source))
            throw new InvalidOperationException("The upload preflight returned a different account, share or path.");
        if (!ShouldProcess(approved.File.Uri.AbsoluteUri, "Upload file")) return;
        if (approved.Exists && !force &&
            !ShouldContinue($"Overwrite '{approved.File.Uri.AbsoluteUri}'?", "File upload")) return;
        StoppingToken.ThrowIfCancellationRequested();
        var retry = new Dictionary<string, object>(parameters)
        {
            [nameof(Context)] = approved.File.Context,
            [nameof(ShareObject)] = approved.File.Share,
            [nameof(Path)] = approved.File.Path,
            ["PreflightOnly"] = false
        };
        retry.Remove(nameof(DirectoryObject));
        retry.Remove(nameof(Profile));
        Execute("Set-AzStorageFileContent", retry);

        void Add(string name, object? value)
        {
            if (value is not null) parameters.Add(name, value);
        }
    }
}

[Cmdlet(VerbsCommon.Get, "AzStorageFileContent", DefaultParameterSetName = "ShareName", SupportsShouldProcess = true)]
[OutputType(typeof(StorageFile))]
public sealed class GetAzStorageFileContentCmdlet : StorageFileCmdletBase
{
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = NameSet)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ShareSet)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = DirectorySet)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = FileSet)]
    [Alias("ShareFileClient", "InputObject")]
    [ValidateNotNull]
    public StorageFile? FileObject { get; set; }

    [Parameter(Position = 2, ParameterSetName = NameSet)]
    [Parameter(Position = 2, ParameterSetName = ShareSet)]
    [Parameter(Position = 2, ParameterSetName = DirectorySet)]
    [Parameter(Position = 1, ParameterSetName = FileSet)]
    public string? Destination { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public SwitchParameter CheckMd5 { get; set; }

    [Parameter]
    [ValidateRange(1, 4)]
    public int ConcurrentTaskCount { get; set; } = 2;

    [Parameter]
    [ValidateRange(1, 8 * 1024 * 1024)]
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;

    protected override void ProcessRecord()
    {
        var file = Preflight(Path, FileObject).GetFileTarget();
        string local = SessionState.Path.GetUnresolvedProviderPathFromPSPath(string.IsNullOrEmpty(Destination) ? "." : Destination);
        string destination = StorageFileFrontend.ResolveDestination(local, file.Path);
        bool overwrite = Force.IsPresent;
        var parameters = Parameters(file.Share, file.Path);
        parameters.Add(nameof(Destination), destination);
        parameters.Add(nameof(PassThru), PassThru.IsPresent);
        parameters.Add(nameof(CheckMd5), CheckMd5.IsPresent);
        parameters.Add("Concurrency", ConcurrentTaskCount);
        parameters.Add(nameof(ChunkSize), ChunkSize);
        if (!ShouldProcess(destination, "Download file")) return;
        if (File.Exists(destination) && !overwrite)
        {
            if (!ShouldContinue($"Overwrite '{destination}'?", "File download")) return;
            overwrite = true;
        }
        parameters.Add("Overwrite", overwrite);
        StoppingToken.ThrowIfCancellationRequested();
        Execute("Get-AzStorageFileContent", parameters);
    }
}
