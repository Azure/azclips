// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// A single leaf in the dispatch table: the namespace verb, the path within it, and the
    /// package that owns it.
    /// </summary>
    /// <param name="Verb">Top-level namespace (e.g. <c>"vm"</c>).</param>
    /// <param name="Path">Path within the namespace (e.g. <c>"create"</c>, or <c>""</c> for the root).</param>
    /// <param name="PackageId">Package id of the contributing extension.</param>
    public sealed record LeafEntry(string Verb, string Path, string PackageId, string? Version);

    /// <summary>
    /// Read-only <c>(verb, path) -&gt; <see cref="LeafEntry"/></c> index built from the
    /// discovery-driven <see cref="ExtensionRegistry"/>. Drives the dispatcher's longest-prefix match.
    /// </summary>
    public sealed class LeafIndex
    {
        private static readonly IEqualityComparer<(string Verb, string Path)> s_keyComparer =
            new VerbPathComparer();

        private readonly IReadOnlyDictionary<(string Verb, string Path), LeafEntry> _entries;
        private readonly HashSet<string> _verbs;

        // Every (verb, path) that is a proper prefix of some leaf — i.e. a command group with no
        // runnable leaf of its own (e.g. (agent, "") and (agent, "tool") when (agent, "tool env show")
        // exists). Precomputed so IsCommandGroup is an O(1) set lookup instead of scanning entries.
        private readonly HashSet<(string Verb, string Path)> _groups;

        private LeafIndex(IReadOnlyDictionary<(string Verb, string Path), LeafEntry> entries)
        {
            _entries = entries;
            _verbs = entries.Keys
                .Select(k => k.Verb)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _groups = new HashSet<(string, string)>(s_keyComparer);
            foreach (var (verb, path) in entries.Keys)
            {
                // A leaf marks all of its proper-prefix ancestors as groups, never itself.
                // "tool resource find" contributes ancestors "", "tool", "tool resource".
                // A root leaf like (login, "") has no ancestors, so login stays runnable.
                if (path.Length == 0)
                {
                    continue;
                }
                var segments = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                _groups.Add((verb, string.Empty));
                for (int i = 1; i < segments.Length; i++)
                {
                    _groups.Add((verb, string.Join(" ", segments, 0, i)));
                }
            }
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
        /// True if any leaf in the index claims <paramref name="verb"/> under any path.
        /// Answers "is this top-level verb known to discovery?" — used by the dispatcher's
        /// fallback path to distinguish "typo inside an installed group" from "extension
        /// genuinely not installed" before consulting <see cref="ExtensionHintTable"/>.
        /// </summary>
        public bool HasVerb(string verb)
        {
            if (string.IsNullOrEmpty(verb))
            {
                return false;
            }

            return _verbs.Contains(verb);
        }

        /// <summary>
        /// True if <c>(verb, path)</c> names a command group rather than a runnable leaf. A leaf is
        /// runnable only when it has no children: <c>login</c> (whose sole leaf is its root) is
        /// runnable, whereas a group root like <c>agent</c> or an intermediate group like
        /// <c>agent tool</c> owns longer leaves and so is a group. The dispatcher uses this to surface
        /// a <c>CommandNotFound</c> hint instead of handing a non-runnable group to the extension,
        /// which would misreport it as an unknown command and fall through to the AI agent.
        /// </summary>
        public bool IsCommandGroup(string verb, string path)
        {
            if (string.IsNullOrEmpty(verb))
            {
                return false;
            }

            // O(1): _groups holds every (verb, path) that is a proper prefix of some leaf, computed
            // once at construction. A runnable root/leaf is never in it (see the ctor).
            return _groups.Contains((verb, path ?? string.Empty));
        }

        /// <summary>
        /// Builds an index from the supplied registry. Duplicate <c>(verb, path)</c>
        /// pairs across extensions fail fast — duplicate leaves are a hard error
        /// (classic-Azure-CLI shadowing-gap protection).
        /// </summary>
        /// <param name="registry">Discovery-built registry (post v1→v2 normalization).</param>
        /// <exception cref="InvalidOperationException">A <c>(verb, path)</c> leaf is claimed by more than one extension.</exception>
        public static LeafIndex Build(ExtensionRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var entries = new Dictionary<(string Verb, string Path), LeafEntry>(s_keyComparer);

            foreach (var (packageId, entry) in registry.Extensions)
            {
                foreach (var contribution in entry.Contributions)
                {
                    AddLeaf(entries, contribution.Verb, contribution.Path, packageId, entry.Version);
                }
            }

            return new LeafIndex(entries);
        }

        private static void AddLeaf(
            Dictionary<(string Verb, string Path), LeafEntry> entries,
            string verb,
            string path,
            string packageId,
            string? version)
        {
            var key = (verb, path);
            if (entries.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate leaf '({verb}, {path})' claimed by both '{existing.PackageId}' and '{packageId}'. " +
                    "The host fails to start when two extensions claim the same (verb, path).");
            }

            entries[key] = new LeafEntry(verb, path, packageId, version);
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
