// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageFileShare(string Name, StorageContext Context, string? ShareSnapshot = null)
{
    public Uri Uri => GetUri(string.Empty);
    internal Uri GetUri(string path) => new(Context.GetFileUri(Name, path.Length == 0 ? null : path).AbsoluteUri
        .WithQuery("sharesnapshot", ShareSnapshot));
}

public abstract record StorageFileEntry(string Path, StorageFileShare Share)
{
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];
    public bool IsDirectory => this is StorageFileDirectory;
    public StorageContext Context => Share.Context;
    public Uri Uri => Share.GetUri(Path);
    public StorageFileProperties Properties { get; init; } = new();
    public StorageFileContinuationToken? ContinuationToken { get; init; }
}

public sealed record StorageFile(string Path, StorageFileShare Share) : StorageFileEntry(Path, Share);
public sealed record StorageFileDirectory(string Path, StorageFileShare Share) : StorageFileEntry(Path, Share);

public sealed record StorageFileProperties
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    public long? ContentLength { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset? CreationTime { get; init; }
    public DateTimeOffset? LastAccessTime { get; init; }
    public DateTimeOffset? LastWriteTime { get; init; }
    public DateTimeOffset? ChangeTime { get; init; }
    public string? FileId { get; init; }
    public string? ParentId { get; init; }
    public string? Attributes { get; init; }
    public string? PermissionKey { get; init; }
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public string? ContentLanguage { get; init; }
    public string? ContentDisposition { get; init; }
    public string? CacheControl { get; init; }
    public byte[]? ContentMd5 { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = EmptyMetadata;

    internal static StorageFileProperties FromHeaders(StorageResourceProperties headers, bool directory)
    {
        string? Header(string name) => headers.Headers.TryGetValue(name, out string? value) ? value : null;
        if (headers.ETag is null || headers.LastModified is null ||
            !directory && (headers.ContentLength is null or < 0 || Header("x-ms-type") != "File"))
            throw new InvalidDataException("Storage did not return valid file/directory properties.");
        byte[]? md5 = null;
        if (!directory && Header("Content-MD5") is { } value)
        {
            try { md5 = Convert.FromBase64String(value); }
            catch (FormatException) { throw new InvalidDataException("Storage returned an invalid file Content-MD5."); }
            if (md5.Length != 16) throw new InvalidDataException("Storage returned an invalid file Content-MD5 length.");
        }
        return new StorageFileProperties
        {
            ContentLength = directory ? null : headers.ContentLength, ETag = headers.ETag, LastModified = headers.LastModified,
            CreationTime = Date(Header("x-ms-file-creation-time")), LastWriteTime = Date(Header("x-ms-file-last-write-time")),
            ChangeTime = Date(Header("x-ms-file-change-time")), FileId = Header("x-ms-file-id"), ParentId = Header("x-ms-file-parent-id"),
            Attributes = Header("x-ms-file-attributes"), PermissionKey = Header("x-ms-file-permission-key"),
            ContentType = directory ? null : headers.ContentType, ContentEncoding = Header("Content-Encoding"),
            ContentLanguage = Header("Content-Language"), ContentDisposition = Header("Content-Disposition"),
            CacheControl = Header("Cache-Control"), ContentMd5 = md5, Metadata = headers.Metadata
        };
    }

    internal static DateTimeOffset? Date(string? value) => value is null ? null :
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date : throw new InvalidDataException("Storage returned an invalid file timestamp.");
}

public sealed record StorageFileContinuationToken(string NextMarker, string StorageAccountName, string Share,
    string Path, string? ShareSnapshot, string? Prefix);
public sealed record StorageFileNextMarker([property: JsonPropertyName("nextMarker")] string? NextMarker);
