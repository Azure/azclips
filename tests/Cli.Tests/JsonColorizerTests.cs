// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.JsonColorizer"/> ensuring JSON tokens are wrapped in the
/// expected ANSI color codes while leaving structural characters uncolored, and that the
/// colorized text still parses back to the same JSON once escape codes are stripped.
/// </summary>
public class JsonColorizerTests
{
    private const string Reset = "\u001b[0m";
    private const string KeyColor = "\u001b[36m";
    private const string StringColor = "\u001b[32m";
    private const string NumberColor = "\u001b[33m";
    private const string LiteralColor = "\u001b[35m";

    private static readonly Regex AnsiPattern = new(@"\u001b\[[0-9;]*m", RegexOptions.Compiled);

    [Fact]
    public void Colorize_WrapsKeysAndStringValues_WithDistinctColors()
    {
        var json = "{\n  \"name\": \"test-rg\"\n}";

        var result = Core.Cli.JsonColorizer.Colorize(json);

        Assert.Contains($"{KeyColor}\"name\"{Reset}:", result);
        Assert.Contains($"{StringColor}\"test-rg\"{Reset}", result);
    }

    [Fact]
    public void Colorize_WrapsNumbers_WithNumberColor()
    {
        var json = "{\n  \"count\": 3.5\n}";

        var result = Core.Cli.JsonColorizer.Colorize(json);

        Assert.Contains($"{NumberColor}3.5{Reset}", result);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public void Colorize_WrapsLiterals_WithLiteralColor(string literal)
    {
        var json = $"{{\n  \"flag\": {literal}\n}}";

        var result = Core.Cli.JsonColorizer.Colorize(json);

        Assert.Contains($"{LiteralColor}{literal}{Reset}", result);
    }

    [Fact]
    public void Colorize_LeavesStructuralCharacters_Uncolored()
    {
        var json = "[1, 2]";

        var result = Core.Cli.JsonColorizer.Colorize(json);

        Assert.Contains("[", result);
        Assert.Contains("]", result);
        Assert.Contains(", ", result);
    }

    [Fact]
    public void Colorize_DoesNotMangle_EscapedQuotesInsideStringValue()
    {
        var value = new { Content = "{\"name\":\"rg1\"}" };
        var json = JsonSerializer.Serialize(value, value.GetType(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var result = Core.Cli.JsonColorizer.Colorize(json);
        var stripped = AnsiPattern.Replace(result, string.Empty);

        Assert.Equal(json, stripped);
    }

    [Fact]
    public void Colorize_RoundTrips_ToEquivalentJson_AfterStrippingAnsiCodes()
    {
        var obj = new { Name = "rg1", Count = 2, Active = true, Note = (string?)null, Tags = new[] { "a", "b" } };
        var json = JsonSerializer.Serialize(obj, obj.GetType(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var colorized = Core.Cli.JsonColorizer.Colorize(json);
        var stripped = AnsiPattern.Replace(colorized, string.Empty);

        using var doc = JsonDocument.Parse(stripped);
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        Assert.True(doc.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("note").ValueKind);
        Assert.Equal(2, doc.RootElement.GetProperty("tags").GetArrayLength());
    }
}
