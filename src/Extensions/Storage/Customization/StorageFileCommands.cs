// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class FileCliCommandBase : FileShareWorkflowBase
{
    public string AuthMode { get; set; } = "login";
    public string? FileEndpoint { get; set; }
    public bool DisallowTrailingDot { get; set; }

    protected T CreateWorkflow<T>(Action<T> configure) where T : FileShareWorkflowBase, new()
    {
        if (!string.Equals(AuthMode, "login", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Azure Files supports auth-mode login only; no account-key fallback is performed.");
        string? account = StorageAccountName;
        if (FileEndpoint is not null)
        {
            if (!Uri.TryCreate(FileEndpoint, UriKind.Absolute, out var endpoint))
                throw new ArgumentException("Specify an HTTPS public Azure Files account endpoint.");
            string endpointAccount = StorageContext.ValidatePublicUri(endpoint, StorageService.Files);
            if (endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || account is not null && account != endpointAccount)
                throw new ArgumentException("The Files endpoint must be an account root consistent with account-name.");
            account = endpointAccount;
        }
        if (StorageAccountName is null) StorageAccountName = account;
        var directory = ResolveDirectory();
        if (account is not null && directory.Context.StorageAccountName != account)
            throw new ArgumentException("The Files endpoint conflicts with the native context.");
        return CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = directory.Context;
            processor.Profile = directory.Context.Profile.Name;
            processor.ShareObject = directory.Share;
            processor.DirectoryObject = directory;
            processor.Path = Path;
            processor.Timeout = Timeout;
            processor.AllowTrailingDot = !DisallowTrailingDot;
            processor.DeferFailureDiagnostics = true;
            configure(processor);
        });
    }
}

public sealed class UploadStorageFileCommand : FileCliCommandBase
{
    public string Source { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public string? ContentMd5Input { get; set; }
    public string[]? MetadataInput { get; set; }
    public bool ValidateContent { get; set; }
    public bool NoProgress { get; set; }
    public int Concurrency { get; set; } = 2;

    protected override Task ExecuteAsync()
    {
        var metadata = StorageCliKeyValue.Parse(MetadataInput, tags: false);
        byte[]? md5 = StorageContentSettings.ParseMd5(ContentMd5Input);
        return CreateWorkflow<UploadStorageFileProcessor>(processor =>
        {
            processor.Source = Source;
            processor.ContentType = ContentType ?? StorageContentSettings.InferContentType(Source);
            processor.ContentEncoding = ContentEncoding;
            processor.ContentLanguage = ContentLanguage;
            processor.ContentDisposition = ContentDisposition;
            processor.CacheControl = CacheControl;
            processor.ContentMd5 = md5;
            processor.Metadata = metadata;
            processor.ValidateContent = ValidateContent;
            processor.NoProgress = NoProgress;
            processor.SuppressProgress = SuppressProgress || NoProgress;
            processor.Concurrency = Concurrency;
            processor.ReturnResponseMetadata = true;
            processor.ProbeDirectoryTarget = true;
            processor.OutputOverride = (value, token) =>
            {
                var result = (StorageFileUploadResult)value!;
                return WriteOutput(JsonSerializer.SerializeToElement(new
                {
                    etag = result.ETag, lastModified = result.LastModified,
                    isServerEncrypted = result.IsServerEncrypted, contentMd5 = result.ContentMd5
                }), token);
            };
        }).ProcessAsync();
    }
}

public sealed class DownloadStorageFileCommand : FileCliCommandBase
{
    public string? Destination { get; set; }
    public string OpenMode { get; set; } = "wb";
    public bool ValidateContent { get; set; }
    public bool NoProgress { get; set; }
    public int Concurrency { get; set; } = 2;
    public long? StartRange { get; set; }
    public long? EndRange { get; set; }

