// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Cli.Parser;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.CliOutputFormatter"/> ensuring CLI output
/// correctly renders strings, primitives, complex objects, and collections.
/// </summary>
// One test redirects the process-global Console.Out to capture output end-to-end.
// Join the non-parallel "Azclips Service Provider Tests" collection so it never runs
// concurrently with engine-based tests that write log lines to Console.
[Collection("Azclips Service Provider Tests")]
public class CliOutputFormatterTests
{
    // ── String pass-through ──────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnString_WhenValueIsString()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsEmptyString()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("");
        Assert.Equal("", result);
    }

    // ── Null handling ────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsNull()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(null);
        Assert.Equal(string.Empty, result);
    }

    // ── Primitives ───────────────────────────────────────────────────

    [Theory]
    [InlineData(42, "42")]
    [InlineData(3.14, "3.14")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData('x', "\"x\"")]
    public void FormatValue_ShouldEmitJsonLiteral_WhenValueIsPrimitive(object value, string expected)
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);
        Assert.Equal(expected, result);
    }

    // ── Complex objects → JSON ───────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeToJson_WhenValueIsComplexObject()
    {
        var obj = new { Name = "test-rg", Location = "eastus" };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(obj);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("test-rg", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("eastus", doc.RootElement.GetProperty("location").GetString());
    }

    // ── Format routing: table/tsv/yaml delegate to their formatters ──

    [Fact]
    public void FormatValue_ShouldRouteToTableFormatter_WhenFormatIsTable()
    {
        var arr = new[] { new { Name = "rg1" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr, OutputFormat.Table);

        Assert.Equal(Core.Cli.Output.TableFormatter.Format("""[{"name":"rg1"}]"""), result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToTsvFormatter_WhenFormatIsTsv()
    {
        var arr = new[] { new { Name = "rg1" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr, OutputFormat.Tsv);

        Assert.Equal("rg1", result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToYamlFormatter_WhenFormatIsYaml()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yaml);

        Assert.Equal("name: rg1", result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToYamlFormatter_WhenFormatIsYamlc()
    {
        // Yamlc coloring is tracked separately (azclips#836) -- currently identical to Yaml.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yamlc, colorPolicyAllowsColor: true);

        Assert.Equal("name: rg1", result);
    }

    // ── Arrays / Collections → JSON array ────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeArray_WhenValueIsArray()
    {
        var arr = new[] { new { Name = "rg1" }, new { Name = "rg2" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("rg1", doc.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("rg2", doc.RootElement[1].GetProperty("name").GetString());
    }

    [Fact]
    public void FormatValue_ShouldSerializeList_WhenValueIsList()
    {
        var list = new List<string> { "alpha", "beta", "gamma" };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(list);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal("alpha", doc.RootElement[0].GetString());
    }

    // ── Dictionaries → JSON object ──────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeDictionary_WhenValueIsDictionary()
    {
        var dict = new Dictionary<string, string>
        {
            ["env"] = "prod",
            ["team"] = "platform"
        };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(dict);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("prod", doc.RootElement.GetProperty("env").GetString());
        Assert.Equal("platform", doc.RootElement.GetProperty("team").GetString());
    }

    // ── Nested structures ───────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeNestedObjects_WhenValueHasNestedProperties()
    {
        var nested = new
        {
            Name = "rg1",
            Tags = new Dictionary<string, string> { ["env"] = "test" },
            Items = new[] { 1, 2, 3 }
        };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(nested);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // ── Decimal (non-primitive, non-string value type) ──────────────

    [Fact]
    public void FormatValue_ShouldEmitJsonLiteral_WhenValueIsDecimal()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(99.99m);
        Assert.Equal("99.99", result);
    }

    // ── Serialization errors rethrown ──────────────────────────────

    [Fact]
    public void FormatValue_ShouldThrow_WhenSerializationFails()
    {
        var unserializable = new UnserializableType();

        Assert.ThrowsAny<JsonException>(() => Core.Cli.Output.CliOutputFormatter.FormatValue(unserializable));
    }

    // ── Empty array ─────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeEmptyArray_WhenValueIsEmptyArray()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(Array.Empty<object>());
        Assert.Equal("[]", result);
    }

    // ── Integration: captures console output end-to-end ─────────────

    [Fact]
    public void FormatValue_ShouldWriteFormattedJsonToConsole_WhenValueIsArray()
    {
        var items = new[] { new { Id = "1", Name = "rg1" }, new { Id = "2", Name = "rg2" } };

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Console.WriteLine(Core.Cli.Output.CliOutputFormatter.FormatValue(items));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        using var doc = JsonDocument.Parse(output.Trim());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    // ── Relaxed escaping: raw JSON strings stay human-readable ──────

    [Fact]
    public void FormatValue_ShouldNotUnicodeEscapeQuotes_WhenValueContainsRawJsonString()
    {
        // Mirrors the `rest` command's RestResponse.Content: a property whose value is itself a raw
        // JSON body. The inner quotes must render as `"` (not `\u0022`) for readable console output.
        var value = new { Content = "{\"name\":\"rg1\"}" };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);

        Assert.DoesNotContain("\\u0022", result);
        Assert.Contains("{\\\"name\\\":\\\"rg1\\\"}", result);
    }

    [Fact]
    public void FormatValue_ShouldNotUnicodeEscapeNonAscii_WhenValueContainsNonAsciiText()
    {
        var value = new { Name = "café-café" };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);

        Assert.Contains("café-café", result);
        Assert.DoesNotContain("\\u00e9", result);
    }

    // ── Colorize opt-in ──────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldNotColorize_WhenColorizeParamOmitted()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" });

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldColorizeJson_WhenFormatIsJsoncAndPolicyAllowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Contains('\u001b', result);
        using var doc = JsonDocument.Parse(StripAnsi(result));
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeJson_WhenFormatIsJsonEvenIfPolicyAllowsColor()
    {
        // json (no 'c') never colorizes, regardless of core.output.color -- deterministic output
        // for scripting even if the user has core.output.color=always configured globally.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Json, colorPolicyAllowsColor: true);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeJsonc_WhenPolicyDisallowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Jsonc, colorPolicyAllowsColor: false);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeStrings_WhenColorizeIsTrue()
    {
        // Strings render as-is (not JSON), so colorize has no effect on them.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("hello world", OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizePrimitives_WhenColorizeIsTrue()
    {
        // Primitives/decimals are out of scope for colorizing (matches the primitive branch,
        // which renders as a single bare JSON literal with little to gain from highlighting).
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(42, OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Equal("42", result);
    }

    private static string StripAnsi(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);

    /// <summary>
    /// A type that intentionally fails JSON serialization by containing a self-reference.
    /// </summary>
    private class UnserializableType
    {
        public UnserializableType Self => this;
        public override string ToString() => "UnserializableType:fallback";
    }
}
