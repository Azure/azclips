// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

public class BlobTransferResult
{
    public string Operation { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public long? ContentLength { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
