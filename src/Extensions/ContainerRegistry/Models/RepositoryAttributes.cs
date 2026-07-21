// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

public class RepositoryAttributes
{
    public string? Registry { get; set; }
    public string? ImageName { get; set; }
    public string? CreatedTime { get; set; }
    public string? LastUpdateTime { get; set; }
    public int? ManifestCount { get; set; }
    public int? TagCount { get; set; }
    public ChangeableAttributes? ChangeableAttributes { get; set; }
}

public class RepositoryList
{
    public string[]? Repositories { get; set; }
}
