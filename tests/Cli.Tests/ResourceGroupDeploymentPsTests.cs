// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

/// <summary>
/// PowerShell surface tests for resource-group deployment cmdlets, focused on legacy
/// Az.Resources compatibility (parameter sets and aliases).
/// </summary>
public class ResourceGroupDeploymentPsTests
{
    private const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string DeploymentId = $"/subscriptions/{SubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Resources/deployments/dep1";

    [Fact]
    public void PowerShellMetadata_GetById_RequiresIdWithLegacyAliases()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("Get-AzResourceGroupDeployment_GetById", ClientType.PowerShell);

        Assert.Equal(typeof(GetResourceGroupDeploymentProcessor_Orchestrator), metadata.ProcessorType);
        Assert.Equal("Id", metadata.Parameters["Id"].Name);
        Assert.True(metadata.Parameters["Id"].IsRequired);
        Assert.Equal(["DeploymentId", "ResourceId"], metadata.Parameters["Id"].Aliases);
        Assert.DoesNotContain("ResourceGroupName", metadata.Parameters.Keys);
        Assert.DoesNotContain("Name", metadata.Parameters.Keys);
    }

    [Fact]
    public void PowerShellMetadata_Get_NameHasDeploymentNameAlias()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("Get-AzResourceGroupDeployment_Get", ClientType.PowerShell);

        Assert.Contains("DeploymentName", metadata.Parameters["Name"].Aliases);
    }

    [Theory]
    [InlineData("Get-AzResourceGroupDeployment_Get", ClientType.PowerShell)]
    [InlineData("deployment group show", ClientType.CLI)]
    public void GetByName_ExposesOrchestratorNotAtomicProcessor(string command, ClientType client)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata(command, client);

        Assert.Equal(typeof(GetResourceGroupDeploymentProcessor_Orchestrator), metadata.ProcessorType);
    }

    [Theory]
    [InlineData("TemplateParameterUri", "ParametersUri")]
    [InlineData("TemplateParameterFile", "ParametersFile")]
    [InlineData("RollbackToLastDeployment", "RollbackOnError")]
    [InlineData("RollBackDeploymentName", "RollbackToDeploymentName")]
    public void PowerShellMetadata_Test_MapsLegacyParameterNames(string legacyName, string parameter)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("Test-AzResourceGroupDeployment", ClientType.PowerShell);

        Assert.Equal(parameter, metadata.ParameterAliases[legacyName]);
        Assert.Contains(legacyName, metadata.Parameters[parameter].Aliases);
    }

    [Fact]
    public async Task GetById_SendsGetForParsedDeployment()
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return ResourceGroupDeploymentCliTests.JsonResponse("""{"name":"dep1"}""");
        });
        var processor = ResourceGroupDeploymentCliTests.CreateProcessor(
            new GetResourceGroupDeploymentProcessor_Orchestrator { Id = DeploymentId }, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/test-rg/providers/Microsoft.Resources/deployments/dep1?api-version=2025-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Get_Output_ExposesLegacyTopLevelPropertiesAlongsideArmShape()
    {
        const string body = $$"""
            {
              "id": "{{DeploymentId}}",
              "name": "dep1",
              "type": "Microsoft.Resources/deployments",
              "properties": {
                "provisioningState": "Succeeded",
                "correlationId": "corr-1",
                "timestamp": "2026-01-02T03:04:05Z",
                "mode": "Incremental",
                "parameters": { "p1": { "type": "String", "value": "v1" } },
                "outputs": { "o1": { "type": "String", "value": "x" } },
                "templateLink": { "uri": "https://example.com/t.json", "contentVersion": "1.0.0.0" },
                "onErrorDeployment": { "type": "LastSuccessful" }
              }
            }
            """;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => ResourceGroupDeploymentCliTests.JsonResponse(body));
        var processor = ResourceGroupDeploymentCliTests.CreateProcessor(
            new GetResourceGroupDeploymentProcessor_Orchestrator { Id = DeploymentId }, pipeline, output);

        await processor.ProcessAsync();

        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output.WrittenValues));
        Assert.Equal("dep1", deployment.Name);
        Assert.Equal("Succeeded", deployment.Properties!.ProvisioningState);
        Assert.Equal("dep1", deployment.DeploymentName);
        Assert.Equal("test-rg", deployment.ResourceGroupName);
        Assert.Equal("Succeeded", deployment.ProvisioningState);
        Assert.Equal("corr-1", deployment.CorrelationId);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), deployment.Timestamp!.Value.ToUniversalTime());
        Assert.Equal(DeploymentMode.Incremental, deployment.Mode);
        Assert.NotNull(deployment.Parameters);
        Assert.NotNull(deployment.Outputs);
        Assert.Equal("https://example.com/t.json", deployment.TemplateLink!.Uri);
        Assert.Equal("LastSuccessful", deployment.OnErrorDeployment!.Type);
    }

    [Fact]
    public void DeploymentExtended_CliJson_OmitsLegacyAliasProperties()
    {
        var deployment = new DeploymentExtended
        {
            Id = DeploymentId,
            Name = "dep1",
            Properties = new DeploymentPropertiesExtended { ProvisioningState = "Succeeded" },
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(deployment, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("properties", names);
        Assert.DoesNotContain("deploymentName", names);
        Assert.DoesNotContain("resourceGroupName", names);
        Assert.DoesNotContain("provisioningState", names);
    }

    [Theory]
    [InlineData("/subscriptions/sub/resourceGroups/test-rg")]
    [InlineData("/subscriptions/sub/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/vm1")]
    public async Task GetById_MalformedId_ThrowsWithoutRequest(string id)
    {
        var pipeline = new FakeHttpPipeline((_, _) => throw new InvalidOperationException("should not send"));
        var processor = ResourceGroupDeploymentCliTests.CreateProcessor(
            new GetResourceGroupDeploymentProcessor_Orchestrator { Id = id }, pipeline, new FakeOutputHandler());

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }
}
