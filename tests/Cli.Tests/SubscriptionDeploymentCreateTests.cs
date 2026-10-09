// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment create (New-AzDeployment / az deployment sub create): command wiring, the request sent
/// and orchestrator rules, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentCreateTests
{
    [Theory]
    [InlineData("deployment sub create", ClientType.CLI)]
    [InlineData("New-AzDeployment", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<NewSubscriptionDeploymentProcessor_Orchestrator>(command, client);

    [Fact]
    public async Task Create_PutsLocationTemplateLinkAndParameters_InIncrementalMode()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Configure(new NewSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
            p.Parameter = ["rgName=rg1"];
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Put, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep?api-version=2025-04-01", sent[0].Uri);
        var body = JsonDocument.Parse(sent[0].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        var properties = body.GetProperty("properties");
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        Assert.Equal("https://example.com/t.json", properties.GetProperty("templateLink").GetProperty("uri").GetString());
        Assert.Equal("rg1", properties.GetProperty("parameters").GetProperty("rgName").GetProperty("value").GetString());
        Assert.IsType<DeploymentExtended>(Assert.Single(output.WrittenValues));
    }

    [Fact]
    public async Task Create_WithoutName_UsesGeneratedGuid()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Configure(new NewSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
        }).ProcessAsync();

        Assert.True(Guid.TryParse(new Uri(sent[0].Uri).Segments[^1], out _), sent[0].Uri);
    }

    [Theory]
    [InlineData(null, "https://example.com/t.json")]
    [InlineData("eastus", null)]
    public async Task Create_MissingLocationOrTemplate_ThrowsBeforeSending(string? location, string? templateUri)
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Assert.ThrowsAsync<ArgumentException>(() => Configure(new NewSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = location ?? string.Empty;
            p.TemplateUri = templateUri;
        }).ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }
}
