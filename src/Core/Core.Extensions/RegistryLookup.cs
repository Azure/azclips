// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Core.Extensions
{
    /// <summary>
    /// Centralized "find an installed extension by user-supplied name" logic, shared across the
    /// CRUD commands (<c>extension show/update/remove/pin/unpin</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unit of install is a *package*, not a verb. A single package can contribute many
    /// verbs (Compute ships both <c>vm</c> and <c>disk</c>); accepting a verb as the lookup
    /// token would let <c>extension remove --name disk</c> silently uninstall the entire Compute
    /// package, including <c>vm</c>. To avoid that trap, lookup is restricted to package
    /// identifiers only:
    /// </para>
    /// <list type="bullet">
    ///   <item>the exact package id (case-insensitive), e.g. <c>Microsoft.Azure.Cirrus.Extensions.Compute</c></item>
    ///   <item>a short suffix that matches the trailing dotted segment, e.g. <c>Compute</c>
    ///     matches <c>Microsoft.Azure.Cirrus.Extensions.Compute</c></item>
    /// </list>
    /// <para>
    /// Verbs and namespaces are still useful as *suggestion* signals — see
    /// <see cref="SuggestPackageForToken"/> — but never as selectors for destructive ops.
    /// </para>
    /// <para>Pure function — no IO. Tests live in <c>tests/Cli.Tests/RegistryLookupTests.cs</c>.</para>
    /// </remarks>
    public static class RegistryLookup
    {
        /// <summary>
        /// Locates the entry for <paramref name="name"/> in <paramref name="registry"/>.
        /// </summary>
        /// <param name="registry">The registry to search; must not be <c>null</c>.</param>
        /// <param name="name">User-supplied package identifier (full package id or short suffix).</param>
        /// <param name="match">When <c>true</c>, the located entry. The default value otherwise.</param>
        /// <returns><c>true</c> if a match was found.</returns>
        /// <remarks>
        /// Verb and namespace tokens are intentionally **not** matched. Callers should invoke
        /// <see cref="SuggestPackageForToken"/> on a miss to enrich the error message with a
        /// "did you mean &lt;package&gt;?" hint when the user typed a verb.
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
                if (Matches(kvp.Key, name))
                {
                    match = kvp;
                    return true;
                }
            }

            match = default;
            return false;
        }

        /// <summary>
        /// Diagnostic-only lookup: when <see cref="TryFind"/> misses, callers may pass the same
        /// token here to discover whether it matches a verb or namespace contributed by some
        /// installed package. Returns the owning package id when a unique association is found,
        /// for use in an "Did you mean &lt;package&gt;?" error suffix. Never used to drive
        /// destructive operations — strictly an error-message enrichment.
        /// </summary>
        /// <param name="registry">The registry to scan; must not be <c>null</c>.</param>
        /// <param name="token">The user-supplied token that failed <see cref="TryFind"/>.</param>
        /// <param name="suggestedPackageId">
        /// When <c>true</c>, the package id that contributes <paramref name="token"/> as a verb,
        /// namespace, or contribution verb. The default value otherwise.
        /// </param>
        /// <returns><c>true</c> if exactly one installed package contributes the token.</returns>
        public static bool SuggestPackageForToken(
            ExtensionRegistry registry,
            string token,
            out string suggestedPackageId)
        {
            ArgumentNullException.ThrowIfNull(registry);
            suggestedPackageId = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string? sole = null;
            foreach (var (packageId, entry) in registry.Extensions)
            {
                if (TokenAppearsIn(entry, token))
                {
                    if (sole is not null && !string.Equals(sole, packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Two distinct packages contribute the same token; can't make a
                        // confident suggestion. Bail without a hint.
                        return false;
                    }
                    sole = packageId;
                }
            }

            if (sole is null)
            {
                return false;
            }

            suggestedPackageId = sole;
            return true;
        }

        /// <summary>
        /// Convenience: strip the well-known <c>Microsoft.Azure.Cirrus.Extensions.</c> prefix from
        /// a package id so error messages can suggest the short form (e.g. <c>Compute</c>) that
        /// the user is most likely to type. Third-party / non-prefixed ids pass through unchanged.
        /// </summary>
        public static string ToShortName(string packageId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
            const string prefix = "Microsoft.Azure.Cirrus.Extensions.";
            return packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? packageId[prefix.Length..]
                : packageId;
        }

        private static bool Matches(string packageId, string name)
        {
            if (string.Equals(packageId, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (packageId.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static bool TokenAppearsIn(ExtensionRegistryEntry entry, string token)
        {
            if (!string.IsNullOrEmpty(entry.Verb) &&
                string.Equals(entry.Verb, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            foreach (var ns in entry.Namespaces)
            {
                if (string.Equals(ns, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            foreach (var contribution in entry.Contributions)
            {
                if (string.Equals(contribution.Verb, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
