// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Version.Models;

/// <summary>
/// Runtime environment information (.NET runtime, OS, and architecture).
/// </summary>
public class RuntimeInfo
{
    public string DotNet { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
}
