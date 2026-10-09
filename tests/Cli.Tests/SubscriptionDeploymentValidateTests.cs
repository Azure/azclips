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
/// Offline tests for subscription-scope deployment validate (Test-AzDeployment / az deployment sub validate): command wiring and the
/// request sent, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentValidateTests
{
    [Theory]
    [InlineData("deployment sub validate", ClientType.CLI)]
    [InlineData("Test-AzDeployment", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<TestSubscriptionDeploymentProcessor_Orchestrator>(command, client);

    [Fact]
    public async Task Validate_PostsToValidateEndpoint_AndWritesResult()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Configure(new TestSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
        }).ProcessAsync();

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep/validate?api-version=2025-04-01", request.Uri);
        Assert.Equal("eastus", JsonDocument.Parse(request.Body!).RootElement.GetProperty("location").GetString());
        Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
    }
}
