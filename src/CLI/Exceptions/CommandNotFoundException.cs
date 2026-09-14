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
        : this(verb, path, Array.Empty<string>())
    {
    }

    public CommandNotFoundException(string verb, IReadOnlyList<string> path, IReadOnlyList<string> unrecognizedArguments)
        : base(
            code: "CommandNotFound",
            exitCode: 4,
            message: BuildMessage(verb, path, unrecognizedArguments),
            details: new Dictionary<string, object?>
            {
                ["verb"] = verb,
                ["path"] = string.Join(" ", path),
            })
    {
        Verb = verb;
        Path = path;
    }

    private static string BuildMessage(string verb, IReadOnlyList<string> path, IReadOnlyList<string> unrecognizedArguments)
    {
        // A bare group renders help before reaching here, so an empty path means trailing flags.
        if (path.Count == 0 && unrecognizedArguments.Count > 0)
        {
            return
                $"Unrecognized arguments for command group '{verb}': {string.Join(' ', unrecognizedArguments)}. " +
                $"Run 'az {verb} --help' to see available commands.";
        }

        var invocation = path.Count == 0
            ? verb
            : $"{verb} {string.Join(' ', path)}";
        return
            $"'az {invocation}' is not a recognized command. " +
            $"Run 'az {verb} --help' to see available commands.";
    }
}
