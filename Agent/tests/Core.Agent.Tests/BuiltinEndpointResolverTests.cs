// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Providers;
using Xunit;

namespace Core.Agent.Tests;

public class BuiltinEndpointResolverTests
{
    private const string EastUsHost = "azclis-copilot-apim-prod-eus.azure-api.net";
    private const string WestEuropeHost = "azclis-copilot-apim-prod-weu.azure-api.net";

    [Theory]
    [InlineData("Global")]
    [InlineData("global")]
    [InlineData("GLOBAL")]
    public void ConfirmedGlobalBoundary_UsesEastUs(string boundary)
    {
        var endpoint = BuiltinEndpointResolver.Choose(boundary);

        Assert.Equal(EastUsHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(boundary, endpoint.Boundary);
    }

    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("SomethingElse", "SomethingElse")]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public void UnconfirmedBoundary_UsesWestEurope(string? boundary, string recorded)
    {
        var endpoint = BuiltinEndpointResolver.Choose(boundary);

        Assert.Equal(WestEuropeHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(recorded, endpoint.Boundary);
    }

    [Theory]
    [InlineData("Global", "https://azclis-copilot-apim-prod-eus.azure-api.net/azclips-eval/openai/")]
    [InlineData("Global", "https://azclis-copilot-apim-ppe-eus.azure-api.net/azclips-eval/openai/")]
    [InlineData("EU", "https://azclis-copilot-apim-prod-weu.azure-api.net/azclips-eval/openai/")]
    [InlineData(null, "https://azclis-copilot-apim-ppe-weu.azure-api.net/azclips-eval/openai/")]
    public void ValidOverride_ForTheChosenRegion_IsUsed(string? boundary, string url)
    {
        var warnings = new List<string>();
        var global = boundary == "Global";
        var options = Overrides(global ? url : "", global ? "" : url);

        var endpoint = BuiltinEndpointResolver.Choose(boundary, options, warnings.Add);

        Assert.Equal(url, endpoint.Url);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("Global", "http://azclis-copilot-apim-prod-eus.azure-api.net/azclips/openai/", "agent.builtin.globalEndpoint")]
    [InlineData("Global", "https://example.com/azclips/openai/", "agent.builtin.globalEndpoint")]
    [InlineData("Global", "https://azclis-copilot-apim-prod-weu.azure-api.net/azclips/openai/", "agent.builtin.globalEndpoint")]
    [InlineData("Global", "azclis-copilot-apim-prod-eus.azure-api.net/azclips/openai/", "agent.builtin.globalEndpoint")]
    [InlineData("EU", "http://azclis-copilot-apim-prod-weu.azure-api.net/azclips/openai/", "agent.builtin.euEndpoint")]
    [InlineData("EU", "https://example.com/azclips/openai/", "agent.builtin.euEndpoint")]
    [InlineData("EU", "https://azclis-copilot-apim-prod-eus.azure-api.net/azclips/openai/", "agent.builtin.euEndpoint")]
    public void InvalidOverride_UsesTheDefaultAndWarns(string boundary, string url, string key)
    {
        var warnings = new List<string>();
        var global = boundary == "Global";
        var options = Overrides(global ? url : "", global ? "" : url);

        var endpoint = BuiltinEndpointResolver.Choose(boundary, options, warnings.Add);

        Assert.Equal(global ? BuiltinEndpointResolver.ProdEastUs : BuiltinEndpointResolver.ProdWestEurope, endpoint.Url);
        Assert.Contains(key, Assert.Single(warnings));
    }

    [Theory]
    [InlineData("Global", BuiltinEndpointResolver.ProdEastUs)]
    [InlineData("EU", BuiltinEndpointResolver.ProdWestEurope)]
    [InlineData(null, BuiltinEndpointResolver.ProdWestEurope)]
    public void UnsetOverride_UsesTheDefault(string? boundary, string expected)
    {
        var warnings = new List<string>();

        var endpoint = BuiltinEndpointResolver.Choose(boundary, new BuiltinProviderOptions(), warnings.Add);

        Assert.Equal(expected, endpoint.Url);
        Assert.Empty(warnings);
    }

    private static BuiltinProviderOptions Overrides(string global, string eu) =>
        new() { GlobalEndpoint = global, EuEndpoint = eu };
}
