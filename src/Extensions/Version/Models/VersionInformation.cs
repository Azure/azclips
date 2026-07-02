// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Version.Models;

/// <summary>
/// Aggregated version information for AzCli, runtime, and configuration paths.
/// </summary>
public class VersionInformation
{
    public string AzCli { get; set; } = string.Empty;
    public RuntimeInfo Runtime { get; set; } = new();
    public PathInfo Paths { get; set; } = new();
}
