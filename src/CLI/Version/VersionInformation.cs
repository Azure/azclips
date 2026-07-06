// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Version;

/// <summary>
/// Aggregated version information for AzCli, runtime, and configuration paths.
/// </summary>
internal sealed class VersionInformation
{
    public string AzCli { get; set; } = string.Empty;
    public RuntimeInfo Runtime { get; set; } = new();
    public PathInfo Paths { get; set; } = new();
}
