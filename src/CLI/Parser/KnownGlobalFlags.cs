// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Parser;

/// <summary>
/// A single global CLI flag — one appearing on every command. Serves as shared source of truth
/// for the runtime <c>--help</c> renderer, the docs exporter, AND <see cref="GlobalArgParser"/>'s
/// parse dispatch. Mirrors argparse's <c>Action</c> pattern from azure-cli's globals model.
/// </summary>
public sealed record KnownGlobalFlag(
    string Name,
    IReadOnlyList<string> Aliases,
    string Summary,
    string? Default,
    string? Choices,
    ParseAction? Action);

/// <summary>
/// Canonical list of global CLI flags recognised by <see cref="GlobalArgParser"/>. Each entry
/// pairs its docs metadata with a <see cref="ParseAction"/> that <see cref="GlobalArgParser"/>
/// dispatches through — one file, one entry per flag, no drift possible between docs and parse.
/// </summary>
/// <remarks>
/// <para><b>Adding a new global flag: single edit in this file.</b></para>
/// <list type="number">
///   <item>Add a <see cref="KnownGlobalFlag"/> entry to <see cref="All"/> below with an
///     appropriate <see cref="ParseAction"/> subclass.</item>
///   <item>If the flag exposes a new field on <see cref="GlobalArgs"/> (e.g. a new
///     <c>bool</c> or <c>enum</c>), add matching properties to
///     <see cref="GlobalArgsBuilder"/> and <see cref="GlobalArgs"/>.</item>
/// </list>
/// <para>Flags whose action is <see langword="null"/> (e.g. <c>--help</c>) are handled by
/// <see cref="GlobalArgParser"/>'s post-pass positional classifier rather than per-token
/// consumption; they still appear here for docs and runtime <c>--help</c> rendering.</para>
/// </remarks>
public static class KnownGlobalFlags
{
    public static readonly IReadOnlyList<KnownGlobalFlag> All =
    [
        new KnownGlobalFlag(
            Name: "--debug",
            Aliases: Array.Empty<string>(),
            Summary: "Increase logging verbosity to show all debug logs.",
            Default: null,
            Choices: null,
            Action: new BoolToggleAction(static b => b.Debug = true)),
        new KnownGlobalFlag(
            Name: "--help",
            Aliases: ["-h", "-help", "-?", "--?"],
            Summary: "Show help for the command or group and exit.",
            Default: null,
            Choices: null,
            Action: null),
        new KnownGlobalFlag(
            Name: "--no-cache",
            Aliases: Array.Empty<string>(),
            Summary: "Bypass the extension discovery cache and reload extensions from disk.",
            Default: null,
            Choices: null,
            Action: new BoolToggleAction(static b => b.NoCache = true)),
        new KnownGlobalFlag(
            Name: "--output",
            Aliases: ["-o"],
            Summary: "Output format.",
            Default: "json",
            Choices: "json, jsonc, table, tsv, yaml, yamlc",
            Action: new OutputFormatAction()),
        new KnownGlobalFlag(
            Name: "--query",
            Aliases: Array.Empty<string>(),
            Summary: "JMESPath query string to filter/project the command result before rendering.",
            Default: null,
            Choices: null,
            Action: new QueryAction()),
    ];
}
