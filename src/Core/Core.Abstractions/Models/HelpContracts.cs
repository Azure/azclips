// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    public record HelpManifest(
        string Extension,
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
        HelpTag[] Tags);

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
        bool SupportsWildcards);

    public record HelpExampleSet(
        IList<HelpExample> Cli,
        IList<HelpExample> PowerShell);

    public record HelpExample(
        string Title,
        string Command);

    public record HelpIndex(
        IList<HelpGroupIndexEntry> Groups,
        IList<HelpCommandIndexEntry> Commands);

    public record HelpGroupIndexEntry(
        string Extension,
        string Prefix,
        string Summary);

    public record HelpCommandIndexEntry(
        string CliName,
        string PsName,
        string Extension,
        string Summary);
}
