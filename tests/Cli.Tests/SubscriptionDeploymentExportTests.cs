// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment export (az deployment sub export): command wiring and the request sent, against a
/// fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentExportTests
{
    [Theory]
    [InlineData("deployment sub export", ClientType.CLI)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<ExportSubscriptionDeploymentTemplateProcessor>(command, client);

    [Fact]
    public async Task Export_PostsToExportTemplateEndpoint()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """{"template":{}}"""));

        await Configure(new ExportSubscriptionDeploymentTemplateProcessor(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep/exportTemplate?api-version=2025-04-01", sent[0].Uri);
    }
}
