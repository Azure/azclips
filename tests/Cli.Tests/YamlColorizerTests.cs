// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using static Core.Cli.Output.JsonColorizer;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.YamlColorizer"/> ensuring keys, string values, numbers,
/// and literals are highlighted correctly and structural characters/whitespace are preserved.
/// Shares its color palette with <see cref="Core.Cli.Output.JsonColorizer"/>.
/// </summary>
public class YamlColorizerTests
{
    [Fact]
    public void Colorize_ShouldWrapKeyAndStringValue_ForMapping()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("name: rg1\nlocation: eastus");

        Assert.Equal(
            $"{KeyColor}name{Reset}: {StringColor}rg1{Reset}\n{KeyColor}location{Reset}: {StringColor}eastus{Reset}",
            result);
    }

    [Fact]
    public void Colorize_ShouldWrapNumber()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("count: 42");

        Assert.Contains($"{NumberColor}42{Reset}", result);
    }

    [Fact]
    public void Colorize_ShouldWrapNegativeAndDecimalNumbers()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("a: -1\nb: 3.14");

        Assert.Contains($"{NumberColor}-1{Reset}", result);
        Assert.Contains($"{NumberColor}3.14{Reset}", result);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public void Colorize_ShouldWrapLiterals(string literal)
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize($"flag: {literal}");

        Assert.Contains($"{LiteralColor}{literal}{Reset}", result);
    }

    [Fact]
    public void Colorize_ShouldWrapSequenceItems_ByInferredType()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("- alpha\n- 42\n- true");

        Assert.Contains($"{StringColor}alpha{Reset}", result);
        Assert.Contains($"{NumberColor}42{Reset}", result);
        Assert.Contains($"{LiteralColor}true{Reset}", result);
        // Sequence items have no preceding Key marker, so none of them get KeyColor.
        Assert.DoesNotContain(KeyColor, result);
    }

    [Fact]
    public void Colorize_ShouldNotTreatUnicodeDigits_AsNumber()
    {
        // Regression: \d in .NET regex matches every Unicode decimal digit (e.g. Arabic-Indic
        // digits), but YAML/JSON numeric syntax only permits ASCII 0-9 -- must stay StringColor.
        var result = Core.Cli.Output.YamlColorizer.Colorize("value: \u0661\u0662\u0663");

        Assert.Contains($"{StringColor}\u0661\u0662\u0663{Reset}", result);
        Assert.DoesNotContain(NumberColor, result);
    }

    [Fact]
    public void Colorize_ShouldTreatQuotedNumericLookingString_AsString_NotNumber()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("""version: "0.1.0" """.TrimEnd());

        Assert.Contains($"{StringColor}\"0.1.0\"{Reset}", result);
        Assert.DoesNotContain(NumberColor, result);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1,000")]
    public void Colorize_ShouldNotTreatNonNumericLookingPlainScalar_AsNumber(string value)
    {
        // Regression: double.TryParse(InvariantCulture) accepted these .NET-only forms even
        // though they're valid plain YAML strings -- only a strict numeric grammar should count.
        var result = Core.Cli.Output.YamlColorizer.Colorize($"value: {value}");

        Assert.Contains($"{StringColor}{value}{Reset}", result);
        Assert.DoesNotContain(NumberColor, result);
    }

    [Fact]
    public void Colorize_ShouldLeaveOmittedNullValue_Uncolored()
    {
        // Regression: YamlFormatter renders a JSON null as a blank mapping value (`tag: ` with
        // nothing after it) -- the scanner emits no Scalar token there, so there's no span to
        // color. Confirms this stays a no-op rather than injecting visible "null" text.
        var yaml = Core.Cli.Output.YamlFormatter.Format("""{"tag":null}""");

        var result = Core.Cli.Output.YamlColorizer.Colorize(yaml);

        Assert.Equal($"{KeyColor}tag{Reset}: ", result);
    }

    [Fact]
    public void Colorize_ShouldNotColorizeStructuralCharacters()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("name: rg1");

        // The mapping colon/space separator is copied through verbatim, never itself colored.
        Assert.Contains($"{Reset}: {StringColor}", result);
    }

    [Fact]
    public void Colorize_ShouldPreserveNestedIndentation()
    {
        var result = Core.Cli.Output.YamlColorizer.Colorize("tags:\n  env: prod");

        var stripped = StripAnsi(result);
        Assert.Equal("tags:\n  env: prod", stripped);
    }

    [Fact]
    public void Colorize_StrippedOfAnsiCodes_ShouldRoundTripToIdenticalYaml()
    {
        var yaml = Core.Cli.Output.YamlFormatter.Format(
            """{"name":"rg1","count":3,"active":true,"tag":null,"items":[1,2]}""");

        var colorized = Core.Cli.Output.YamlColorizer.Colorize(yaml);
        var stripped = StripAnsi(colorized);

        Assert.Equal(yaml, stripped);
    }

    private static string StripAnsi(string text) => Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);
}
