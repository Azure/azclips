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
    /// The folder name under the install root (e.g.
    /// <c>Microsoft.Azure.Cirrus.Extensions.Compute</c>). Passed verbatim to
    /// <see cref="IAssemblyLoadingProvider.LoadExtension"/> as the extension name.
    /// </param>
    /// <param name="Version">The version directory name (e.g. <c>1.1.0</c>).</param>
    public sealed record InstalledExtensionRef(string Name, string Version);
}
