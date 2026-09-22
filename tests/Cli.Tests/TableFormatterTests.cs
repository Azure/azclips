// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.TableFormatter"/> covering the three shape decisions: array of
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

        var result = Core.Cli.Output.TableFormatter.Format(json);
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

        var result = Core.Cli.Output.TableFormatter.Format(json);
        var lines = result.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal(new[] { "name", "rg1" }, Columns(lines[0]));
        Assert.Equal(new[] { "location", "eastus" }, Columns(lines[1]));
    }

    [Fact]
    public void Format_ShouldRenderSingleUnheadedColumn_ForArrayOfScalars()
    {
        var json = """["alpha","beta","gamma"]""";

        var result = Core.Cli.Output.TableFormatter.Format(json);

        Assert.Equal("alpha\nbeta\ngamma", result);
    }

    [Fact]
    public void Format_ShouldReturnEmptyString_ForEmptyArray()
    {
        var result = Core.Cli.Output.TableFormatter.Format("[]");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ShouldFallBackToSingleColumn_ForMixedArray()
    {
        // Regression: taking the object header/row path off only the first item's ValueKind
        // crashed with InvalidOperationException on TryGetProperty once a later item wasn't an object.
        var json = """[{"a":1},2]""";

        var result = Core.Cli.Output.TableFormatter.Format(json);

        Assert.Equal("""{"a":1}""" + "\n2", result);
    }

    [Fact]
    public void Format_ShouldDropNestedColumnEntirely_ForArrayOfObjects()
    {
        var json = """[{"name":"rg1","tags":{"env":"prod"}}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json);
        var lines = result.Split('\n');

        Assert.Equal(new[] { "name" }, Columns(lines[0]));
        Assert.DoesNotContain("env", result);
        Assert.DoesNotContain("tags", result);
    }

    [Fact]
    public void Format_ShouldDropNestedProperty_ForSingleObject()
    {
        var json = """{"name":"rg1","tags":{"env":"prod"}}""";

        var result = Core.Cli.Output.TableFormatter.Format(json);

        Assert.Equal("name  rg1", result);
    }

    [Fact]
    public void Format_ShouldRenderBooleansCapitalized()
    {
        var json = """[{"name":"rg1","enabled":true},{"name":"rg2","enabled":false}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json);

        Assert.Contains("True", result);
        Assert.Contains("False", result);
    }

    [Fact]
    public void Format_ShouldConstrainColumnsToTerminalWidth()
    {
        var json = """[{"name":"resource-group-with-a-long-name","location":"eastus"}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 30);

        Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 30, line));
        Assert.Contains("...", result);
    }

    [Fact]
    public void Format_ShouldConstrainSeparatorsAtMinimumTwoColumnWidth()
    {
        var json = """[{"name":"alpha","location":"beta"}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 3);

        Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 3, line));
    }

    [Fact]
    public void Format_ShouldNotTruncateWideCjkWhenItFitsDisplayWidth()
    {
        var json = """[{"name":"漢字漢字","location":"eastus"}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 8);

        Assert.All(result.Split('\n'), line => Assert.True(MeasureDisplayCells(line) <= 8, line));
    }

    [Fact]
    public void Format_ShouldConstrainEmojiByDisplayWidth()
    {
        var json = """["😀😀😀😀"]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 4);

        Assert.All(result.Split('\n'), line => Assert.True(MeasureDisplayCells(line) <= 4, line));
    }

    private static int MeasureDisplayCells(string value)
    {
        var total = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is 0x0A or 0x0D)
            {
                continue;
            }

            total += IsWideRune(rune) ? 2 : 1;
        }

        return total;
    }

    private static bool IsWideRune(Rune rune)
    {
        var value = rune.Value;
        return value switch
        {
            >= 0x1100 and <= 0x115F => true,
            >= 0x2E80 and <= 0xA4CF => true,
            >= 0xAC00 and <= 0xD7A3 => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0xFE30 and <= 0xFE4F => true,
            >= 0xFF00 and <= 0xFF60 => true,
            >= 0xFFE0 and <= 0xFFE6 => true,
            >= 0x1F300 and <= 0x1FAFF => true,
            >= 0x2600 and <= 0x27BF => true,
            _ => false
        };
    }
}
