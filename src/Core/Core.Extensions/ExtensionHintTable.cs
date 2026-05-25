// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Extensions
{
    /// <summary>
    /// Unified build-time map of <c>"verb"</c> or <c>"verb path"</c> &#x2192; package id for
    /// first-party extensions, embedded by Core.Cli (see
    /// <c>src/CLI/build/ExtensionHints.targets</c>). Replaces the separate <c>VerbHintTable</c> +
    /// <c>LeafHintTable</c> introduced in Phase 4/Phase 8 — the information was redundant in the
    /// verb dimension and is cleaner as a single document.
    /// </summary>
    /// <remarks>
    /// Consulted by the dispatcher when both the live <c>LeafIndex</c> and verb resolver miss, so
    /// we can produce a precise <c>extension add</c> suggestion instead of a generic
    /// "command not found." Per <c>docs/spec/extension-management.md §11.6</c>: covers the
    /// "unknown verb" case (no installed package at all) and the "verb partially served, leaf
    /// missing" case (e.g. another extension claims <c>vm</c> but not <c>vm scale</c>).
    /// </remarks>
    public sealed class ExtensionHintTable
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Key is the full-path form (e.g. <c>"vm create"</c>) or just the verb (e.g. <c>"vm"</c>
        /// for the bare-verb / group-help case); value is the package id that owns it.
        /// </summary>
        [JsonPropertyName("leaves")]
        public Dictionary<string, string> Leaves { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Lazy<ExtensionHintTable> s_default = new(LoadFromEmbeddedResource);

        /// <summary>
        /// Process-lifetime singleton loaded from the <c>extension-hints.json</c> embedded
        /// resource. Returns an empty table if the resource is missing (e.g. host built without
        /// the target).
        /// </summary>
        public static ExtensionHintTable Default => s_default.Value;

        /// <summary>
        /// Looks up the package id that owns <paramref name="verb"/> alone. Used by the
        /// dispatcher for the bare-verb (group-help) miss path.
        /// </summary>
        public bool TryGetPackageId(string verb, out string packageId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verb);
            if (Leaves.TryGetValue(verb, out var found))
            {
                packageId = found;
                return true;
            }
            packageId = string.Empty;
            return false;
        }

        /// <summary>
        /// Looks up the package id that owns the leaf identified by <paramref name="verb"/> and
        /// <paramref name="path"/>. Empty <paramref name="path"/> falls back to bare-verb lookup.
        /// </summary>
        public bool TryGetPackageId(string verb, string path, out string packageId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verb);
            var key = string.IsNullOrEmpty(path) ? verb : $"{verb} {path}";
            if (Leaves.TryGetValue(key, out var found))
            {
                packageId = found;
                return true;
            }
            packageId = string.Empty;
            return false;
        }

        private static ExtensionHintTable LoadFromEmbeddedResource()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Stream? stream;
                try
                {
                    stream = assembly.GetManifestResourceStream("extension-hints.json");
                }
                catch
                {
                    continue;
                }

                if (stream is null)
                {
                    continue;
                }

                using (stream)
                {
                    try
                    {
                        var table = JsonSerializer.Deserialize<ExtensionHintTable>(stream);
                        if (table is not null)
                        {
                            // Rebuild with case-insensitive comparer; System.Text.Json doesn't
                            // honor the comparer specified on the property default.
                            table.Leaves = new Dictionary<string, string>(table.Leaves, StringComparer.OrdinalIgnoreCase);
                            return table;
                        }
                    }
                    catch (JsonException)
                    {
                        // Malformed resource; fall through to empty table.
                    }
                }
            }

            return new ExtensionHintTable();
        }
    }
}
