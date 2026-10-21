// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Extensions;

/// <summary>Command ownership without processor types or parameter metadata.</summary>
public sealed record ExtensionRoutingInfo(
    string PackageId,
    string ResolvedVersion,
    IReadOnlyList<string> Verbs,
    IReadOnlyList<string> Leaves,
    string? Location)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ExtensionInstallation? Installation { get; init; }

    public static ExtensionRoutingInfo FromDiscovery(DiscoveredExtensionInfo info)
        => new(info.PackageId, info.ResolvedVersion, info.Verbs, info.Commands.Keys.ToArray(), info.Location)
        {
            Installation = info.Installation,
        };
}
