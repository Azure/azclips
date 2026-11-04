// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageFileUploadResult(string ETag, DateTimeOffset LastModified, bool? IsServerEncrypted, byte[]? ContentMd5);