    protected override Task ExecuteAsync()
    {
        if (OpenMode is not ("wb" or "ab"))
            throw new ArgumentException("OpenMode must be wb (replace) or ab (append).");
        string destination = StorageFileFrontend.ResolveDestination(Destination, Path);
        return CreateWorkflow<DownloadStorageFileProcessor>(processor =>
        {
            processor.Destination = destination;
            processor.Overwrite = OpenMode == "wb";
            processor.Append = OpenMode == "ab";
            processor.Concurrency = Concurrency;
            processor.StartRange = StartRange;
            processor.EndRange = EndRange;
            processor.ValidateContent = ValidateContent;
            processor.NoProgress = NoProgress;
            processor.SuppressProgress = SuppressProgress || NoProgress;
            processor.ReturnProperties = true;
            processor.OutputOverride = (value, token) => WriteOutput(StorageFileFrontend.Project((StorageFileEntry)value!), token);
        }).ProcessAsync();
    }
}

public class ListStorageFilesCommand : FileCliCommandBase
{
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public string NumResults { get; set; } = "*";
    public bool ShowNextMarker { get; set; }
    public bool ExcludeExtendedInfo { get; set; }
    protected virtual bool DirectoriesOnly => false;

    protected override async Task ExecuteAsync()
    {
        long? limit = NumResults == "*" ? null :
            long.TryParse(NumResults, NumberStyles.None, CultureInfo.InvariantCulture, out long count) && count > 0
                ? count : throw new ArgumentException("NumResults must be a positive 64-bit integer or *.");
        var results = new List<JsonElement>();
        var child = CreateWorkflow<ListStorageFilesProcessor>(processor =>
        {
            processor.Prefix = Prefix;
            processor.Marker = Marker;
            processor.NumResults = limit;
            processor.ShowNextMarker = ShowNextMarker;
            processor.ExcludeExtendedInfo = ExcludeExtendedInfo;
            processor.DirectoriesOnly = DirectoriesOnly;
            processor.OutputOverride = (value, token) =>
            {
                token.ThrowIfCancellationRequested();
                results.Add(value is StorageFileEntry entry ? StorageFileFrontend.Project(entry) :
                    JsonSerializer.SerializeToElement((StorageFileNextMarker)value!));
                return Task.CompletedTask;
            };
        });
        await child.ProcessAsync();
        Token.ThrowIfCancellationRequested();
        await WriteOutput(results.ToArray(), Token);
    }
}

public sealed class ListStorageDirectoriesCommand : ListStorageFilesCommand
{
    protected override bool DirectoriesOnly => true;
}

public static class StorageFileFrontend
{
    public static string ResolveDestination(string? destination, string? remotePath)
    {
        if (string.IsNullOrEmpty(remotePath) || remotePath.EndsWith('/'))
            throw new ArgumentException("Specify a source file path.");
        string name = remotePath[(remotePath.LastIndexOf('/') + 1)..];
        bool useRemoteName = string.IsNullOrEmpty(destination) || Directory.Exists(destination);
        if (useRemoteName) StorageDownload.ValidateFileName(name);
        string result = string.IsNullOrEmpty(destination) ? name :
            useRemoteName ? System.IO.Path.Combine(destination, name) : destination;
        return StorageDownload.ResolveDestination(result, allowMissingParent: true);
    }

    internal static JsonElement Project(StorageFileEntry entry) => JsonSerializer.SerializeToElement(new
    {
        name = entry.Name,
        isDirectory = entry.IsDirectory,
        properties = new
        {
            contentLength = entry.Properties.ContentLength,
            etag = entry.Properties.ETag,
            lastModified = entry.Properties.LastModified,
            creationTime = entry.Properties.CreationTime,
            lastAccessTime = entry.Properties.LastAccessTime,
            lastWriteTime = entry.Properties.LastWriteTime,
            changeTime = entry.Properties.ChangeTime,
            fileId = entry.Properties.FileId,
            parentId = entry.Properties.ParentId,
            fileAttributes = entry.Properties.Attributes,
            filePermissionKey = entry.Properties.PermissionKey,
            contentSettings = new
            {
                contentType = entry.Properties.ContentType,
                contentEncoding = entry.Properties.ContentEncoding,
                contentLanguage = entry.Properties.ContentLanguage,
                contentDisposition = entry.Properties.ContentDisposition,
                cacheControl = entry.Properties.CacheControl,
                contentMd5 = entry.Properties.ContentMd5
            }
        },
        metadata = entry.Properties.Metadata
    });
}
