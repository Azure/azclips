// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Core.Extensions
{
    /// <summary>
    /// Centralized "find an installed extension by user-supplied name" logic, shared across the
    /// CRUD commands (<c>extension show/update/remove/pin/unpin</c>). Prior to PR #253 each
    /// command duplicated a slightly different match expression; the most common variant only
    /// inspected the legacy single <see cref="ExtensionRegistryEntry.Verb"/> field, so an
    /// extension that contributed to multiple top-level namespaces could only be addressed by
    /// its first verb (or full package id). This helper accepts any of:
    /// <list type="bullet">
    ///   <item>the exact package id (case-insensitive)</item>
    ///   <item>a short suffix (e.g. <c>"Resources"</c> matches <c>"Microsoft.Azure.Cirrus.Extensions.Resources"</c>)</item>
    ///   <item>the legacy <see cref="ExtensionRegistryEntry.Verb"/> field</item>
    ///   <item>any v2 namespace in <see cref="ExtensionRegistryEntry.Namespaces"/></item>
    ///   <item>any contribution verb in <see cref="ExtensionRegistryEntry.Contributions"/></item>
    /// </list>
    /// Pure function — no IO. Tests live in <c>tests/Cli.Tests/RegistryLookupTests.cs</c>.
    /// </summary>
    public static class RegistryLookup
    {
        /// <summary>
        /// Locates the entry for <paramref name="name"/> in <paramref name="registry"/>.
        /// </summary>
        /// <param name="registry">The registry to search; must not be <c>null</c>.</param>
        /// <param name="name">User-supplied identifier (package id, short name, verb, or namespace).</param>
        /// <param name="match">When <c>true</c>, the located entry. The default value otherwise.</param>
        /// <returns><c>true</c> if a match was found.</returns>
        /// <remarks>
        /// Match precedence is left-to-right of the list above; the first hit wins. In practice
        /// collisions are rare because verbs/namespaces are globally unique (LeafIndex enforces
        /// it at install time). When two namespaces from different packages happen to collide on
        /// the user-supplied <paramref name="name"/>, callers receive whichever the dictionary
        /// enumerates first; pass the full package id to disambiguate.
        /// </remarks>
        public static bool TryFind(
            ExtensionRegistry registry,
            string name,
            out KeyValuePair<string, ExtensionRegistryEntry> match)
        {
            ArgumentNullException.ThrowIfNull(registry);
            if (string.IsNullOrWhiteSpace(name))
            {
                match = default;
                return false;
            }

            foreach (var kvp in registry.Extensions)
            {
                if (Matches(kvp.Key, kvp.Value, name))
                {
                    match = kvp;
                    return true;
                }
            }

            match = default;
            return false;
        }

        private static bool Matches(string packageId, ExtensionRegistryEntry entry, string name)
        {
            if (string.Equals(packageId, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (packageId.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(entry.Verb) &&
                string.Equals(entry.Verb, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            foreach (var ns in entry.Namespaces)
            {
                if (string.Equals(ns, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            // Defensive: pre-NormalizeEntry fixtures can have Contributions populated but
            // Namespaces empty. Fall back to scanning contribution verbs so tests that build
            // entries by hand still resolve.
            foreach (var contribution in entry.Contributions)
            {
                if (string.Equals(contribution.Verb, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
