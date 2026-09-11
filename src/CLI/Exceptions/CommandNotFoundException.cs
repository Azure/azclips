// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli.Exceptions;

/// <summary>
/// Thrown by <see cref="Parser.ExtensionDispatcher"/> when the user types a verb that IS
/// owned by a discovered/installed extension, but the specific <c>(verb, path)</c> leaf
/// they invoked does not exist. Distinct from <see cref="ExtensionNotInstalledException"/>
/// (code <c>ExtensionNotFound</c>), which fires only when the owning package is genuinely
/// absent.
/// </summary>
/// <remarks>
/// Bypasses the AI agent fallback: the user's intent is unambiguous (they want a leaf
/// under a known group), so we surface a targeted "run <c>--help</c>" hint instead.
/// </remarks>
public sealed class CommandNotFoundException : AzclipsErrorException
{
    public string Verb { get; }
    public IReadOnlyList<string> Path { get; }

    public CommandNotFoundException(string verb, IReadOnlyList<string> path)
        : base(
            code: "CommandNotFound",
            exitCode: 4,
            message: BuildMessage(verb, path),
            details: new Dictionary<string, object?>
            {
                ["verb"] = verb,
                ["path"] = string.Join(" ", path),
            })
    {
        Verb = verb;
        Path = path;
    }

    private static string BuildMessage(string verb, IReadOnlyList<string> path)
    {
        var invocation = path.Count == 0
            ? verb
            : $"{verb} {string.Join(' ', path)}";
        return
            $"'az {invocation}' is not a recognized command. " +
            $"Run 'az {verb} --help' to see available commands.";
    }
}
