// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryServiceParameterFormattingTests
{
    private static readonly Dictionary<string, object> Parameters = new()
    {
        ["name"] = "my-rg",
        ["tags"] = new[] { "env=dev", "team=cli" },
        ["count"] = 3,
        ["switch"] = null!,
    };

    [Fact]
    public void FormatParameters_MasksEveryValue_ByDefault()
    {
        Assert.Equal(
            "-name *** -tags *** -count *** -switch ***",
            TelemetryService.FormatParameters(Parameters, revealValues: false));
    }

    [Fact]
    public void FormatParameters_WritesRealValues_WhenRulesAreBypassed()
    {
        Assert.Equal(
            "-name my-rg -tags env=dev,team=cli -count 3 -switch",
            TelemetryService.FormatParameters(Parameters, revealValues: true));
    }

    [Fact]
    public void FormatParameters_FlattensNestedCliValues_WhenRulesAreBypassed()
    {
        var cliParameters = new Dictionary<string, object>
        {
            ["name"] = new List<List<string>> { new() { "my-rg" } },
            ["tags"] = new List<List<string>> { new() { "a=1", "b=2" }, new() { "c=3" } },
        };

        Assert.Equal(
            "-name my-rg -tags a=1,b=2,c=3",
            TelemetryService.FormatParameters(cliParameters, revealValues: true));
    }
}
