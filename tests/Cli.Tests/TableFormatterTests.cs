// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.TableFormatter"/> covering the three shape decisions: array of
/// objects (multi-column, headers from the first item), single object (2-column key/value), and
/// array of scalars (single unheaded column).
/// </summary>
public class TableFormatterTests
{
    private static string[] Columns(string line) => Regex.Split(line.Trim(), "\\s{2,}");

    [Fact]
    public void Format_ShouldRenderHeaderAndDataRows_ForArrayOfObjects()
    {
        var json = """[{"name":"rg1","location":"eastus"},{"name":"rg-two","location":"westus2"}]""";

        var result = Core.Cli.TableFormatter.Format(json);
        var lines = result.Split('\n');

        Assert.Equal(4, lines.Length);
        Assert.Equal(new[] { "name", "location" }, Columns(lines[0]));
        Assert.Matches("^-+  -+$", lines[1]);
        Assert.Equal(new[] { "rg1", "eastus" }, Columns(lines[2]));
        Assert.Equal(new[] { "rg-two", "westus2" }, Columns(lines[3]));
    }

    [Fact]
    public void Format_ShouldRenderTwoColumnKeyValue_ForSingleObject()
    {
        var json = """{"name":"rg1","location":"eastus"}""";

        var result = Core.Cli.TableFormatter.Format(json);
        var lines = result.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal(new[] { "name", "rg1" }, Columns(lines[0]));
        Assert.Equal(new[] { "location", "eastus" }, Columns(lines[1]));
    }

    [Fact]
    public void Format_ShouldRenderSingleUnheadedColumn_ForArrayOfScalars()
    {
        var json = """["alpha","beta","gamma"]""";

        var result = Core.Cli.TableFormatter.Format(json);

        Assert.Equal("alpha\nbeta\ngamma", result);
    }

    [Fact]
    public void Format_ShouldReturnEmptyString_ForEmptyArray()
    {
        var result = Core.Cli.TableFormatter.Format("[]");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ShouldInlineNestedValueAsCompactJson()
    {
        var json = """[{"name":"rg1","tags":{"env":"prod"}}]""";

        var result = Core.Cli.TableFormatter.Format(json);

        Assert.Contains("""{"env":"prod"}""", result);
        Assert.DoesNotContain("\n ", result); // nested value must not introduce embedded newlines
    }

    [Fact]
    public void Format_ShouldRenderBooleansCapitalized()
    {
        var json = """[{"name":"rg1","enabled":true},{"name":"rg2","enabled":false}]""";

        var result = Core.Cli.TableFormatter.Format(json);

        Assert.Contains("True", result);
        Assert.Contains("False", result);
    }
}
