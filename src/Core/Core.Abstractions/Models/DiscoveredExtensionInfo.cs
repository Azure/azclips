// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// The per-extension envelope emitted by <c>DiscoverySession</c>. Carries extension
    /// identity, contributed verbs, and the full command table for one extension at one
    /// resolved version.
    /// </summary>
    /// <param name="PackageId">
    /// Full NuGet package id (e.g. <c>Microsoft.Azure.Azclips.Extensions.Compute</c>).
    /// Derived from <see cref="CommandMetadata.AssemblyName"/> when available.
    /// </param>
    /// <param name="ResolvedVersion">
    /// The version actually loaded from disk (e.g. <c>1.1.0</c>). Empty string when
    /// the version is unknown (e.g. built-ins or legacy single-name paths).
    /// </param>
    /// <param name="Verbs">
    /// Top-level CLI verbs contributed by this extension (e.g. <c>["vm", "disk"]</c>).
    /// Sourced from <see cref="ExtensionVerbAttribute"/> declarations on the index type.
    /// </param>
    /// <param name="Commands">
    /// All commands for the requested client type, keyed by
    /// <see cref="CommandMetadata.InvocationName"/> (e.g. <c>"vm create"</c>).
    /// </param>
    /// <param name="Location">
    /// Absolute filesystem path to the extension's primary DLL on disk, propagated from the
    /// loader that enumerated it via <see cref="InstalledExtensionRef.Location"/>. Surfaced by
    /// <c>az extension list</c>/<c>show</c> so users can see exactly which on-disk file backs a
    /// listed extension. May be <c>null</c> when discovery could not attribute the info to a
    /// specific loader (e.g. named-extension or single-command lookup paths that bypass the
    /// on-disk enumeration).
    /// </param>
    public sealed record DiscoveredExtensionInfo(
        string PackageId,
        string ResolvedVersion,
        IReadOnlyList<string> Verbs,
        IReadOnlyDictionary<string, CommandMetadata> Commands,
        string? Location);
}
