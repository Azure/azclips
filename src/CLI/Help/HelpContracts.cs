// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Core.Cli.Help
{
    /// <summary>
    /// Per-verb help manifest. One manifest is emitted per CLI verb (e.g. <c>"vm"</c>,
    /// <c>"disk"</c>); multi-verb extensions produce multiple manifests.
    /// </summary>
    public record HelpManifest(
        string Verb,
        string Version,
        string RootSummary,
        IList<HelpCommand> Commands);

    public record HelpCommand(
        string CliName,
        string PsName,
        string Summary,
        string Description,
        IList<HelpParameter> Parameters,
        HelpExampleSet Examples,
        HelpTag[] Tags,
        string? PreviewMessage = null,
        string? ExperimentalMessage = null,
        string? DeprecatedMessage = null);

    public record HelpParameter(
        string Name,
        string PsName,
        string Type,
        bool Required,
        string Description,
        string[] Aliases,
        string[] AllowedValues,
        string ParameterSetName,
        bool SupportsPipeline,
        bool SupportsWildcards,
        string? GroupName = null,
        string? ValueSources = null);

    public record HelpExampleSet(
        IList<HelpExample> Cli,
        IList<HelpExample> PowerShell);

    public record HelpExample(
        string Title,
        string Command);

    public record HelpIndex(
        IList<HelpGroupIndexEntry> Groups,
        IList<HelpCommandIndexEntry> Commands);

    /// <summary>Index entry for a verb-level root group.</summary>
    public record HelpGroupIndexEntry(
        string Verb,
        string Summary);

    /// <summary>Index entry for a single command. <see cref="Verb"/> is the verb namespace it belongs to.</summary>
    public record HelpCommandIndexEntry(
        string CliName,
        string PsName,
        string Verb,
        string Summary);

    public record HelpCatalogHeader(int SchemaVersion, string CoreVersion, string ExtensionFingerprint);
    public record HelpManifestDocument(HelpCatalogHeader Header, IList<HelpManifest> Manifests);
    public record HelpIndexDocument(HelpCatalogHeader Header, HelpIndex Index);
}
