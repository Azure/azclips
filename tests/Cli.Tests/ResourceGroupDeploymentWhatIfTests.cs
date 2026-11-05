// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for resource-group deployment what-if (az deployment group what-if / New-AzResourceGroupDeploymentWhatIf):
/// Location polling and error surfacing, against a fake HTTP pipeline.
/// </summary>
public class ResourceGroupDeploymentWhatIfTests
{
    private const string PollUri = "https://management.azure.com/subscriptions/x/operationresults/whatif-op?api-version=2025-04-01";

    private static NewResourceGroupDeploymentWhatIfProcessor_Orchestrator WhatIf(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        Configure(new NewResourceGroupDeploymentWhatIfProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
            p.TemplateUri = "https://example.com/t.json";
        });

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
                : Status(HttpStatusCode.OK, """{"status":"Succeeded","properties":{"changes":[{"resourceId":"/subscriptions/x/resourceGroups/test-rg/providers/Microsoft.Storage/storageAccounts/sa","changeType":"Create"}]}}""");
        });

        await WhatIf(pipeline, output).ProcessAsync();

        Assert.Equal(3, sent.Count);
        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal(DeploymentUri + "/whatIf?api-version=2025-04-01", sent[0].Uri);
        Assert.All(sent.Skip(1), poll =>
        {
            Assert.Equal(HttpMethod.Get, poll.Method);
            Assert.Equal(PollUri, poll.Uri);
        });
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues));
        Assert.Equal("Create", Assert.Single(result.Changes!).ChangeType);
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
            return Status(HttpStatusCode.OK, """{"status":"Failed","error":{"code":"InvalidTemplate","message":"The value for the template parameter 'storageAccountName' is not provided."}}""");
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WhatIf(pipeline, output).ProcessAsync());

        Assert.Contains("InvalidTemplate", error.Message);
        Assert.Contains("storageAccountName", error.Message);
        Assert.Empty(output.WrittenValues);
    }
}
