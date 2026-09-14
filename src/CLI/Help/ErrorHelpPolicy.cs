// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;

namespace Core.Cli.Help;

/// <summary>
/// Decides when a dispatch or validation failure should render help instead of only an error.
/// Kept out of <see cref="Program"/> so each rule is named at the call site and unit-testable.
/// </summary>
internal static class ErrorHelpPolicy
{
    /// <summary>
    /// True for a bare group invocation such as <c>az vm</c>. Extra tokens (<c>az vm --list</c>)
    /// stay on the structured-error path, keeping their command-not-found exit code.
    /// </summary>
    internal static bool ShouldShowGroupHelp(CommandNotFoundException ex, string[] positional)
        => ex.Path.Count == 0
        && positional.Length == 1
        && positional[0].Equals(ex.Verb, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the error stream belongs to a human: stderr is a terminal and the caller did not
    /// ask for a machine-readable error format. Rendering a help page anywhere else would corrupt
    /// the structured error already written to that stream.
    /// </summary>
    /// <param name="requestedFormat">
    /// <c>GlobalArgs.Output</c>, which is null unless --output was passed explicitly, so the
    /// configured default format alone does not suppress help for interactive users.
    /// </param>
    internal static bool IsHumanReadableErrorStream(bool stderrIsRedirected, OutputFormat? requestedFormat)
        => !stderrIsRedirected
        && requestedFormat is not (OutputFormat.Json or OutputFormat.Jsonc);

    /// <summary>
    /// True when a resolved command failed only because required parameters were missing,
    /// so its help page is worth rendering beneath the error.
    /// </summary>
    internal static bool ShouldShowCommandHelp(
        AzclipsErrorException ex,
        [NotNullWhen(true)] CommandInvocation? invocation,
        bool errorStreamIsHumanReadable)
        => errorStreamIsHumanReadable
        && invocation is not null
        && ex is UsageErrorException { HasMissingRequiredParameters: true };

    internal static HelpRequest CommandHelpRequest(CommandInvocation invocation)
        => new(invocation.CommandName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
