// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Identifies a single installed extension discovered on disk by an
    /// <see cref="IAssemblyLoadingProvider"/>. Used to pass extension identity through the
    /// assembly loading layer so <see cref="DiscoverySession"/> can emit
    /// <see cref="DiscoveredExtensionInfo"/> envelopes with accurate provenance.
    /// </summary>
    /// <param name="Name">
    /// Provider-specific extension identifier passed to
    /// <see cref="IAssemblyLoadingProvider.LoadExtension"/>. Depending on provider,
    /// this may be a full package id (for example,
    /// <c>Microsoft.Azure.Azclips.Extensions.Compute</c>) or a short extension name
    /// (for example, <c>Compute</c>).
    /// </param>
    /// <param name="Version">
    /// The resolved version string (e.g. <c>1.1.0</c>). May be <see cref="string.Empty"/> when
    /// the loader cannot determine a version (e.g. packaged installs where per-version metadata
    /// has been stripped). Source is loader-defined: some loaders read it from a version
    /// directory name, others from on-disk package metadata.
    /// </param>
    /// <param name="Location">
    /// Absolute filesystem path to the extension's primary DLL as reported by the loader that
    /// enumerated it. Surfaced by <c>az extension list</c>/<c>show</c> so users can see exactly
    /// which on-disk file backs a listed extension. May be <c>null</c> when the loader cannot
    /// report a path (e.g. in-memory / synthetic providers used by tests). Runtime-only —
    /// never persisted to <c>registry.json</c>.
    /// </param>
    public sealed record InstalledExtensionRef(string Name, string Version, string? Location);
}
