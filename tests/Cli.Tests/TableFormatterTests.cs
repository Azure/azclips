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
    public void Format_ShouldOverflowRatherThanEraseColumns_WhenTerminalIsTooNarrow()
    {
        var json = """[{"name":"alpha","location":"beta"}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 3);

        Assert.Equal("name  location\n----  --------\na...  beta", result);
    }

    [Theory]
    [InlineData(0, "abcdefghij  uvwxyzabcd  x")]
    [InlineData(17, "abc...  uvw...  x")]
    [InlineData(18, "abcd...  uvw...  x")]
    [InlineData(3, "a...  u...  x")]
    public void FormatRows_ShouldShareShrinkageAndPreserveShortColumns(int terminalWidth, string expected)
    {
        var rows = new List<string[]> { new[] { "abcdefghij", "uvwxyzabcd", "x" } };

        var result = Core.Cli.Output.TableFormatter.FormatRows(rows, terminalWidth: terminalWidth);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatRows_ShouldAllocateRemainingSpaceToShrinkableColumns()
    {
        var rows = new List<string[]> { new[] { "long-value-1234567890", "abcdefghij", "uvwxyzabcd" } };

        var result = Core.Cli.Output.TableFormatter.FormatRows(rows, ["LongHeader", "B", "C"], terminalWidth: 25);
        var lines = result.Split('\n');

        Assert.Equal("----------  ------  -----", lines[1]);
        Assert.Equal(new[] { "long-va...", "abc...", "uv..." }, Columns(lines[2]));
        Assert.All(lines, line => Assert.True(line.Length <= 25, line));
    }

    [Theory]
    [InlineData(260)]
    [InlineData(200)]
    [InlineData(151)]
    [InlineData(150)]
    [InlineData(149)]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    public void Format_ShouldPreserveUserHeadersAndPopulatedColumns(int terminalWidth)
    {
        var json = """
            [{"id":"00000000-0000-0000-0000-000000000001",
              "displayName":"Example User","givenName":"Example","surname":"User",
              "userPrincipalName":"example.user@example.invalid","mail":"example.user@example.invalid",
              "jobTitle":null,"mobilePhone":null,"officeLocation":null,"preferredLanguage":null,
              "businessPhones":[],"deletedDateTime":null,"usageLocation":null,
              "accountEnabled":null,"mailNickname":null}]
            """;
        var expectedHeaders = new[]
        {
            "id", "displayName", "givenName", "surname", "userPrincipalName", "mail",
            "jobTitle", "mobilePhone", "officeLocation", "preferredLanguage",
            "deletedDateTime", "usageLocation", "accountEnabled", "mailNickname"
        };

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth);
        var lines = result.Split('\n');
        var widths = Columns(lines[1]).Select(column => column.Length).ToArray();

        Assert.Equal(expectedHeaders, Columns(lines[0]));
        var offset = 0;
        var populated = new[]
        {
            "00000000-0000-0000-0000-000000000001", "Example User", "Example",
            "User", "example.user@example.invalid", "example.user@example.invalid"
        };
        for (var i = 0; i < populated.Length; i++)
        {
            var cell = lines[2].Substring(offset, widths[i]).TrimEnd();
            Assert.StartsWith(populated[i][..1], cell);
            Assert.True(cell == populated[i] || cell.EndsWith("..."), cell);
            offset += widths[i] + 2;
        }

        // Full headers plus the four-character minimum for ID require 182 cells.
        Assert.All(lines, line => Assert.True(line.Length <= Math.Max(terminalWidth, 182), line));
        if (terminalWidth == 260)
            Assert.Equal(Core.Cli.Output.TableFormatter.Format(json), result);
    }

    [Fact]
    public void Format_ShouldPreserveWideCjkWhenItFitsDisplayWidth()
    {
        var json = """["漢字漢字"]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 8);

        Assert.Equal("漢字漢字", result);
        Assert.All(result.Split('\n'), line => Assert.True(MeasureDisplayCells(line) <= 8, line));
    }

    [Fact]
    public void Format_ShouldPreserveFirstEmojiWhenMinimumWidthExceedsTerminal()
    {
        var json = """["😀😀😀😀"]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 4);

        Assert.Equal("😀...", result);
        Assert.Equal(5, MeasureDisplayCells(result));
    }

    [Theory]
    [InlineData("""[{"name":"漢字漢","icon":"😀😀😀"}]""")]
    [InlineData("""[{"name":"abcdef","icon":"abcdef"},{"name":"漢字漢","icon":"😀😀😀"}]""")]
    public void Format_ShouldPreserveFirstWideRuneInEveryRow_WhenColumnsOverflow(string json)
    {
        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 10);
        var lines = result.Split('\n');

        Assert.Equal(new[] { "name", "icon" }, Columns(lines[0]));
        Assert.Equal("-----  -----", lines[1]);
        Assert.Equal("漢...  😀...", lines[^1]);
        Assert.Equal(12, MeasureDisplayCells(lines[^1]));
    }

    [Fact]
    public void Format_ShouldNotReserveEllipsisSpaceForShortWideValues()
    {
        var json = """[{"name":"漢"},{"name":"abcdef"}]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 4);

        Assert.Equal("name\n----\n漢\na...", result);
    }

    [Fact]
    public void Format_ShouldExpandTabsBeforeMeasuringDisplayWidth()
    {
        var json = """["a\t"]""";

        var result = Core.Cli.Output.TableFormatter.Format(json, terminalWidth: 8);

        Assert.All(result.Split('\n'), line => Assert.True(MeasureDisplayCells(line) <= 8, line));
    }

    [Fact]
    public void FormatRows_ShouldAlignColumnsWithHeadersAndSeparators()
    {
        var headers = new[] { "Extension", "Duration", "Source", "Assembly" };
        var rows = new List<string[]>
        {
            new[] { "Compute", "12.3 ms", "Bundled", "/path/to/compute.dll" },
            new[] { "Storage", "5.1 ms", "Cached", "/path/to/storage.dll" }
        };

        var result = Core.Cli.Output.TableFormatter.FormatRows(rows, headers);
        var lines = result.Split('\n');

        Assert.Equal(4, lines.Length);
        Assert.Equal(headers, Columns(lines[0]));
        Assert.Matches("^-+\\s+-+\\s+-+\\s+-+$", lines[1]);
        Assert.Contains("Compute", lines[2]);
        Assert.Contains("12.3 ms", lines[2]);
        Assert.Contains("Storage", lines[3]);
        Assert.Contains("Cached", lines[3]);
    }

    [Fact]
    public void FormatRows_ShouldOmitSeparatorLine_WhenIncludeHeaderSeparatorIsFalse()
    {
        var headers = new[] { "Extension", "Loader", "Time (ms)" };
        var rows = new List<string[]>
        {
            new[] { "Compute", "Bundled", "12.3" }
        };

        var result = Core.Cli.Output.TableFormatter.FormatRows(rows, headers, includeHeaderSeparator: false);
        var lines = result.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal(headers, Columns(lines[0]));
        Assert.DoesNotContain("---", result);
        Assert.Contains("Compute", lines[1]);
    }

    [Fact]
    public void FormatRows_ShouldHandleRaggedRowsGracefully()
    {
        var headers = new[] { "Col1", "Col2", "Col3" };
        var rows = new List<string[]>
        {
            new[] { "Val1" },
            new[] { "Val1", "Val2", "Val3", "Val4" }
        };

        var result = Core.Cli.Output.TableFormatter.FormatRows(rows, headers);
        var lines = result.Split('\n');
        Assert.All(lines, line => Assert.Equal(line.TrimEnd(), line));
        Assert.Equal("Val1", lines[2]);
    }

    [Fact]
    public void FormatRows_ShouldReturnEmptyString_WhenEmptyAndNoHeaders()
    {
        var result = Core.Cli.Output.TableFormatter.FormatRows(new List<string[]>());
        Assert.Equal(string.Empty, result);
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
