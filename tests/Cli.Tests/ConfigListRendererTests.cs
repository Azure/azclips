// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Cli.Output;
using Core.Cli.Parser;
using Microsoft.Azure.Azclips.Extensions.Config.Models;

namespace Cli.Tests;

public class ConfigListRendererTests
{
    [Theory]
    [InlineData("", "default")]
    [InlineData("", "global config")]
    [InlineData("Login;profile  show", "global config")]
    public void BuiltInDefault_RendersReadableListWithActualSource(string value, string source)
    {
        var text = Render(value, source);
        Assert.StartsWith($"agent.excludedCommands [{source}]", text);
        if (value.Length == 0)
        {
            Assert.Contains(ConfigListRenderer.EmptyListMessage, text);
        }
        else
        {
            Assert.Equal($"agent.excludedCommands [{source}]{Environment.NewLine}  - Login{Environment.NewLine}  - profile  show", text);
        }
    }

    [Theory]
    [InlineData("", (int)OutputFormatSource.Flag)]
    [InlineData("Login;profile show", (int)OutputFormatSource.Flag)]
    [InlineData("Login;profile show", (int)OutputFormatSource.UserConfig)]
    public void ExplicitOrConfiguredJson_RendersArray(string value, int source)
    {
        var array = JsonNode.Parse(Render(value, "global config", (OutputFormatSource)source))!.AsArray();
        Assert.Equal(value.Length == 0 ? 0 : 2, array.Count);
        if (array.Count > 0)
        {
            Assert.Equal("Login", array[0]!.GetValue<string>());
            Assert.Equal("profile show", array[1]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("", "[]")]
    [InlineData("Login;profile show", "- Login\n- profile show")]
    public void ExplicitYaml_RendersArray(string value, string expected)
    {
        var text = Render(value, "global config", OutputFormatSource.Flag, OutputFormat.Yaml);
        Assert.Equal(expected, text.Replace("\r\n", "\n").Trim());
    }

    [Fact]
    public void Query_OperatesOnArrayEvenWithDefaultFormat()
    {
        Assert.Equal("profile show", Render("Login;profile show", "global config", query: "[1]").Trim());
        Assert.Equal("2", Render("Login;profile show", "global config", query: "length(@)").Trim());
    }

    [Fact]
    public void ConfigListAndOtherKeys_AreNotIntercepted()
    {
        var entry = new ConfigEntry("agent.excludedCommands", "Login", "global config");
        Assert.False(ConfigListRenderer.TryRender(new[] { entry }, _ => true,
            () => throw new InvalidOperationException("Unrelated output must not read format provenance."),
            OutputFormat.Json, OutputFormat.Tsv, false, null, null, out _));
        Assert.False(ConfigListRenderer.TryRender(entry, _ => false,
            () => throw new InvalidOperationException("Unrelated output must not read format provenance."),
            OutputFormat.Json, OutputFormat.Tsv, false, null, null, out _));
    }

    [Fact]
    public void Query_DoesNotReadFormatProvenance()
    {
        Assert.True(ConfigListRenderer.TryRender(
            new ConfigEntry("agent.excludedCommands", "Login", "global config"), _ => true,
            () => throw new InvalidOperationException("A query must not read format provenance."),
            OutputFormat.Json, OutputFormat.Tsv, false, "[0]", null, out var text));
        Assert.Equal("Login", text);
    }

    private static string Render(string value, string entrySource,
        OutputFormatSource source = OutputFormatSource.Default,
        OutputFormat format = OutputFormat.Json, string? query = null)
    {
        var rendered = ConfigListRenderer.TryRender(
            new ConfigEntry("agent.excludedCommands", value, entrySource),
            key => key == "agent.excludedCommands", () => source, format, OutputFormat.Tsv,
            false, query, null, out var text);
        Assert.True(rendered);
        return Assert.IsType<string>(text);
    }
}
