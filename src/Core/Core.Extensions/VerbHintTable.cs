// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Extensions
{
    /// <summary>
    /// Build-time map of verb &#x2192; package id for first-party extensions, embedded by Core.Cli
    /// (see <c>src/CLI/build/VerbHints.targets</c>). Consulted by the dispatcher when a verb the
    /// user typed is not present in either the built-in seed or the on-disk registry, so we can
    /// suggest the right <c>extension add</c> invocation instead of generic "command not found".
    /// </summary>
    /// <remarks>
    /// Per <c>docs/spec/extension-management.md §4.3</c>: the v1 surface is monorepo-only; a feed-
    /// served verb index is the documented Q9 follow-up.
    /// </remarks>
    public sealed class VerbHintTable
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("verbs")]
        public Dictionary<string, string> Verbs { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Lazy<VerbHintTable> s_default = new(LoadFromEmbeddedResource);

        /// <summary>
        /// Process-lifetime singleton loaded from the <c>verb-hints.json</c> embedded resource.
        /// Returns an empty table if the resource is missing (e.g. host built without the target).
        /// </summary>
        public static VerbHintTable Default => s_default.Value;

        /// <summary>
        /// Looks up the package id that owns <paramref name="verb"/>, if any.
        /// </summary>
        public bool TryGetPackageId(string verb, out string packageId)
        {
            if (Verbs.TryGetValue(verb, out var found))
            {
                packageId = found;
                return true;
            }
            packageId = string.Empty;
            return false;
        }

        private static VerbHintTable LoadFromEmbeddedResource()
        {
            // Scan all currently-loaded assemblies for the resource; whichever host (CLI or PS)
            // embedded it will provide the data. Keeps Core.Extensions agnostic of which assembly
            // actually carries the resource.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Stream? stream;
                try
                {
                    stream = assembly.GetManifestResourceStream("verb-hints.json");
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
                        var table = JsonSerializer.Deserialize<VerbHintTable>(stream);
                        if (table is not null)
                        {
                            // Rebuild verbs with a case-insensitive comparer: System.Text.Json
                            // does not honor the comparer specified on the property default.
                            table.Verbs = new Dictionary<string, string>(table.Verbs, StringComparer.OrdinalIgnoreCase);
                            return table;
                        }
                    }
                    catch (JsonException)
                    {
                        // Malformed resource; fall through to empty table.
                    }
                }
            }

            return new VerbHintTable();
        }
    }
}
