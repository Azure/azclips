// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.TsvFormatter"/> -- same shape decisions as
/// <see cref="Core.Cli.Output.TableFormatter"/> minus header/column-width logic.
/// </summary>
public class TsvFormatterTests
{
    [Fact]
    public void Format_ShouldRenderTabSeparatedRows_ForArrayOfObjects_WithoutHeader()
    {
        var json = """[{"name":"rg1","location":"eastus"},{"name":"rg2","location":"westus2"}]""";

        var result = Core.Cli.Output.TsvFormatter.Format(json);

        Assert.Equal("rg1\teastus\nrg2\twestus2", result);
    }

    [Fact]
    public void Format_ShouldRenderTabSeparatedValues_ForSingleObject()
    {
        var json = """{"name":"rg1","location":"eastus"}""";

        var result = Core.Cli.Output.TsvFormatter.Format(json);

        Assert.Equal("rg1\teastus", result);
    }

    [Fact]
    public void Format_ShouldRenderOnePerLine_ForArrayOfScalars()
    {
        var json = """["alpha","beta"]""";

        var result = Core.Cli.Output.TsvFormatter.Format(json);

        Assert.Equal("alpha\nbeta", result);
    }

    [Fact]
    public void Format_ShouldReturnEmptyString_ForEmptyArray()
    {
        var result = Core.Cli.Output.TsvFormatter.Format("[]");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ShouldFallBackToSingleColumn_ForMixedArray()
    {
        // Regression: taking the object row path off only the first item's ValueKind crashed
        // with InvalidOperationException on TryGetProperty once a later item wasn't an object.
        var json = """[{"a":1},2]""";

        var result = Core.Cli.Output.TsvFormatter.Format(json);

        Assert.Equal("""{"a":1}""" + "\n2", result);
    }

    [Fact]
    public void Format_ShouldDropNestedProperty_ForSingleObject()
    {
        var json = """{"name":"rg1","tags":{"env":"prod"}}""";

        var result = Core.Cli.Output.TsvFormatter.Format(json);

        Assert.Equal("rg1", result);
    }
}
