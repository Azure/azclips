// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment list (Get-AzDeployment / az deployment sub list): command wiring and the request
/// sent, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentListTests
{
    [Theory]
    [InlineData("deployment sub list", ClientType.CLI)]
    [InlineData("Get-AzDeployment_List", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<ListSubscriptionDeploymentProcessor>(command, client);

    [Fact]
    public async Task List_GetsSubscriptionDeployments()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """{"value":[]}"""));

        await Configure(new ListSubscriptionDeploymentProcessor(), pipeline, output, p => p.SubscriptionId = SubscriptionId).ProcessAsync();

        Assert.Equal(HttpMethod.Get, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "?api-version=2025-04-01", sent[0].Uri);
    }
}
