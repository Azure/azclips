// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Cirrus.Extensions.Version.Models;

/// <summary>
/// Aggregated version information for AzCli, runtime, and configuration paths.
/// </summary>
public class VersionInformation
{
    public string AzCli { get; set; } = string.Empty;
    public RuntimeInfo Runtime { get; set; } = new();
    public PathInfo Paths { get; set; } = new();

}

/// <summary>
/// Runtime environment information (.NET runtime, OS, and architecture).
/// </summary>
public class RuntimeInfo
{
    public string DotNet { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
}

/// <summary>
/// Filesystem paths relevant to the CLI installation and configuration.
/// </summary>
public class PathInfo
{
    public string Install { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
    public string Extensions { get; set; } = string.Empty;
}
