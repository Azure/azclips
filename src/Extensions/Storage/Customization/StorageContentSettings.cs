// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageContentSettings
{
    internal static byte[]? ParseMd5(string? value)
    {
        if (value is null) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { throw new ArgumentException("Content-MD5 must be a Base64-encoded 16-byte value."); }
        if (bytes.Length != 16) throw new ArgumentException("Content-MD5 must contain exactly 16 bytes.");
        return bytes;
    }

    internal static string InferContentType(string path)
    {
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
        ".txt" or ".log" or ".ini" => "text/plain",
        ".csv" => "text/csv",
        ".htm" or ".html" => "text/html",
        ".css" => "text/css",
        ".js" or ".mjs" => "application/javascript",
        ".json" or ".map" => "application/json",
        ".xml" or ".xsl" => "application/xml",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        ".tar" => "application/x-tar",
        ".7z" => "application/x-7z-compressed",
        ".gz" or ".svgz" => "application/gzip",
        ".bz2" => "application/x-bzip2",
        ".xz" => "application/x-xz",
        ".z" => "application/x-compress",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".ico" => "image/vnd.microsoft.icon",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/x-wav",
        ".ogg" => "audio/ogg",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mpeg" or ".mpg" => "video/mpeg",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".wasm" => "application/wasm",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
        };
    }
}
