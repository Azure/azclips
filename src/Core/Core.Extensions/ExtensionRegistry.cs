// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Core.Extensions
{
    /// <summary>
    /// Root document persisted at <c>~/.cirrus/extensions/registry.json</c>.
    /// Tracks every installed (non-built-in) extension. Built-ins live in the host's static seed.
    /// </summary>
    /// <remarks>
    /// Schema is documented in <c>docs/spec/extension-management.md §5.1</c>.
    /// Shape A: exactly one entry per extension package; <see cref="ExtensionRegistryEntry.Verb"/>
    /// is the single top-level verb that extension claims.
    /// </remarks>
    public sealed class ExtensionRegistry
    {
        /// <summary>
        /// Schema version. Bumped on breaking changes to the on-disk shape.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// Map from NuGet package id (e.g. <c>Microsoft.Azure.Cirrus.Extensions.Compute</c>) to entry.
        /// </summary>
        [JsonPropertyName("extensions")]
        public Dictionary<string, ExtensionRegistryEntry> Extensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The schema version this codebase writes and accepts on read.
        /// </summary>
        public const int CurrentSchemaVersion = 1;
    }

    /// <summary>
    /// A single installed extension's persisted state.
    /// </summary>
    public sealed class ExtensionRegistryEntry
    {
        /// <summary>
        /// Top-level verb this extension claims (declared via <c>[ExtensionVerb]</c> on its <c>IExtensionIndex</c>).
        /// </summary>
        [JsonPropertyName("verb")]
        public required string Verb { get; set; }

        /// <summary>
        /// Installed version. Per §5.1 Shape A is strict single-version; <c>update</c> overwrites in place.
        /// </summary>
        [JsonPropertyName("version")]
        public required string Version { get; set; }

        /// <summary>
        /// If <c>true</c>, <c>extension update</c> refuses to advance <see cref="Version"/> without <c>--force</c>.
        /// </summary>
        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; }

        /// <summary>
        /// NuGet feed the extension was installed from. <c>null</c> for local-path installs (future).
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        /// UTC timestamp of the install (or last successful <c>update</c>).
        /// </summary>
        [JsonPropertyName("installedAt")]
        public DateTimeOffset InstalledAt { get; set; }
    }
}
