// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for resource-group deployment create (az deployment group create / New-AzResourceGroupDeployment):
/// preflight validation runs before the PUT, matching the legacy clients.
/// </summary>
public class ResourceGroupDeploymentCreateTests
{
    private static NewResourceGroupDeploymentProcessor_Orchestrator Create(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        Configure(new NewResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
            p.TemplateUri = "https://example.com/t.json";
        });

    [Fact]
    public async Task Create_ValidationFails_ThrowsAndNeverSendsPut()
    {
        var (pipeline, output, sent) = Fake(_ =>
            Status(HttpStatusCode.BadRequest, """{"error":{"code":"InvalidTemplate","message":"The template parameter 'storageAccountName' is not provided."}}"""));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(pipeline, output).ProcessAsync());

        Assert.Contains("InvalidTemplate", error.Message);
        Assert.Contains("storageAccountName", error.Message);
        var only = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, only.Method);
        Assert.Equal(DeploymentUri + "/validate?api-version=2025-04-01", only.Uri);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Create_ValidationSucceeds_ThenSendsPutWithSameBody()
    {
        var (pipeline, output, sent) = Fake(request => request.Method == HttpMethod.Post
            ? Status(HttpStatusCode.OK, """{"properties":{"provisioningState":"Succeeded"}}""")
            : Status(HttpStatusCode.OK, """{"name":"dep","properties":{"provisioningState":"Succeeded"}}"""));

        await Create(pipeline, output).ProcessAsync();

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal(DeploymentUri + "/validate?api-version=2025-04-01", sent[0].Uri);
        Assert.Equal(HttpMethod.Put, sent[1].Method);
        Assert.Equal(DeploymentUri + "?api-version=2025-04-01", sent[1].Uri);
        Assert.Equal(sent[0].Body, sent[1].Body);
        // Remaining requests are the create's own wait-for-terminal-state GETs.
        Assert.All(sent.Skip(2), s => Assert.Equal(HttpMethod.Get, s.Method));
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task Create_JsoncTemplateAndParametersFiles_AreAccepted()
    {
        string dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string template = Path.Combine(dir, "t.json");
            string parameters = Path.Combine(dir, "p.json");
            File.WriteAllText(template, """
                {
                  // line comment
                  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                  "contentVersion": "1.0.0.0", /* block comment */
                  "resources": [],
                }
                """);
            File.WriteAllText(parameters, """
                {
                  "contentVersion": "1.0.0.0",
                  "parameters": {
                    // comment
                    "name": { "value": "sa1", },
                  },
                }
                """);

            var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, """{"name":"dep","properties":{"provisioningState":"Succeeded"}}"""));
            var create = Configure(new NewResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = "test-rg";
                p.Name = "dep";
                p.TemplateFile = template;
                p.ParametersFile = parameters;
            });

            await create.ProcessAsync();

            Assert.Contains("\"contentVersion\":\"1.0.0.0\"", sent[0].Body);
            Assert.Contains("\"value\":\"sa1\"", sent[0].Body);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
