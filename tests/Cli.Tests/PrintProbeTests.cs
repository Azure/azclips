// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="PrintProbe"/>. Lightweight — surface is tiny.
/// </summary>
public class PrintProbeTests
{
    [Fact]
    public void TryHandle_ReturnsNull_WhenFirstArgIsNotProbeFlag()
    {
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["login"], writer);

        Assert.Null(exit);
        Assert.Empty(writer.ToString());
    }

    [Fact]
    public void TryHandle_ReturnsNull_WhenArgsEmpty()
    {
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle([], writer);

        Assert.Null(exit);
    }

    [Fact]
    public void TryHandle_List_PrintsRegisteredNames_Sorted()
    {
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow", "--list"], writer);

        Assert.Equal(0, exit);
        var lines = writer.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        // Sortedness, not exact contents — adding samples must not break the test.
        Assert.Equal(lines.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), lines);
    }

    [Fact]
    public void TryHandle_KnownName_PrintsArt()
    {
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow", "kitten"], writer);

        Assert.Equal(0, exit);
        Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public void TryHandle_UnknownName_ReturnsOne_AndNudgesTowardList()
    {
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow", "no-such-cat"], writer);

        Assert.Equal(1, exit);
        Assert.Contains("--list", writer.ToString());
    }

    [Fact]
    public void TryHandle_BareMeow_PrintsSomething()
    {
        // Every entry in Cats is non-empty, so randomness is safe here.
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow"], writer);

        Assert.Equal(0, exit);
        Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public void TryHandle_PrintProbeAlias_BehavesLikeMeow()
    {
        // --print-probe is the primary flag; --meow is retained as an alias.
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--print-probe", "--list"], writer);

        Assert.Equal(0, exit);
        Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public void TryHandle_HintMessagesEchoInvokedFlag()
    {
        // Users who typed `--meow` should see `--meow` in the hint text (and vice versa).
        var meowWriter = new StringWriter();
        PrintProbe.TryHandle(["--meow", "no-such-sample"], meowWriter);
        Assert.Contains("--meow", meowWriter.ToString());

        var probeWriter = new StringWriter();
        PrintProbe.TryHandle(["--print-probe", "no-such-sample"], probeWriter);
        Assert.Contains("--print-probe", probeWriter.ToString());
    }

    [Fact]
    public void TryHandle_ExtraTokens_PrintsUsage_AndReturnsOne()
    {
        // args.Length >= 3 — unknown shape. Covers the fall-through branch.
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow", "kitten", "extra"], writer);

        Assert.Equal(1, exit);
        Assert.Contains("Usage:", writer.ToString());
    }
}
