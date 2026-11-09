// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageBlobCopyStartResult(string CopyId, string CopyStatus, string ETag, DateTimeOffset LastModified);
