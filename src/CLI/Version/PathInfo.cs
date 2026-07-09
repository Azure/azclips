// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Version;

/// <summary>
/// Filesystem paths relevant to the CLI installation and configuration.
/// </summary>
internal sealed class PathInfo
{
    public string InstallLocation { get; set; } = string.Empty;
    public string ConfigLocation { get; set; } = string.Empty;
    public string ExtensionsLocation { get; set; } = string.Empty;
}
