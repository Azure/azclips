// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for the hidden <c>--meow</c> easter egg. Kept lightweight — behavior surface
/// is small and intentionally does not appear in <c>az --help</c>.
/// </summary>
public class PrintProbeTests
{
    [Fact]
    public void TryHandle_ReturnsNull_WhenFirstArgIsNotMeow()
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
        // Roster is authoritative and case-insensitively sorted; make the test resilient
        // to future additions by checking sortedness, not exact contents.
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
        // Randomness is fine here — every entry in Cats produces non-empty output.
        var writer = new StringWriter();

        var exit = PrintProbe.TryHandle(["--meow"], writer);

        Assert.Equal(0, exit);
        Assert.NotEmpty(writer.ToString());
    }
}
