// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageFileUploadPreflightException(StorageFile file, bool exists) : Exception(
    "The file upload target has been resolved; invocation-thread approval is required before writing.")
{
    public StorageFile File { get; } = file;
    public bool Exists { get; } = exists;
}

public sealed class StorageFilePowerShellPreflight : FileShareWorkflowBase
{
    public StorageFile? FileObject { get; set; }

    public StorageFileDirectory GetDirectoryTarget()
    {
        RequireNativeContext();
        return ResolveDirectory(Path);
    }

    public StorageFile GetFileTarget(string? source = null)
    {
        RequireNativeContext();
        if (FileObject is not null && DirectoryObject is not null)
            throw new ArgumentException("Choose a native file or directory, not both.");
        string? path = FileObject?.Path ?? Path;
        if (source is not null)
        {
            string name = System.IO.Path.GetFileName(System.IO.Path.GetFullPath(source));
            path = string.IsNullOrEmpty(path) ? name : path.EndsWith('/') ? path.TrimEnd('/') + "/" + name : path;
        }
        if (string.IsNullOrEmpty(path) || path.EndsWith('/'))
            throw new ArgumentException("Specify a non-root file path.");
        var target = ResolveDirectory(path, FileObject?.Share);
        if (target.Path.Length == 0 || FileObject is not null && Path is not null && Path.Trim('/') != target.Path)
            throw new ArgumentException("The file path conflicts with the native file.");
        if (source is not null && target.Share.ShareSnapshot is not null)
            throw new ArgumentException("Uploads cannot target a share snapshot.");
        return new StorageFile(target.Path, target.Share);
    }

    private void RequireNativeContext()
    {
        if (Context is null && ShareObject is null && DirectoryObject is null && FileObject is null)
            throw new ArgumentException("Specify a native Storage context or Files resource.");
    }

    protected override Task ExecuteAsync() => throw new NotSupportedException("Use the synchronous preflight methods.");
}

public sealed class UploadStorageFilePowerShellCommand : FileShareWorkflowBase
{
    public string Source { get; set; } = string.Empty;
    public bool PreflightOnly { get; set; } = true;
    public bool Force { get; set; }
    public bool PassThru { get; set; }
    public bool ValidateContent { get; set; }
    public bool StoreContentMd5 { get; set; } = true;
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = UploadStorageFileProcessor.MaximumRangeBytes;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }

    protected override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(Source))
            throw new ArgumentException("Specify a local source file.");
        string source = System.IO.Path.GetFullPath(Source);
        if (!File.Exists(source)) throw new FileNotFoundException("The upload source file does not exist.", source);
        if (Concurrency is < 1 or > 4 || ChunkSize is < 1 or > UploadStorageFileProcessor.MaximumRangeBytes ||
            new FileInfo(source).Length > UploadStorageFileProcessor.MaximumFileBytes ||
            ContentMd5 is { Length: not 16 })
            throw new ArgumentException("Specify valid bounded transfer options, file size and a 16-byte Content-MD5.");
        foreach (string? value in new[] { ContentType, ContentEncoding, ContentLanguage, ContentDisposition, CacheControl })
            if (value?.Any(character => character < 0x20 || character > 0x7e) == true)
                throw new ArgumentException("Content settings must contain printable ASCII header values.");
        using (var validation = new HttpRequestMessage()) AddMetadataHeaders(validation, Metadata);
        string name = System.IO.Path.GetFileName(source);
        var directory = ResolveDirectory(Path);
        if (directory.Share.ShareSnapshot is not null)
            throw new ArgumentException("Uploads cannot target share snapshots.");
        if (PreflightOnly)
        {
            bool isDirectory = string.IsNullOrEmpty(Path) || Path.EndsWith('/');
            if (!isDirectory)
                isDirectory = await IsDirectoryAsync(directory);
            if (isDirectory)
                directory = new StorageFileDirectory(
                    directory.Path.Length == 0 ? name : directory.Path + "/" + name, directory.Share);
            bool exists = !Force && await ExistsAsync(directory);
            Token.ThrowIfCancellationRequested();
            // No PSCmdlet callback is made on a pipeline worker, and no write precedes approval.
            throw new StorageFileUploadPreflightException(new StorageFile(directory.Path, directory.Share), exists);
        }
        if (directory.Path.Length == 0 || string.IsNullOrEmpty(Path) || Path.EndsWith('/'))
            throw new ArgumentException("The approved upload must identify a frozen exact file path.");
        var child = CreateExtendedProcessor<UploadStorageFileProcessor>(processor =>
        {
            processor.Context = directory.Context;
            processor.Profile = directory.Context.Profile.Name;
            processor.ShareObject = directory.Share;
            processor.Path = directory.Path;
            processor.Source = source;
            processor.Timeout = Timeout;
            processor.AllowTrailingDot = AllowTrailingDot;
            processor.Concurrency = Concurrency;
            processor.ChunkSize = ChunkSize;
            processor.ValidateContent = ValidateContent;
            processor.StoreContentMd5 = StoreContentMd5;
            processor.PassThru = PassThru;
            processor.ContentType = ContentType;
            processor.ContentEncoding = ContentEncoding;
            processor.ContentLanguage = ContentLanguage;
            processor.ContentDisposition = ContentDisposition;
            processor.CacheControl = CacheControl;
            processor.ContentMd5 = ContentMd5;
            processor.Metadata = Metadata;
            processor.DeferFailureDiagnostics = true;
        });
        await child.ProcessAsync();
    }

    private async Task<bool> ExistsAsync(StorageFileDirectory target)
    {
        try
        {
            var probe = CreateFileProcessor<FileProcessors.GetPropertiesFileProcessor>(target, processor =>
            {
                processor.Url = target.Uri.AbsoluteUri;
                processor.Timeout = Timeout;
                processor.AllowTrailingDot = AllowTrailingDot;
                processor.DeferFailureDiagnostics = true;
            });
            using var response = await probe.SendAsync();
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidDataException("Storage did not confirm file properties.");
            return true;
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "ResourceNotFound" or "ParentNotFound")
        {
            return false;
        }
    }
}
