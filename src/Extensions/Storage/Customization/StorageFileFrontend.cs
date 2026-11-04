// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

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
