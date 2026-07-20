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
    /// Static seed of the <b>Core</b> extensions \u2014 those baked into the CLI host at compile time
    /// and always dispatchable without any filesystem read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Terminology (pending rename in a follow-up PR):</b> this repository informally refers to
    /// three tiers of extensions. The name <c>BuiltInExtensions</c> here refers only to the first
    /// tier (Core); it does not mean everything shipped alongside <c>az.exe</c>.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>Core</term>
    ///     <description>The compile-time seed in <see cref="Map"/>. Currently <c>extension</c>,
    ///     <c>login</c>/<c>logout</c>/<c>profile</c>, <c>config</c>, <c>agent</c>. Dispatch resolves
    ///     these through the static map with no version required.</description>
    ///   </item>
    ///   <item>
    ///     <term>Bundled</term>
    ///     <description>DLLs shipped alongside <c>az.exe</c> in the packaged install
    ///     (<c>&lt;install&gt;/Extensions/</c>) or the local build output (<c>artifacts/&lt;Config&gt;/</c>).
    ///     Loaded by <c>BuiltinExtensionLoader</c>. Bundled membership is orthogonal to Core:
    ///     packaging profiles (lean vs full, see <c>inject_builtin_extensions.sh</c>) can ship
    ///     any subset of Core extensions, and Bundled-only extensions like <c>Resources</c> and
    ///     <c>Compute</c> aren't in <see cref="Map"/> at all.</description>
    ///   </item>
    ///   <item>
    ///     <term>Installed</term>
    ///     <description>DLLs placed by <c>az extension add</c> under
    ///     <c>~/.azclips/extensions/&lt;pkg&gt;/&lt;version&gt;/</c>. Loaded by
    ///     <c>MultiVersionExtensionLoader</c>.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Verb conflicts: a Core entry always wins over a registry entry. <c>extension add</c> must
    /// refuse to register any verb that appears in this seed.
    /// </para>
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
                ["extension"] = new("extension", "Microsoft.Azure.Azclips.Extensions.Extension")
                {
                    Leaves = new[] { "add", "list", "show", "remove", "update", "pin", "unpin" },
                },
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
                ["logout"] = new("logout", "Microsoft.Azure.Azclips.Extensions.Login"),
                ["profile"] = new("profile", "Microsoft.Azure.Azclips.Extensions.Login")
                {
                    Leaves = new[] { "list", "show", "set", "remove" },
                },
                ["config"] = new("config", "Microsoft.Azure.Azclips.Extensions.Config")
                {
                    Leaves = new[] { "get", "set", "delete", "list", "info" },
                },
                ["agent"] = new("agent", "Microsoft.Azure.Azclips.Extensions.Agent")
                {
                    Leaves = new[] { "tool env", "tool help", "tool resource find", "tool resource sku", "tool resource tree", "tool resource show", "tool resource refresh" },
                },
            };

        /// <summary>
        /// True if <paramref name="verb"/> is owned by a built-in extension.
        /// </summary>
        public static bool IsBuiltInVerb(string verb) => Map.ContainsKey(verb);
    }
}
