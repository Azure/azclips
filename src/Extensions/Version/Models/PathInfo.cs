// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Version.Models;

/// <summary>
/// Filesystem paths relevant to the CLI installation and configuration.
/// </summary>
public class PathInfo
{
    public string Install { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
}
