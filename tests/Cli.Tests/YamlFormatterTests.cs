// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.YamlFormatter"/>. Backs both <c>Yaml</c> and <c>Yamlc</c> --
/// yamlc coloring itself is covered separately in <see cref="YamlColorizerTests"/>.
/// </summary>
public class YamlFormatterTests
{
    [Fact]
    public void Format_ShouldRenderKeys_ForObject()
    {
        var result = Core.Cli.Output.YamlFormatter.Format("""{"name":"rg1","location":"eastus"}""");

        Assert.Equal("name: rg1\nlocation: eastus", result);
    }

    [Fact]
    public void Format_ShouldRenderSequence_ForArray()
    {
        var result = Core.Cli.Output.YamlFormatter.Format("""["alpha","beta"]""");

        Assert.Equal("- alpha\n- beta", result);
    }

    [Fact]
    public void Format_ShouldNotHaveTrailingNewline()
    {
        var result = Core.Cli.Output.YamlFormatter.Format("""{"name":"rg1"}""");

        Assert.DoesNotContain('\n', result[^1..]);
    }

    [Fact]
    public void Format_ShouldRenderDateTimeOffsetAsScalarString_NotExpandedObject()
    {
        // Regression: YamlDotNet's own reflection would otherwise expand DateTimeOffset into all
        // of its properties (dateTime, utcDateTime, ticks, ...) instead of a single ISO 8601
        // string, since it has no built-in special-casing for it the way System.Text.Json does.
        var result = Core.Cli.Output.YamlFormatter.Format("""{"installedAt":"2026-09-10T03:08:00.5448431+00:00"}""");

        Assert.Equal("installedAt: 2026-09-10T03:08:00.5448431+00:00", result);
    }

    [Fact]
    public void Format_ShouldRenderNestedObjectsAndArrays()
    {
        var result = Core.Cli.Output.YamlFormatter.Format("""{"name":"rg1","tags":{"env":"prod"},"items":[1,2]}""");

        Assert.Equal("name: rg1\ntags:\n  env: prod\nitems:\n- 1\n- 2", result);
    }

    [Fact]
    public void Format_ShouldPreserveDecimalPrecision_ForHighPrecisionNumbers()
    {
        // Regression: falling back to double for every non-integer number loses precision beyond
        // ~15-17 significant digits; decimal preserves up to ~28-29.
        var result = Core.Cli.Output.YamlFormatter.Format("""{"amount":123456789012345.6789}""");

        Assert.Equal("amount: 123456789012345.6789", result);
    }
}
