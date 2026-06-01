// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Extensions
{
    /// <summary>
    /// Unified build-time map of <c>"verb"</c> or <c>"verb path"</c> &#x2192; package id for
    /// first-party extensions, embedded by Core.Cli (see
    /// <c>src/CLI/build/ExtensionHints.targets</c>).
    /// </summary>
    /// <remarks>
    /// Consulted by the dispatcher when both the live <c>LeafIndex</c> and verb resolver miss, so
    /// we can produce a precise <c>extension add</c> suggestion instead of a generic
    /// "command not found." Per <c>docs/spec/extension-management.md §7.3</c>: covers the
    /// "unknown verb" case (no installed package at all) and the "verb partially served, leaf
    /// missing" case (e.g. another extension claims <c>vm</c> but not <c>vm scale</c>).
    /// </remarks>
    /// <summary>
    /// Per-leaf entry in <see cref="ExtensionHintTable"/>. The object shape (instead of a bare
    /// package-id string) is the feed-ready commitment from <c>docs/spec/extension-management.md
    /// §8.1</c>: optional fields can be added without re-bumping <c>schemaVersion</c>. Compat
    /// fields (<see cref="MinCoreVersion"/> / <see cref="MaxCoreVersion"/>) are reserved for the
    /// vNext feed half (Q26) and stay unset for v1 first-party output.
    /// </summary>
    /// <remarks>
    /// Unknown JSON properties on this object are tolerated — the reader uses default
    /// <c>System.Text.Json</c> behavior, which silently ignores extras. That's deliberate: a
    /// future feed entry can carry richer metadata without older hosts choking.
    /// </remarks>
    public sealed class ExtensionHint
    {
        [JsonPropertyName("packageId")]
        public string PackageId { get; set; } = string.Empty;

        [JsonPropertyName("minCoreVersion")]
        public string? MinCoreVersion { get; set; }

        [JsonPropertyName("maxCoreVersion")]
        public string? MaxCoreVersion { get; set; }
    }

    public sealed class ExtensionHintTable
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// UTC timestamp at which the table was generated. Provenance for the vNext feed half;
        /// unused at runtime in v1.
        /// </summary>
        [JsonPropertyName("generatedAt")]
        public DateTimeOffset? GeneratedAt { get; set; }

        /// <summary>
        /// Cirrus host version that produced the table. Provenance for the vNext feed half (so a
        /// host can compare a fetched table's source version against its own and decide whether
        /// to fall back to the embedded copy); unused at runtime in v1.
        /// </summary>
        [JsonPropertyName("sourceHostVersion")]
        public string? SourceHostVersion { get; set; }

        /// <summary>
        /// Key is the full-path form (e.g. <c>"vm create"</c>) or just the verb (e.g. <c>"vm"</c>
        /// for the bare-verb / group-help case); value is the owning <see cref="ExtensionHint"/>.
        /// </summary>
        [JsonPropertyName("leaves")]
        public Dictionary<string, ExtensionHint> Leaves { get; set; } =
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
                packageId = found.PackageId;
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
                packageId = found.PackageId;
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
                            table.Leaves = new Dictionary<string, ExtensionHint>(table.Leaves, StringComparer.OrdinalIgnoreCase);
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
