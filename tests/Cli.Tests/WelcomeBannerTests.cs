// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="WelcomeBanner"/>. Guards the content shape that scripts
/// and users grep against — the gating (bare `az` only) is a one-line check in
/// Program.Main and is exercised by manual smoke.
/// </summary>
public class WelcomeBannerTests
{
    [Fact]
    public void Write_ProducesNonEmptyOutput()
    {
        var writer = new StringWriter();

        WelcomeBanner.Write(writer);

        Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public void Write_ContainsProductNameAndHelpHint()
    {
        // Regression guard for the two things scripts / newcomers latch onto:
        // the product name and the pointer to `az --help`.
        var writer = new StringWriter();

        WelcomeBanner.Write(writer);
        var output = writer.ToString();

        Assert.Contains("Azure CLI", output);
        Assert.Contains("az --help", output);
    }

    [Fact]
    public void Write_StartsAndEndsWithBlankLine()
    {
        // Leading/trailing blank lines separate the banner from the shell prompt
        // and from the root help output below it.
        var writer = new StringWriter();

        WelcomeBanner.Write(writer);
        var lines = writer.ToString().Split(Environment.NewLine);

        Assert.Equal(string.Empty, lines[0]);
        Assert.Equal(string.Empty, lines[^2]); // last real line before the trailing newline
    }
}
