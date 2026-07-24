// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

/// <summary>Single tag attributes (response for show).</summary>
public class TagAttributes
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public TagAttributeBase? Tag { get; set; }
}

/// <summary>Tag detail — maps to TagAttributesBase in swagger.</summary>
public class TagAttributeBase
{
    public string? Name { get; set; }
    public string? Digest { get; set; }
    public string? CreatedTime { get; set; }
    public string? LastUpdateTime { get; set; }
    public bool? Signed { get; set; }
    public ChangeableAttributes? ChangeableAttributes { get; set; }
}

/// <summary>Paginated tags list response.</summary>
public class TagList
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public TagAttributeBase[]? Tags { get; set; }
}
