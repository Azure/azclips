// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// A single leaf in the dispatch table: the namespace verb, the path within it, and the
    /// package that owns it. See <c>docs/spec/extension-management.md §11.5</c> ("Dispatch").
    /// </summary>
    /// <param name="Verb">Top-level namespace (e.g. <c>"vm"</c>).</param>
    /// <param name="Path">Path within the namespace (e.g. <c>"create"</c>, or <c>""</c> for the root).</param>
    /// <param name="PackageId">Package id of the contributing extension (or built-in seed entry).</param>
    /// <param name="IsBuiltIn">True if the leaf is satisfied by the built-in seed.</param>
    public sealed record LeafEntry(string Verb, string Path, string PackageId, bool IsBuiltIn);

    /// <summary>
    /// Read-only <c>(verb, path) -&gt; <see cref="LeafEntry"/></c> index built from the merged
    /// built-in seed and on-disk registry. Phase 6 plumbing; the dispatcher rewrite in Phase 7
    /// will replace <see cref="ExtensionResolver"/>'s single-verb lookup with longest-prefix
    /// match over this index. See spec §11.5 and §11.9 (Phase 6, item 20).
    /// </summary>
    public sealed class LeafIndex
    {
        private static readonly IEqualityComparer<(string Verb, string Path)> s_keyComparer =
            new VerbPathComparer();

        private readonly IReadOnlyDictionary<(string Verb, string Path), LeafEntry> _entries;

        private LeafIndex(IReadOnlyDictionary<(string Verb, string Path), LeafEntry> entries)
        {
            _entries = entries;
        }

        /// <summary>
        /// All <c>(verb, path)</c> leaves known to the host. Case-insensitive lookup.
        /// </summary>
        public IReadOnlyDictionary<(string Verb, string Path), LeafEntry> Entries => _entries;

        /// <summary>
        /// True if some extension claims the exact <c>(verb, path)</c> leaf.
        /// </summary>
        public bool TryGet(string verb, string path, out LeafEntry? leaf)
        {
            if (_entries.TryGetValue((verb, path), out var found))
            {
                leaf = found;
                return true;
            }

            leaf = null;
            return false;
        }

        /// <summary>
        /// Builds an index from the supplied built-in seed and registry. Duplicate <c>(verb, path)</c>
        /// pairs across sources fail fast \u2014 mirroring the spec's "hard error on duplicate leaf"
        /// rule (\u00a711.4). Built-ins are emitted as <c>(verb, "")</c> root leaves.
        /// </summary>
        /// <param name="builtIns">Map from verb to built-in descriptor (e.g. <see cref="BuiltInExtensions.Map"/>).</param>
        /// <param name="registry">Loaded registry (post v1\u2192v2 normalization).</param>
        /// <exception cref="InvalidOperationException">A <c>(verb, path)</c> leaf is claimed by more than one source.</exception>
        public static LeafIndex Build(
            IReadOnlyDictionary<string, BuiltInExtension> builtIns,
            ExtensionRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(builtIns);
            ArgumentNullException.ThrowIfNull(registry);

            var entries = new Dictionary<(string Verb, string Path), LeafEntry>(s_keyComparer);

            foreach (var builtIn in builtIns.Values)
            {
                AddLeaf(entries, builtIn.Verb, string.Empty, builtIn.PackageId, isBuiltIn: true);
                foreach (var path in builtIn.Leaves)
                {
                    AddLeaf(entries, builtIn.Verb, path, builtIn.PackageId, isBuiltIn: true);
                }
            }

            foreach (var (packageId, entry) in registry.Extensions)
            {
                foreach (var contribution in entry.Contributions)
                {
                    AddLeaf(entries, contribution.Verb, contribution.Path, packageId, isBuiltIn: false);
                }
            }

            return new LeafIndex(entries);
        }

        private static void AddLeaf(
            Dictionary<(string Verb, string Path), LeafEntry> entries,
            string verb,
            string path,
            string packageId,
            bool isBuiltIn)
        {
            var key = (verb, path);
            if (entries.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate leaf '({verb}, {path})' claimed by both '{existing.PackageId}' and '{packageId}'. " +
                    "Per spec §11.4, the host fails to start when two extensions claim the same (verb, path).");
            }

            entries[key] = new LeafEntry(verb, path, packageId, isBuiltIn);
        }

        private sealed class VerbPathComparer : IEqualityComparer<(string Verb, string Path)>
        {
            public bool Equals((string Verb, string Path) x, (string Verb, string Path) y) =>
                string.Equals(x.Verb, y.Verb, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string Verb, string Path) obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Verb),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
        }
    }
}
