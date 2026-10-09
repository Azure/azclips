// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment what-if (az deployment sub what-if): command wiring and the request sent, against a
/// fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentWhatIfTests
{
    [Theory]
    [InlineData("deployment sub what-if", ClientType.CLI)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<NewSubscriptionDeploymentWhatIfProcessor_Orchestrator>(command, client);

    [Fact]
    public async Task WhatIf_PostsToWhatIfEndpoint_WithResultFormat()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """{"status":"Succeeded","changes":[]}"""));

        await Configure(new NewSubscriptionDeploymentWhatIfProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
            p.ResultFormat = "ResourceIdOnly";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep/whatIf?api-version=2025-04-01", sent[0].Uri);
        var body = JsonDocument.Parse(sent[0].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("ResourceIdOnly", body.GetProperty("properties").GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
    }
}
