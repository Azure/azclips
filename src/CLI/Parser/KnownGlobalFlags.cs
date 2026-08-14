// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Parser;

/// <summary>
/// A single global CLI flag — one appearing on every command. Serves as shared source of
/// truth for both runtime <c>--help</c> rendering and the docs exporter's CLI2Yaml globals block.
/// Parse behaviour per flag stays in <see cref="GlobalArgParser"/>; this record only describes
/// what users see.
/// </summary>
public sealed record KnownGlobalFlag(
    string Name,
    IReadOnlyList<string> Aliases,
    string Summary,
    string? Default,
    string? Choices);

/// <summary>
/// Canonical list of global CLI flags recognised by <see cref="GlobalArgParser"/>. Mirrors
/// azure-cli's model where <c>knack</c>'s CLIParser injects the same set into every command's
/// argparse tree; the exporter and the runtime renderer both read the same list so docs and
/// in-tool help never drift. See <see cref="KnownGlobalFlagsTests"/> in the Cli.Tests project
/// for the drift-detection contract.
/// </summary>
public static class KnownGlobalFlags
{
    public static readonly IReadOnlyList<KnownGlobalFlag> All =
    [
        new KnownGlobalFlag(
            Name: "--debug",
            Aliases: Array.Empty<string>(),
            Summary: "Increase logging verbosity to show all debug logs.",
            Default: null,
            Choices: null),
        new KnownGlobalFlag(
            Name: "--help",
            Aliases: ["-h"],
            Summary: "Show help for the command or group and exit.",
            Default: null,
            Choices: null),
        new KnownGlobalFlag(
            Name: "--no-cache",
            Aliases: Array.Empty<string>(),
            Summary: "Bypass the extension discovery cache and reload extensions from disk.",
            Default: null,
            Choices: null),
        new KnownGlobalFlag(
            Name: "--output",
            Aliases: ["-o"],
            Summary: "Output format.",
            Default: "plain",
            Choices: "json, plain"),
    ];
}
