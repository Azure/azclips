// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Models
{
    /// <summary>The results of the file transfer commands. The spec defines these responses only as headers.</summary>
    public class FileCopyResult
    {
        /// <summary>The copy ID, used to poll the copy or abort it.</summary>
        [JsonPropertyName("copyId")]
        public string? CopyId { get; set; }

        /// <summary>The copy state: <c>pending</c>, or <c>success</c> when the service finished it synchronously.</summary>
        [JsonPropertyName("copyStatus")]
        public string? CopyStatus { get; set; }

        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        internal static FileCopyResult From(HttpResponseMessage response)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            return new FileCopyResult
            {
                CopyId = headers.GetValueOrDefault("x-ms-copy-id"),
                CopyStatus = headers.GetValueOrDefault("x-ms-copy-status"),
                ETag = headers.GetValueOrDefault("ETag"),
                LastModified = headers.GetValueOrDefault("Last-Modified"),
            };
        }
    }

    public class FileUploadResult
    {
        /// <summary>Bytes uploaded.</summary>
        [JsonPropertyName("contentLength")]
        public long ContentLength { get; set; }

        /// <summary>The file ETag after the last write.</summary>
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>The file's last-modified date-time after the last write.</summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        internal static FileUploadResult From(HttpResponseMessage response, long contentLength)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            return new FileUploadResult
            {
                ContentLength = contentLength,
                ETag = headers.GetValueOrDefault("ETag"),
                LastModified = headers.GetValueOrDefault("Last-Modified"),
            };
        }
    }

    public class FileDownloadResult
    {
        /// <summary>The local file the content was written to.</summary>
        [JsonPropertyName("file")]
        public string? File { get; set; }

        /// <summary>Bytes written.</summary>
        [JsonPropertyName("contentLength")]
        public long ContentLength { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        internal static FileDownloadResult From(HttpResponseMessage response, string file, long contentLength)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            return new FileDownloadResult
            {
                File = file,
                ContentLength = contentLength,
                ContentType = headers.GetValueOrDefault("Content-Type"),
                ETag = headers.GetValueOrDefault("ETag"),
                LastModified = headers.GetValueOrDefault("Last-Modified"),
            };
        }
    }
}
