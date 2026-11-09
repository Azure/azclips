// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public interface IBlobContainerOptions
{
    StorageContext? Context { get; set; }
    string? Profile { get; set; }
    string? StorageAccountName { get; set; }
    string? Container { get; set; }
    StorageBlobContainer? ContainerObject { get; set; }
    int? Timeout { get; set; }
}

public interface IBlobTargetOptions : IBlobContainerOptions
{
    string? Blob { get; set; }
    StorageBlob? InputObject { get; set; }
    string? Snapshot { get; set; }
    string? VersionId { get; set; }
    string? LeaseId { get; set; }
    string? IfMatch { get; set; }
    string? IfNoneMatch { get; set; }
    DateTimeOffset? IfModifiedSince { get; set; }
    DateTimeOffset? IfUnmodifiedSince { get; set; }
    string? IfTags { get; set; }
}

public interface IBlobCliProcessor : IBlobTargetOptions, Core.Abstractions.IProcessor
{
    string AuthMode { get; set; }
    string? BlobEndpoint { get; set; }
    string? IfModifiedSinceInput { get; set; }
    string? IfUnmodifiedSinceInput { get; set; }
}

public interface IBlobUploadProcessor : IBlobTargetOptions, Core.Abstractions.IProcessor
{
    string File { get; set; }
    bool Overwrite { get; set; }
    bool ValidateContent { get; set; }
    int Concurrency { get; set; }
    int ChunkSize { get; set; }
    string? ContentType { get; set; }
    string? ContentEncoding { get; set; }
    string? ContentLanguage { get; set; }
    string? ContentDisposition { get; set; }
    string? CacheControl { get; set; }
    byte[]? ContentMd5 { get; set; }
    IReadOnlyDictionary<string, string>? Metadata { get; set; }
    IReadOnlyDictionary<string, string>? Tags { get; set; }
    string? Tier { get; set; }
}

public interface IFileShareOptions
{
    StorageContext? Context { get; set; }
    string? Profile { get; set; }
    string? StorageAccountName { get; set; }
    string? ShareName { get; set; }
    StorageFileShare? ShareObject { get; set; }
    StorageFileDirectory? DirectoryObject { get; set; }
    string? Path { get; set; }
    string? ShareSnapshot { get; set; }
    bool EnableFileBackupRequestIntent { get; set; }
    bool? AllowTrailingDot { get; set; }
    int? Timeout { get; set; }
}

public interface IFileCliOptions : IFileShareOptions
{
    string AuthMode { get; set; }
    string? FileEndpoint { get; set; }
    bool DisallowTrailingDot { get; set; }
}

internal sealed class BlobTargetOptions : IBlobTargetOptions
{
    public StorageContext? Context { get; set; }
    public string? Profile { get; set; }
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
}
