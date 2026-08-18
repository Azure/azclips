// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

public sealed record BlobTransferResult(
    string Operation,
    string Source,
    string Destination,
    string? ETag,
    string? LastModified,
    long? ContentLength);
