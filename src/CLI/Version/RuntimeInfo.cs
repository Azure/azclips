// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Version;

/// <summary>
/// Runtime environment information (.NET runtime, OS, and architecture).
/// </summary>
internal sealed class RuntimeInfo
{
    public string DotNet { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
}
