// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Core.Extensions
{
    /// <summary>
    /// Root document persisted at <c>~/.azclips/extensions/registry.json</c>.
    /// Tracks every installed (non-built-in) extension. Built-ins live in the host's static seed.
    /// </summary>
    /// <remarks>
    /// Each entry carries a <c>contributions</c> list of <c>(verb, path)</c> pairs and a
    /// derived <c>namespaces</c> field. The reader tolerates legacy v1 entries (single
    /// <c>verb</c> field, no contributions).
    /// </remarks>
    public sealed class ExtensionRegistry
    {
        /// <summary>
        /// Schema version. Bumped on breaking changes to the on-disk shape.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// Map from NuGet package id (e.g. <c>Microsoft.Azure.Azclips.Extensions.Compute</c>) to entry.
        /// </summary>
        [JsonPropertyName("extensions")]
        public Dictionary<string, ExtensionRegistryEntry> Extensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The schema version this codebase writes and accepts on read.
        /// </summary>
        /// <remarks>
        /// v1: single <c>verb</c> field per entry. v2: <c>contributions</c> list of
        /// <c>(verb, path)</c> leaves plus a derived <c>namespaces</c> list. v1 entries are read
        /// tolerantly and synthesized into a single <c>(verb, "")</c> contribution.
        /// </remarks>
        public const int CurrentSchemaVersion = 2;
    }

    /// <summary>
    /// A single leaf an extension contributes: the <see cref="Verb"/> top-level namespace plus the
    /// remaining command <see cref="Path"/> within it. Empty <c>Path</c> means "the namespace root".
    /// </summary>
    /// <remarks>
    /// The full CLI key for the leaf is <c>{Verb} {Path}</c> (e.g. <c>vm create</c>, where
    /// <c>Verb="vm"</c> and <c>Path="create"</c>).
    /// </remarks>
    public sealed class Contribution
    {
        /// <summary>Top-level namespace verb (e.g. <c>"vm"</c>).</summary>
        [JsonPropertyName("verb")]
        public required string Verb { get; set; }

        /// <summary>Path within the namespace (e.g. <c>"create"</c>, <c>"disk attach"</c>, or <c>""</c> for the namespace root).</summary>
        [JsonPropertyName("path")]
        public required string Path { get; set; }
    }

    /// <summary>
    /// A single installed extension's persisted state.
    /// </summary>
    public sealed class ExtensionRegistryEntry
    {
        /// <summary>
        /// Legacy v1 field: top-level verb this extension claims. Retained for back-compat —
        /// v2 entries synthesize a single <c>(Verb, "")</c> <see cref="Contributions"/> when this is set
        /// and no contributions are present. New v2-only entries leave this null and rely on
        /// <see cref="Contributions"/>.
        /// 
        /// DEPRECATED in §5.2: No longer written to registry.json; only read for legacy v1 entries.
        /// Verbs are derived from discovery at runtime. This field is kept for read-side compat only.
        /// </summary>
        [JsonPropertyName("verb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public string? Verb { get; set; }

        /// <summary>
        /// Installed version. Strict single-version; <c>update</c> overwrites in place.
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

        /// <summary>
        /// v2: the set of <c>(verb, path)</c> leaves this extension contributes. v1 entries are
        /// normalized into a single-element list with <c>Path = ""</c> on load.
        /// 
        /// DEPRECATED in §5.2: No longer written to registry.json; derived from discovery at runtime.
        /// Kept for read-side compat with existing registry files (v2 entries). New writes never set this.
        /// </summary>
        [JsonPropertyName("contributions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public List<Contribution> Contributions { get; set; } = new();

        /// <summary>
        /// v2: derived list of distinct top-level namespaces this extension contributes to. Always
        /// re-derived from <see cref="Contributions"/> before write; persisted for read-side ergonomics
        /// (e.g. <c>extension list --by-verb</c>) without forcing readers to scan contributions.
        /// 
        /// DEPRECATED in §5.2: No longer written to registry.json; derived from discovery at runtime.
        /// Kept for read-side compat with existing registry files. New writes never set this.
        /// </summary>
        [JsonPropertyName("namespaces")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public List<string> Namespaces { get; set; } = new();

        /// <summary>
        /// Absolute filesystem path to the extension's primary DLL as reported by the loader
        /// that discovered it. Populated at runtime by
        /// <c>DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences</c> so
        /// <c>az extension list</c> and <c>az extension show</c> can surface it.
        /// <b>Runtime-only</b> — never persisted to <c>registry.json</c> because paths shift
        /// between installs and the file is portable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string? Location { get; set; }

        /// <summary>The exact extension installation selected for execution. Not saved in the registry.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Core.Abstractions.ExtensionInstallation? Installation { get; init; }
    }
}
