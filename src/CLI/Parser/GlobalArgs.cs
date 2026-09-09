// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Version;

namespace Core.Cli.Parser;

/// <summary>
/// One-pass classification of user-facing global CLI flags. Produced by
/// <see cref="GlobalArgParser.Parse"/> and consumed by <c>Program.Main</c>. Replaces the ad-hoc
/// <c>ParseXxxFlag</c> helpers and the <c>DispatchHelpException</c> / <c>DispatchVersionException</c>
/// control-flow exceptions with a single record.
/// </summary>
/// <param name="Positional">
/// The original <c>args</c> minus any recognized global flags (<c>--debug</c>, <c>--output</c>,
/// <c>--no-cache</c>) and their values. Passed to <see cref="ExtensionDispatcher.Parse"/> for
/// normal command dispatch. Help / version tokens are left in place so downstream error handling
/// can still surface them if the caller ignores <see cref="Help"/> / <see cref="Version"/>.
/// </param>
/// <param name="IsBareInvocation">
/// <c>args.Length == 0</c> at entry to <c>Program.Main</c>. Used only for the welcome-banner
/// decision; <c>az --help</c> is <em>not</em> bare (it explicitly asked for help).
/// </param>
/// <param name="Debug"><c>--debug</c> was present anywhere in the input.</param>
/// <param name="Output">Resolved output format; defaults to <see cref="OutputFormat.Plain"/>.</param>
/// <param name="NoCache">
/// <c>--no-cache</c> was present. The caller mutates <c>AZCLIPS_NO_EXTENSION_CACHE</c>; the parser
/// does not.
/// </param>
/// <param name="Help">Non-null when a help request was recognized. See <see cref="HelpRequest"/>.</param>
/// <param name="Version">Non-null when a version request was recognized. See <see cref="VersionRequest"/>.</param>
internal sealed record GlobalArgs(
    IReadOnlyList<string> Positional,
    bool IsBareInvocation,
    bool Debug,
    OutputFormat Output,
    bool NoCache,
    HelpRequest? Help,
    VersionRequest? Version);

/// <summary>
/// Recognized <c>--output</c> value. Unknown values raise a usage error at parse time
/// (see <see cref="GlobalArgParser.Parse"/>).
/// </summary>
public enum OutputFormat
{
    Plain,
    Json,
    Jsonc,
    Yaml,
    Yamlc,
}

/// <summary>
/// A recognized help request. <see cref="Path"/> is empty for root help
/// (<c>az</c>, <c>az --help</c>, <c>az help</c>); non-empty for extension-scoped help
/// (<c>az group [cmd] --help</c>, <c>az help group [cmd]</c>). Group-vs-command semantics
/// are decided by the help renderer against the extension leaf index, not by the parser.
/// </summary>
internal sealed record HelpRequest(IReadOnlyList<string> Path);

/// <summary>
/// A recognized version request. <see cref="Format"/> is <see cref="VersionOutputFormat.Text"/>
/// for the flag form (<c>az --version</c>) and <see cref="VersionOutputFormat.Json"/> for the
/// verb form (<c>az version</c>), matching the pre-refactor dispatch shape.
/// </summary>
internal sealed record VersionRequest(VersionOutputFormat Format);
