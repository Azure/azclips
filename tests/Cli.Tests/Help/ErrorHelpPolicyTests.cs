// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Xunit;

namespace Cli.Tests.Help;

/// <summary>
/// Covers the help-vs-error decisions that <c>Program.Main</c> applies in its catch blocks,
/// which cannot be asserted end-to-end because extension discovery varies by environment.
/// </summary>
public class ErrorHelpPolicyTests
{
    [Fact]
    public void ShouldShowGroupHelp_IsTrue_ForBareGroupInvocation()
    {
        var ex = new CommandNotFoundException("vm", []);

        Assert.True(ErrorHelpPolicy.ShouldShowGroupHelp(ex, ["vm"]));
    }

    [Fact]
    public void ShouldShowGroupHelp_IgnoresVerbCasing()
    {
        var ex = new CommandNotFoundException("vm", []);

        Assert.True(ErrorHelpPolicy.ShouldShowGroupHelp(ex, ["VM"]));
    }

    [Theory]
    [InlineData("--list")]
    [InlineData("foobar")]
    public void ShouldShowGroupHelp_IsFalse_WhenExtraTokensFollowTheGroup(string extraToken)
    {
        // These must keep the command-not-found exit code instead of rendering help.
        var ex = new CommandNotFoundException("vm", []);

        Assert.False(ErrorHelpPolicy.ShouldShowGroupHelp(ex, ["vm", extraToken]));
    }

    [Fact]
    public void ShouldShowGroupHelp_IsFalse_WhenSubcommandPathWasResolved()
    {
        var ex = new CommandNotFoundException("vm", ["foobar"]);

        Assert.False(ErrorHelpPolicy.ShouldShowGroupHelp(ex, ["vm", "foobar"]));
    }

    [Fact]
    public void ShouldShowCommandHelp_IsTrue_ForMissingRequiredParameters()
    {
        var ex = AzclipsErrorException.MissingRequiredParameters("Invalid arguments: Required parameter '--name' is missing.");

        Assert.IsType<MissingRequiredArgumentException>(ex);
        Assert.True(ErrorHelpPolicy.ShouldShowCommandHelp(ex, Invocation("vm create"), errorStreamIsHumanReadable: true));
    }

    [Fact]
    public void ShouldShowCommandHelp_IsFalse_WhenErrorStreamIsNotHumanReadable()
    {
        var ex = AzclipsErrorException.MissingRequiredParameters("Invalid arguments: Required parameter '--name' is missing.");

        Assert.False(ErrorHelpPolicy.ShouldShowCommandHelp(ex, Invocation("vm create"), errorStreamIsHumanReadable: false));
    }

    [Fact]
    public void ShouldShowCommandHelp_IsFalse_ForOtherUsageErrors()
    {
        var ex = AzclipsErrorException.UsageError("Unrecognized arguments for 'version': foo");

        Assert.False(ErrorHelpPolicy.ShouldShowCommandHelp(ex, Invocation("vm create"), errorStreamIsHumanReadable: true));
    }

    [Fact]
    public void ShouldShowCommandHelp_IsFalse_WhenNoCommandWasResolved()
    {
        var ex = AzclipsErrorException.MissingRequiredParameters("Invalid arguments: Required parameter '--name' is missing.");

        Assert.False(ErrorHelpPolicy.ShouldShowCommandHelp(ex, invocation: null, errorStreamIsHumanReadable: true));
    }

    [Fact]
    public void IsHumanReadableErrorStream_IsTrue_ForInteractiveStderr()
    {
        Assert.True(ErrorHelpPolicy.IsHumanReadableErrorStream(stderrIsRedirected: false));
    }

    [Fact]
    public void IsHumanReadableErrorStream_IsFalse_WhenStderrIsRedirected()
    {
        Assert.False(ErrorHelpPolicy.IsHumanReadableErrorStream(stderrIsRedirected: true));
    }

    [Fact]
    public void CommandHelpRequest_SplitsCommandNameIntoHelpPath()
    {
        var request = ErrorHelpPolicy.CommandHelpRequest(Invocation("vm create"));

        Assert.Equal(new[] { "vm", "create" }, request.Path);
    }

    private static CommandInvocation Invocation(string commandName)
        => new("Compute", commandName, new Dictionary<string, object>());
}
