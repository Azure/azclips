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
/// Offline tests for subscription-scope deployment what-if (az deployment sub what-if): command wiring, the request sent, and
/// Location polling, against a fake HTTP pipeline.
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

    private const string PollUri = "https://management.azure.com/subscriptions/x/operationresults/whatif-op?api-version=2025-04-01";

    [Fact]
    public async Task WhatIf_Accepted_PollsLocationUntilDone_AndNeverGetsPostOnlyWhatIfUri()
    {
        int polls = 0;
        var (pipeline, output, sent) = Fake(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var accepted = Status(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(PollUri);
                return accepted;
            }

            // ARM routes GET .../whatIf nowhere; only the Location URI serves the result.
            if (request.RequestUri!.AbsoluteUri != PollUri)
            {
                return Status(HttpStatusCode.NotFound, """{"message":"No HTTP resource was found that matches the request URI"}""");
            }

            // The second 202 omits Location: the poller must keep using the last one.
            return ++polls == 1
                ? Status(HttpStatusCode.Accepted)
                : Status(HttpStatusCode.OK, """{"status":"Succeeded","properties":{"changes":[{"resourceId":"/subscriptions/x/resourceGroups/rg","changeType":"Create"}]}}""");
        });

        await Configure(new NewSubscriptionDeploymentWhatIfProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
        }).ProcessAsync();

        Assert.Equal(3, sent.Count);
        Assert.All(sent.Skip(1), poll =>
        {
            Assert.Equal(HttpMethod.Get, poll.Method);
            Assert.Equal(PollUri, poll.Uri);
        });
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues));
        Assert.Equal("Create", Assert.Single(result.Properties!.Changes!).ChangeType);
    }

    [Fact]
    public async Task WhatIf_PolledResultFailed_SurfacesArmErrorMessage()
    {
        var (pipeline, output, _) = Fake(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var accepted = Status(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(PollUri);
                return accepted;
            }

            // ARM reports template errors as a 200 poll whose body carries status Failed + error.
            return Status(HttpStatusCode.OK, """{"status":"Failed","error":{"code":"InvalidTemplate","message":"The value for the template parameter 'rgName' is not provided."}}""");
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Configure(new NewSubscriptionDeploymentWhatIfProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
        }).ProcessAsync());

        Assert.Contains("InvalidTemplate", error.Message);
        Assert.Contains("rgName", error.Message);
        Assert.Empty(output.WrittenValues);
    }
}
