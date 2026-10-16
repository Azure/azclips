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

    private static TestSubscriptionDeploymentProcessor_Orchestrator Validate(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        Configure(new TestSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Location = "eastus";
            p.TemplateUri = "https://example.com/t.json";
        });

    [Fact]
    public async Task Validate_ArmValidationError400_ThrowsReadableMessage_AndWritesNothing()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.BadRequest,
            """{"error":{"code":"InvalidTemplate","message":"Template is invalid."}}"""));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Single(sent);
        Assert.StartsWith("Deployment validation failed.", error.Message);
        Assert.Contains("InvalidTemplate: Template is invalid.", error.Message);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Validate_Accepted_PollsLocation_AndNeverGetsPostOnlyValidateUri()
    {
        const string pollUri = "https://management.azure.com/subscriptions/x/operationresults/validate-op?api-version=2025-04-01";
        var (pipeline, output, sent) = Fake(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var accepted = Status(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(pollUri);
                return accepted;
            }

            return request.RequestUri!.AbsoluteUri == pollUri
                ? Status(HttpStatusCode.OK, SucceededSubscriptionDeployment)
                : Status(HttpStatusCode.NotFound, """{"message":"No HTTP resource was found that matches the request URI"}""");
        });

        await Validate(pipeline, output).ProcessAsync();

        Assert.Equal(2, sent.Count);
        Assert.Equal(HttpMethod.Get, sent[1].Method);
        Assert.Equal(pollUri, sent[1].Uri);
        Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
    }

    [Fact]
    public async Task Validate_AcceptedThenPollAccepted_ReturnsTerminalPollResponse_WithoutFinalGet()
    {
        const string pollUri = "https://management.azure.com/subscriptions/x/operationresults/validate-op?api-version=2025-04-01";
        int polls = 0;
        var (pipeline, output, sent) = Fake(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var accepted = Status(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(pollUri);
                return accepted;
            }

            // The operation result is one-shot: once it has been returned, ARM answers 404.
            return ++polls switch
            {
                1 => Status(HttpStatusCode.Accepted),
                2 => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment),
                _ => Status(HttpStatusCode.NotFound, """{"message":"Operation result no longer available"}"""),
            };
        });

        await Validate(pipeline, output).ProcessAsync();

        Assert.Equal(3, sent.Count);
        Assert.All(sent.Skip(1), s => Assert.Equal(pollUri, s.Uri));
        Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
    }
}
