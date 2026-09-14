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
    /// True when a resolved command failed only because required parameters were missing,
    /// so its help page is worth rendering beneath the error.
    /// </summary>
    /// <param name="stderrIsRedirected">
    /// Pass <see cref="Console.IsErrorRedirected"/>. When stderr is piped the help page is
    /// suppressed so the structured error stays parseable; the message keeps the --help pointer.
    /// Taken as a parameter rather than read here so the rule stays unit-testable, matching
    /// <c>ColorPolicy.ShouldEmitColor</c>.
    /// </param>
    internal static bool ShouldShowCommandHelp(
        AzclipsErrorException ex,
        [NotNullWhen(true)] CommandInvocation? invocation,
        bool stderrIsRedirected)
        => !stderrIsRedirected
        && invocation is not null
        && ex is UsageErrorException { HasMissingRequiredParameters: true };

    internal static HelpRequest CommandHelpRequest(CommandInvocation invocation)
        => new(invocation.CommandName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
