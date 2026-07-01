// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.CliOutputFormatter"/> ensuring CLI output
/// correctly renders strings, primitives, complex objects, and collections.
/// </summary>
// One test redirects the process-global Console.Out to capture output end-to-end.
// Join the non-parallel "Cirrus Service Provider Tests" collection so it never runs
// concurrently with engine-based tests that write log lines to Console.
[Collection("Cirrus Service Provider Tests")]
public class CliOutputFormatterTests
{
    // ── String pass-through ──────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnString_WhenValueIsString()
    {
        var result = Core.Cli.CliOutputFormatter.FormatValue("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsEmptyString()
    {
        var result = Core.Cli.CliOutputFormatter.FormatValue("");
        Assert.Equal("", result);
    }

    // ── Null handling ────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsNull()
    {
        var result = Core.Cli.CliOutputFormatter.FormatValue(null);
        Assert.Equal(string.Empty, result);
    }

    // ── Primitives ───────────────────────────────────────────────────

    [Theory]
    [InlineData(42)]
    [InlineData(3.14)]
    [InlineData(true)]
    [InlineData('x')]
    public void FormatValue_ShouldUseToString_WhenValueIsPrimitive(object value)
    {
        var expected = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value);
        var result = Core.Cli.CliOutputFormatter.FormatValue(value);
        Assert.Equal(expected, result);
    }

    // ── Complex objects → JSON ───────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeToJson_WhenValueIsComplexObject()
    {
        var obj = new { Name = "test-rg", Location = "eastus" };
        var result = Core.Cli.CliOutputFormatter.FormatValue(obj);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("test-rg", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("eastus", doc.RootElement.GetProperty("location").GetString());
    }

    // ── Arrays / Collections → JSON array ────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeArray_WhenValueIsArray()
    {
        var arr = new[] { new { Name = "rg1" }, new { Name = "rg2" } };
        var result = Core.Cli.CliOutputFormatter.FormatValue(arr);

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
        var result = Core.Cli.CliOutputFormatter.FormatValue(list);

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
        var result = Core.Cli.CliOutputFormatter.FormatValue(dict);

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
        var result = Core.Cli.CliOutputFormatter.FormatValue(nested);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // ── Decimal (non-primitive, non-string value type) ──────────────

    [Fact]
    public void FormatValue_ShouldUseToString_WhenValueIsDecimal()
    {
        var result = Core.Cli.CliOutputFormatter.FormatValue(99.99m);
        var expected = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", 99.99m);
        Assert.Equal(expected, result);
    }

    // ── Serialization errors rethrown ──────────────────────────────

    [Fact]
    public void FormatValue_ShouldThrow_WhenSerializationFails()
    {
        var unserializable = new UnserializableType();

        Assert.ThrowsAny<JsonException>(() => Core.Cli.CliOutputFormatter.FormatValue(unserializable));
    }

    // ── Empty array ─────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeEmptyArray_WhenValueIsEmptyArray()
    {
        var result = Core.Cli.CliOutputFormatter.FormatValue(Array.Empty<object>());
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
            Console.WriteLine(Core.Cli.CliOutputFormatter.FormatValue(items));
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

    /// <summary>
    /// A type that intentionally fails JSON serialization by containing a self-reference.
    /// </summary>
    private class UnserializableType
    {
        public UnserializableType Self => this;
        public override string ToString() => "UnserializableType:fallback";
    }
}
