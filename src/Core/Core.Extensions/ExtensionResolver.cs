// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// Result of resolving a top-level verb to the extension that owns it.
    /// </summary>
    /// <param name="Verb">The verb that was matched (preserves the seed/registry casing).</param>
    /// <param name="PackageId">Full extension package id (e.g. <c>Microsoft.Azure.Cirrus.Extensions.Compute</c>).</param>
    /// <param name="Version">Installed version. <c>null</c> for built-ins, which ship with the host.</param>
    /// <remarks>
    /// Built-in vs. installed is implicit: a verb is built-in iff <see cref="BuiltInExtensions.IsBuiltInVerb"/>
    /// returns true for <c>Verb</c>.
    /// </remarks>
    public sealed record ResolvedExtension(string Verb, string PackageId, string? Version);

    /// <summary>
    /// Resolves a top-level CLI verb (e.g. <c>"vm"</c>) to the extension that should handle it.
    /// </summary>
    /// <remarks>
    /// Source order, per the spec:
    /// <list type="number">
    ///   <item>Built-in seed (<see cref="BuiltInExtensions.Map"/>) — wins on conflict.</item>
    ///   <item>On-disk registry (<c>~/.cirrus/extensions/registry.json</c>).</item>
    /// </list>
    /// </remarks>
    public interface IExtensionResolver
    {
        /// <summary>
        /// Try to resolve <paramref name="verb"/> to an extension.
        /// </summary>
        /// <returns>True if a built-in or installed extension claims the verb.</returns>
        bool TryResolve(string verb, out ResolvedExtension? resolved);
    }

    /// <summary>
    /// Default <see cref="IExtensionResolver"/> backed by an <see cref="IExtensionRegistryStore"/>.
    /// </summary>
    public sealed class ExtensionResolver : IExtensionResolver
    {
        private readonly IExtensionRegistryStore _registryStore;
        private readonly IReadOnlyDictionary<string, BuiltInExtension> _builtIns;

        /// <summary>
        /// Creates a resolver that consults the default built-in seed and the supplied registry store.
        /// </summary>
        public ExtensionResolver(IExtensionRegistryStore registryStore)
            : this(registryStore, BuiltInExtensions.Map)
        {
        }

        /// <summary>
        /// Test-friendly overload that takes an explicit built-in seed.
        /// </summary>
        public ExtensionResolver(IExtensionRegistryStore registryStore, IReadOnlyDictionary<string, BuiltInExtension> builtIns)
        {
            _registryStore = registryStore;
            _builtIns = builtIns;
        }

        /// <inheritdoc />
        public bool TryResolve(string verb, out ResolvedExtension? resolved)
        {
            if (string.IsNullOrWhiteSpace(verb))
            {
                resolved = null;
                return false;
            }

            // Built-ins win on conflict (defensive — extension add must refuse colliding verbs).
            if (_builtIns.TryGetValue(verb, out var builtIn))
            {
                resolved = new ResolvedExtension(builtIn.Verb, builtIn.PackageId, Version: null);
                return true;
            }

            // Fall through to installed extensions. An entry may claim multiple namespaces
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
