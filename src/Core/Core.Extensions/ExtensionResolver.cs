// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// Result of resolving a top-level verb to the extension that owns it.
    /// </summary>
    /// <param name="Verb">The verb that was matched (preserves the registry casing).</param>
    /// <param name="PackageId">Full extension package id (e.g. <c>Microsoft.Azure.Azclips.Extensions.Compute</c>).</param>
    /// <param name="Version">Installed version reported by the registry entry.</param>
    public sealed record ResolvedExtension(string Verb, string PackageId, string? Version);

    /// <summary>
    /// Resolves a top-level CLI verb (e.g. <c>"vm"</c>) to the extension that should handle it.
    /// </summary>
    /// <remarks>
    /// Registry is the sole source of truth — every verb is resolved through the same code path
    /// (no Core-tier exception). Duplicate-verb collisions are caught upstream by
    /// <see cref="LeafIndex.Build"/>.
    /// </remarks>
    public interface IExtensionResolver
    {
        /// <summary>
        /// Try to resolve <paramref name="verb"/> to an extension.
        /// </summary>
        /// <returns>True if an entry in the registry claims the verb.</returns>
        bool TryResolve(string verb, out ResolvedExtension? resolved);
    }

    /// <summary>
    /// Default <see cref="IExtensionResolver"/> backed by an <see cref="IExtensionRegistryStore"/>.
    /// </summary>
    public sealed class ExtensionResolver : IExtensionResolver
    {
        private readonly IExtensionRegistryStore _registryStore;

        public ExtensionResolver(IExtensionRegistryStore registryStore)
        {
            _registryStore = registryStore;
        }

        /// <inheritdoc />
        public bool TryResolve(string verb, out ResolvedExtension? resolved)
        {
            if (string.IsNullOrWhiteSpace(verb))
            {
                resolved = null;
                return false;
            }

            // Registry is the sole source of truth. An entry may claim multiple namespaces
            // (e.g. Compute contributes both `vm` and `disk`); consult Namespaces first, fall
            // back to the legacy v1 `Verb` field or the first contribution.
            var registry = _registryStore.Load();
            foreach (var (packageId, entry) in registry.Extensions)
            {
                IReadOnlyList<string> entryVerbs =
                    entry.Namespaces.Count > 0 ? entry.Namespaces :
                    entry.Verb is not null ? new[] { entry.Verb } :
                    entry.Contributions.Count > 0 ? new[] { entry.Contributions[0].Verb } :
                    Array.Empty<string>();

                foreach (var entryVerb in entryVerbs)
                {
                    if (string.Equals(entryVerb, verb, StringComparison.OrdinalIgnoreCase))
                    {
                        resolved = new ResolvedExtension(entryVerb, packageId, entry.Version);
                        return true;
                    }
                }
            }

            resolved = null;
            return false;
        }
    }
}
