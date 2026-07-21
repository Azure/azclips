// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

public class ManifestAttributes
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public ManifestAttributeBase? Manifest { get; set; }
}

public class ManifestAttributeBase
{
    public string? Digest { get; set; }
    public long? ImageSize { get; set; }
    public string? CreatedTime { get; set; }
    public string? LastUpdateTime { get; set; }
    public string? Architecture { get; set; }
    public string? Os { get; set; }
    public string? MediaType { get; set; }
    public string? ConfigMediaType { get; set; }
    public string[]? Tags { get; set; }
    public ChangeableAttributes? ChangeableAttributes { get; set; }
}

public class ManifestList
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public ManifestAttributeBase[]? Manifests { get; set; }
}
