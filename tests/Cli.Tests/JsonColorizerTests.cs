// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using static Core.Cli.JsonColorizer;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.JsonColorizer"/> ensuring keys, string values, numbers, and
/// literals are highlighted correctly and structural characters/whitespace are preserved.
/// </summary>
public class JsonColorizerTests
{
    [Fact]
    public void Colorize_ShouldWrapKeyAndStringValue_WithDistinctColors()
    {
        var result = Core.Cli.JsonColorizer.Colorize("""{"name":"rg1"}""");

        Assert.Equal($$"""{{{KeyColor}}"name"{{Reset}}:{{StringColor}}"rg1"{{Reset}}}""", result);
    }

    [Fact]
    public void Colorize_ShouldWrapNumber()
    {
        var result = Core.Cli.JsonColorizer.Colorize("""{"count":42}""");

        Assert.Contains($"{NumberColor}42{Reset}", result);
    }

    [Fact]
    public void Colorize_ShouldWrapNegativeAndDecimalNumbers()
    {
        var result = Core.Cli.JsonColorizer.Colorize("""{"a":-1,"b":3.14}""");

        Assert.Contains($"{NumberColor}-1{Reset}", result);
        Assert.Contains($"{NumberColor}3.14{Reset}", result);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public void Colorize_ShouldWrapLiterals(string literal)
    {
        var result = Core.Cli.JsonColorizer.Colorize($$"""{"flag":{{literal}}}""");

        Assert.Contains($"{LiteralColor}{literal}{Reset}", result);
    }

    [Fact]
    public void Colorize_ShouldNotColorizeStructuralCharacters()
    {
        var result = Core.Cli.JsonColorizer.Colorize("""{"items":[1,2]}""");

        // Structural characters (braces, brackets, colon, comma) are copied through verbatim,
        // never wrapped in a color code themselves.
        Assert.Contains("{" + KeyColor, result);
        Assert.Contains("]}", result);
    }

    [Fact]
    public void Colorize_ShouldPreserveEscapedQuotesInsideStringValue()
    {
        var value = new { content = "{\"inner\":true}" };
        var json = JsonSerializer.Serialize(value);

        var result = Core.Cli.JsonColorizer.Colorize(json);
        var stripped = StripAnsi(result);

        using var doc = JsonDocument.Parse(stripped);
        Assert.Equal("{\"inner\":true}", doc.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public void Colorize_ShouldPreserveIndentationWhitespace()
    {
        var json = "{\n  \"name\": \"rg1\"\n}";

        var result = Core.Cli.JsonColorizer.Colorize(json);

        Assert.Contains("{\n  ", result);
        Assert.Contains("\n}", result);
    }

    [Fact]
    public void Colorize_StrippedOfAnsiCodes_ShouldRoundTripToIdenticalJson()
    {
        var value = new { name = "rg1", count = 3, active = true, tag = (string?)null, items = new[] { 1, 2 } };
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });

        var colorized = Core.Cli.JsonColorizer.Colorize(json);
        var stripped = StripAnsi(colorized);

        Assert.Equal(json, stripped);
    }

    [Fact]
    public void Colorize_ShouldReturnUnchanged_WhenJsonHasNoColorableTokens()
    {
        var result = Core.Cli.JsonColorizer.Colorize("[]");

        Assert.Equal("[]", result);
    }

    private static string StripAnsi(string text) => Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);
}
