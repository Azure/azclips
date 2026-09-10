// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.TsvFormatter"/> -- same shape decisions as
/// <see cref="Core.Cli.TableFormatter"/> minus header/column-width logic.
/// </summary>
public class TsvFormatterTests
{
    [Fact]
    public void Format_ShouldRenderTabSeparatedRows_ForArrayOfObjects_WithoutHeader()
    {
        var json = """[{"name":"rg1","location":"eastus"},{"name":"rg2","location":"westus2"}]""";

        var result = Core.Cli.TsvFormatter.Format(json);

        Assert.Equal("rg1\teastus\nrg2\twestus2", result);
    }

    [Fact]
    public void Format_ShouldRenderTabSeparatedValues_ForSingleObject()
    {
        var json = """{"name":"rg1","location":"eastus"}""";

        var result = Core.Cli.TsvFormatter.Format(json);

        Assert.Equal("rg1\teastus", result);
    }

    [Fact]
    public void Format_ShouldRenderOnePerLine_ForArrayOfScalars()
    {
        var json = """["alpha","beta"]""";

        var result = Core.Cli.TsvFormatter.Format(json);

        Assert.Equal("alpha\nbeta", result);
    }

    [Fact]
    public void Format_ShouldReturnEmptyString_ForEmptyArray()
    {
        var result = Core.Cli.TsvFormatter.Format("[]");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ShouldInlineNestedValueAsCompactJson()
    {
        var json = """{"name":"rg1","tags":{"env":"prod"}}""";

        var result = Core.Cli.TsvFormatter.Format(json);

        Assert.Equal("rg1\t{\"env\":\"prod\"}", result);
    }
}
