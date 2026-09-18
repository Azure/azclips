// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Agent.Tools.Help;

// Read-side contracts mirror Core.Cli.Help without loading the CLI into the agent.
// Writer-to-provider tests cover every field, including optional help metadata.
public record HelpManifest(string Verb, string Version, string RootSummary, IList<HelpCommand> Commands);

public record HelpCommand(
    string CliName, string PsName, string Summary, string Description,
    IList<HelpParameter> Parameters, HelpExampleSet Examples, HelpTag[] Tags,
    string? PreviewMessage = null, string? ExperimentalMessage = null, string? DeprecatedMessage = null);

public record HelpParameter(
    string Name, string PsName, string Type, bool Required, string Description,
    string[] Aliases, string[] AllowedValues, string ParameterSetName,
    bool SupportsPipeline, bool SupportsWildcards, string? GroupName = null, string? ValueSources = null);

public record HelpExampleSet(IList<HelpExample> Cli, IList<HelpExample> PowerShell);
public record HelpExample(string Title, string Command);
public record HelpIndex(IList<HelpGroupIndexEntry> Groups, IList<HelpCommandIndexEntry> Commands);
public record HelpGroupIndexEntry(string Verb, string Summary);
public record HelpCommandIndexEntry(string CliName, string PsName, string Verb, string Summary);

public record HelpCatalogHeader(int SchemaVersion, string CoreVersion, string ExtensionFingerprint);
public record HelpManifestDocument(HelpCatalogHeader Header, IList<HelpManifest> Manifests);
public record HelpIndexDocument(HelpCatalogHeader Header, HelpIndex Index);
