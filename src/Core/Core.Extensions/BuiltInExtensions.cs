// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// In-memory descriptor for a built-in (host-shipped) extension. Built-ins are not persisted
    /// to <c>registry.json</c>; this seed is merged with the on-disk registry at startup.
    /// </summary>
    /// <param name="Verb">Top-level verb the built-in claims (e.g. <c>"login"</c>).</param>
    /// <param name="PackageId">Full assembly / package id (e.g. <c>Microsoft.Azure.Azclips.Extensions.Login</c>).</param>
    public sealed record BuiltInExtension(string Verb, string PackageId)
    {
        /// <summary>
        /// Paths within <see cref="Verb"/> that the built-in's <c>s_cliTable</c> handles
        /// (e.g. <c>"remove"</c> for the key <c>"extension remove"</c>). Seeded into
        /// <see cref="LeafIndex"/> so dispatch's longest-prefix match peels positional
        /// arguments correctly — without this, <c>az extension remove Resources</c>
        /// would fall into the verb-only fallback and join <c>"remove Resources"</c>
        /// as the command name. Empty string represents the bare-verb leaf and is
        /// always seeded implicitly.
        /// </summary>
        public IReadOnlyList<string> Leaves { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Static seed of built-in extensions. The "true core" set is <c>extension</c>,
    /// <c>login</c>, and <c>config</c>.
    /// </summary>
    /// <remarks>
    /// Verb conflicts: a built-in always wins over a registry entry. <c>extension add</c> must refuse
    /// to register any verb that appears in this seed.
    /// </remarks>
    public static class BuiltInExtensions
    {
        /// <summary>
        /// Map from verb (case-insensitive) to built-in descriptor. Leaves must mirror each
        /// built-in's <c>s_cliTable</c> keys (minus the verb prefix) so the leaf index can
        /// resolve positional sub-commands like <c>extension remove &lt;name&gt;</c>.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, BuiltInExtension> Map =
            new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                // True core — present in every install, never removed.
                ["extension"] = new("extension", "Microsoft.Azure.Cirrus.Extensions.Extension")
                {
                    Leaves = new[] { "add", "list", "show", "remove", "update", "pin", "unpin" },
                },
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login")
                {
                    Leaves = new[] { "logout", "show" },
                },
                ["config"] = new("config", "Microsoft.Azure.Cirrus.Extensions.Config")
                {
                    Leaves = new[] { "get", "set", "delete", "list", "info" },
                },
            };

        /// <summary>
        /// True if <paramref name="verb"/> is owned by a built-in extension.
        /// </summary>
        public static bool IsBuiltInVerb(string verb) => Map.ContainsKey(verb);
    }
}
