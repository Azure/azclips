// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions
{
    /// <summary>
    /// In-memory descriptor for a built-in (host-shipped) extension. Built-ins are not persisted
    /// to <c>registry.json</c>; this seed is merged with the on-disk registry at startup.
    /// </summary>
    /// <param name="Verb">Top-level verb the built-in claims (e.g. <c>"login"</c>).</param>
    /// <param name="PackageId">Full assembly / package id (e.g. <c>Microsoft.Azure.Cirrus.Extensions.Login</c>).</param>
    public sealed record BuiltInExtension(string Verb, string PackageId);

    /// <summary>
    /// Static seed of built-in extensions. Per <c>docs/spec/extension-management.md §3</c> ("True core"),
    /// the only true built-ins are <c>extension</c>, <c>login</c>, and <c>config</c>.
    /// </summary>
    /// <remarks>
    /// Verb conflicts: a built-in always wins over a registry entry. <c>extension add</c> must refuse
    /// to register any verb that appears in this seed.
    /// </remarks>
    public static class BuiltInExtensions
    {
        /// <summary>
        /// Map from verb (case-insensitive) to built-in descriptor.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, BuiltInExtension> Map =
            new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                // True core — present in every install, never removed.
                ["extension"] = new("extension", "Microsoft.Azure.Cirrus.Extensions.Extension"),
                ["login"]     = new("login",     "Microsoft.Azure.Cirrus.Extensions.Login"),
                ["config"]    = new("config",    "Microsoft.Azure.Cirrus.Extensions.Config"),
            };

        /// <summary>
        /// True if <paramref name="verb"/> is owned by a built-in extension.
        /// </summary>
        public static bool IsBuiltInVerb(string verb) => Map.ContainsKey(verb);
    }
}
