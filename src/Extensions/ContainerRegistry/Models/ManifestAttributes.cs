// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

/// <summary>Single manifest attributes (response for show).</summary>
public class ManifestAttributes
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public ManifestAttributeBase? Manifest { get; set; }
}

/// <summary>Manifest detail — maps to ManifestAttributesBase in swagger.</summary>
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
    public ManifestReference[]? References { get; set; }
    public ChangeableAttributes? ChangeableAttributes { get; set; }
}

/// <summary>Paginated manifests list response.</summary>
public class ManifestList
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public ManifestAttributeBase[]? Manifests { get; set; }
}

/// <summary>Reference to a related manifest (for multi-arch images).</summary>
public class ManifestReference
{
    public string? Digest { get; set; }
    public string? Architecture { get; set; }
    public string? Os { get; set; }
}
