// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStorageFileProcessor : FileShareWorkflowBase
{
    internal const int MaximumRangeBytes = 4 * 1024 * 1024;
    internal const long MaximumFileBytes = 4L * 1024 * 1024 * 1024 * 1024;
    public string Source { get; set; } = string.Empty;
    public bool ValidateContent { get; set; }
    public bool NoProgress { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = MaximumRangeBytes;
    public string? LeaseId { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public bool PassThru { get; set; }
    internal bool ReturnResponseMetadata { get; set; }
    internal bool StoreContentMd5 { get; set; }
    internal bool ProbeDirectoryTarget { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;
    internal Func<string, CancellationToken, Task<bool>>? ConfirmOverwrite { get; set; }

    protected override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(Source))
            throw new ArgumentException("Specify a source file.");
        string sourcePath = System.IO.Path.GetFullPath(Source);
        string name = System.IO.Path.GetFileName(sourcePath);
        string relativePath = string.IsNullOrEmpty(Path) ? name : Path.EndsWith('/') ? Path.TrimEnd('/') + "/" + name : Path;
        var target = ResolveDirectory(relativePath);
        if (target.Path.Length == 0 || target.Share.ShareSnapshot is not null)
            throw new ArgumentException("Uploads must target a file in a live share, not the root or a snapshot.");
        if (Concurrency is < 1 or > 4 || ChunkSize is < 1 or > MaximumRangeBytes || PassThru && ReturnResponseMetadata)
            throw new ArgumentException("Use 1-4 workers, chunks of 1 byte through 4 MiB and one output mode.");
        var settings = new Settings(ContentType ?? "application/octet-stream", ContentEncoding, ContentLanguage,
            ContentDisposition, CacheControl, ContentMd5?.ToArray());
        if (settings.Md5 is { Length: not 16 })
            throw new ArgumentException("Content-MD5 must contain exactly 16 bytes.");
        foreach (string? value in new[] { settings.Type, settings.Encoding, settings.Language, settings.Disposition, settings.CacheControl, LeaseId })
            if (value?.Any(character => character < 0x20 || character > 0x7e) == true)
                throw new ArgumentException("Content settings and lease IDs must contain printable ASCII header values.");
        if (LeaseId == string.Empty) throw new ArgumentException("Lease ID cannot be empty.");
        var metadata = Metadata is null ? null : new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase));
        using (var validation = new HttpRequestMessage()) AddMetadataHeaders(validation, metadata);
        if (ProbeDirectoryTarget && relativePath == Path && await IsDirectoryAsync(target))
            target = new StorageFileDirectory(target.Path + "/" + name, target.Share);
        if (!ShouldProcess(target.Uri.AbsoluteUri, "Upload file")) return;

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        ValidateLength(length);
        if (ConfirmOverwrite is not null)
        {
            var probe = PropertiesProcessor(target);
            probe.DeferFailureDiagnostics = true;
            bool exists;
            try
            {
                using var response = await probe.SendAsync();
                exists = true;
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound &&
                exception.Data["ServiceErrorCode"] is "ResourceNotFound")
            {
                exists = false;
            }
            if (exists && !await ConfirmOverwrite(target.Uri.AbsoluteUri, Token)) return;
        }

        StorageFileUploadResult result;
        string? fileId;
        HttpResponseMessage created;
        try
        {
            created = await CreateFileAsync(target, length, metadata, settings, deferFailure: true);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound &&
            exception.Data["ServiceErrorCode"] is "ParentNotFound")
        {
            await CreateParentDirectoriesAsync(target);
            created = await CreateFileAsync(target, length, metadata, settings, deferFailure: DeferFailureDiagnostics);
        }
        using (created)
        {
            result = ReadResult(created, HttpStatusCode.Created);
            fileId = Header(created, "x-ms-file-id");
        }
        using var hash = StoreContentMd5 && settings.Md5 is null ? StorageWireFormat.CreateContentMd5Hash() : null;
        long transferred = 0;
        while (transferred < length)
        {
            Token.ThrowIfCancellationRequested();
            int count = (int)Math.Min(Concurrency, (length - transferred - 1) / ChunkSize + 1);
            var chunks = new byte[count][];
            long cursor = transferred;
            for (int index = 0; index < count; index++)
            {
                chunks[index] = await StorageWireFormat.ReadFileChunkAsync(source, cursor, (int)Math.Min(ChunkSize, length - cursor), Token);
                hash?.AppendData(chunks[index]);
                cursor += chunks[index].Length;
            }
            var tasks = new Task<StorageFileUploadResult>[count];
            cursor = transferred;
            for (int index = 0; index < count; index++)
            {
                tasks[index] = UploadRangeAsync(target, cursor, chunks[index]);
                cursor += chunks[index].Length;
            }
            var responses = await Task.WhenAll(tasks);
            result = responses[^1];
            transferred = cursor;
            await PublishProgressAsync(transferred, length, completed: false);
        }
        Token.ThrowIfCancellationRequested();
        if (source.Length != length) throw new IOException("The source file length changed during upload.");
        byte[]? storedHash = settings.Md5 ?? hash?.GetHashAndReset();
        if (storedHash is not null)
        {
            var headers = CreateFileProcessor<FileProcessors.SetHttpHeadersFileProcessor>(target, processor =>
            {
                processor.Url = target.Uri.AbsoluteUri; processor.Timeout = Timeout;
                processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
                processor.FileContentType = settings.Type; processor.FileContentEncoding = settings.Encoding;
                processor.FileContentLanguage = settings.Language; processor.FileContentDisposition = settings.Disposition;
                processor.FileCacheControl = settings.CacheControl; processor.FileContentMD5 = storedHash;
                processor.FilePermission = "preserve"; processor.FileAttributes = "preserve";
                processor.FileCreationTime = "preserve"; processor.FileLastWriteTime = "preserve";
            });
            using var response = await headers.SendAsync();
            result = ReadResult(response, HttpStatusCode.OK) with { ContentMd5 = storedHash };
        }
        StorageFile? file = null;
        if (PassThru)
        {
            using var response = await PropertiesProcessor(target).SendAsync();
            var properties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(response), directory: false);
            if (properties.ContentLength != length || fileId is not null && properties.FileId != fileId ||
                storedHash is not null && (properties.ETag != result.ETag || properties.ContentMd5 is null ||
                    !properties.ContentMd5.AsSpan().SequenceEqual(storedHash)))
                throw new InvalidDataException("Storage returned unexpected uploaded file properties.");
            file = new StorageFile(target.Path, target.Share) { Properties = properties };
        }
        Token.ThrowIfCancellationRequested();
        await PublishProgressAsync(length, length, completed: true);
        if (file is not null) await WriteOutput(file, Token);
        else if (ReturnResponseMetadata) await WriteOutput(result, Token);
    }

    private Task<HttpResponseMessage> CreateFileAsync(StorageFileDirectory target, long length,
        IReadOnlyDictionary<string, string>? metadata, Settings settings, bool deferFailure) =>
        CreateFileProcessor<FileProcessors.CreateFileProcessor>(target, processor =>
        {
            processor.Url = target.Uri.AbsoluteUri; processor.FileContentLength = length; processor.FileType = "file";
            processor.Body = []; processor.ContentLength = 0; processor.Metadata = metadata;
            processor.Timeout = Timeout; processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
            processor.FilePermission = "inherit"; processor.FileAttributes = "Archive";
            processor.FileCreationTime = "now"; processor.FileLastWriteTime = "now";
            processor.FileContentType = settings.Type; processor.FileContentEncoding = settings.Encoding;
            processor.FileContentLanguage = settings.Language; processor.FileContentDisposition = settings.Disposition;
            processor.FileCacheControl = settings.CacheControl;
            processor.DeferFailureDiagnostics = deferFailure;
        }).SendAsync();

    private async Task CreateParentDirectoriesAsync(StorageFileDirectory target)
    {
        string[] segments = target.Path.Split('/');
        for (int index = 1; index < segments.Length; index++)
        {
            Token.ThrowIfCancellationRequested();
            var directory = new StorageFileDirectory(string.Join('/', segments, 0, index), target.Share);
            var child = CreateFileProcessor<FileProcessors.CreateDirectoryProcessor>(directory, processor =>
            {
                processor.Url = directory.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.AllowTrailingDot = AllowTrailingDot;
                processor.FilePermission = "inherit"; processor.FileAttributes = "Directory";
                processor.FileCreationTime = "now"; processor.FileLastWriteTime = "now";
                processor.DeferFailureDiagnostics = true;
            });
            try
            {
                using var response = await child.SendAsync();
                if (response.StatusCode != HttpStatusCode.Created)
                    throw new InvalidDataException("Storage did not confirm directory creation with HTTP 201.");
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict &&
                exception.Data["ServiceErrorCode"] is "ResourceAlreadyExists")
            {
            }
        }
    }

    internal static void ValidateLength(long length)
    {
        if (length is < 0 or > MaximumFileBytes)
            throw new ArgumentOutOfRangeException(nameof(length), "Files uploads support lengths of zero through 4 TiB.");
    }

    private async Task<StorageFileUploadResult> UploadRangeAsync(StorageFileDirectory target, long offset, byte[] bytes)
    {
        var child = CreateFileProcessor<FileProcessors.UploadRangeFileProcessor>(target, processor =>
        {
            processor.Url = target.Uri.AbsoluteUri; processor.Timeout = Timeout; processor.Body = bytes;
            processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{offset + bytes.Length - 1}");
            processor.FileRangeWrite = "update"; processor.ContentLength = bytes.Length;
            processor.ContentMD5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
            processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
        });
        using var response = await child.SendAsync();
        return ReadResult(response, HttpStatusCode.Created);
    }

    private FileProcessors.GetPropertiesFileProcessor PropertiesProcessor(StorageFileDirectory target) =>
        CreateFileProcessor<FileProcessors.GetPropertiesFileProcessor>(target, processor =>
        {
            processor.Url = target.Uri.AbsoluteUri; processor.Timeout = Timeout;
            processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
        });

    private static StorageFileUploadResult ReadResult(HttpResponseMessage response, HttpStatusCode expected)
    {
        var properties = StorageResourceProperties.FromResponse(response);
        if (response.StatusCode != expected || properties.ETag is null || properties.LastModified is null)
            throw new InvalidDataException("Storage did not confirm the file upload operation with valid properties.");
        bool? encrypted = Header(response, "x-ms-request-server-encrypted") switch
        {
            null => null, "true" => true, "false" => false,
            _ => throw new InvalidDataException("Storage returned an invalid file encryption flag.")
        };
        return new StorageFileUploadResult(properties.ETag, properties.LastModified.Value, encrypted, null);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    private Task PublishProgressAsync(long transferred, long total, bool completed) =>
        NoProgress || SuppressProgress ? Task.CompletedTask : Publisher.Publish(Events.Progress, Token, new EventData
        {
            Id = Events.Progress, Message = completed ? "File upload completed." : $"Uploaded {transferred} of {total} bytes.",
            Value = completed ? 100 : total == 0 ? 0 : Math.Min(99, transferred * 100.0 / total), Cancel = Cancel
        });

    private sealed record Settings(string Type, string? Encoding, string? Language, string? Disposition, string? CacheControl, byte[]? Md5);
}

public sealed record StorageFileUploadResult(string ETag, DateTimeOffset LastModified, bool? IsServerEncrypted, byte[]? ContentMd5);
