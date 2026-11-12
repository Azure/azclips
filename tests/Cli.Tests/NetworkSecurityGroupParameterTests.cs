// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;
using Xunit;

namespace Cli.Tests;

public class NetworkSecurityGroupParameterTests
{
    private const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string NsgName = "my-nsg";

    private static void Init(IProcessor processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        pipeline.PollingInterval = 0;
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = SubscriptionId };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<CreateOrUpdateNetworkSecurityGroupsProcessor_Customization> configure)
    {
        string? body = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            body ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json($$"""{ "name": "{{NsgName}}", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new CreateOrUpdateNetworkSecurityGroupsProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
            Location = "eastus",
        };
        configure(processor);
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(body);
        return JsonDocument.Parse(body!).RootElement;
    }

    [Fact]
    public async Task Create_WithoutTags_OmitsOptionalResponseProperties()
    {
        var body = await CaptureCreateBodyAsync(_ => { });

        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task Create_TagsPreserveKeysAndValues()
    {
        var body = await CaptureCreateBodyAsync(processor =>
            processor.TagSpecs = ["Owner=team-A", "connection=server=primary"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("team-A", tags.GetProperty("Owner").GetString());
        Assert.Equal("server=primary", tags.GetProperty("connection").GetString());
    }

    /// <summary>Azure CLI tags are KEY[=VALUE]: a bare key is a tag with an empty value.</summary>
    [Fact]
    public async Task Create_BareTagKey_GetsAnEmptyValue()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["owner", "env=dev"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("", tags.GetProperty("owner").GetString());
        Assert.Equal("dev", tags.GetProperty("env").GetString());
    }

    [Fact]
    public async Task Create_RepeatedTagKey_LastValueWins()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["env=dev", "env=prod"]);

        Assert.Equal("prod", body.GetProperty("tags").GetProperty("env").GetString());
    }

    [Fact]
    public async Task Create_TagWithoutKey_IsRejected()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(() =>
            CaptureCreateBodyAsync(processor => processor.TagSpecs = ["=dev"]));

        Assert.Contains("--tags", error.Message);
        Assert.Contains("KEY[=VALUE]", error.Message);
    }

    [Fact]
    public async Task Create_WithoutLocation_UsesResourceGroupLocation()
    {
        var requests = new List<(HttpMethod Method, Uri Uri, string? Body)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            requests.Add((request.Method, request.RequestUri!, body));
            return request.Method == HttpMethod.Get
                ? Json("""{ "location": "westus2" }""")
                : Json($$"""{ "name": "{{NsgName}}", "location": "westus2", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new CreateOrUpdateNetworkSecurityGroupsProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}?api-version=2025-04-01",
            requests[0].Uri.AbsoluteUri);
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal("westus2", JsonDocument.Parse(requests[1].Body!).RootElement.GetProperty("location").GetString());
        Assert.Equal(HttpMethod.Get, requests[2].Method);
    }

    [Theory]
    [InlineData("", "/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/providers/Microsoft.Network/networkSecurityGroups")]
    [InlineData(ResourceGroupName, "/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourceGroups/my-rg/providers/Microsoft.Network/networkSecurityGroups")]
    public async Task List_SelectsRequestedScope(string resourceGroupName, string expectedPath)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json("""{ "value": [] }""");
        });
        var processor = new ListAllNetworkSecurityGroupsProcessor_Orchestrator
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = resourceGroupName,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(expectedPath, requested?.AbsolutePath);
    }

    [Fact]
    public async Task Create_PollsAzureAsyncOperation()
    {
        var pollingUri = new Uri("https://management.azure.com/operations/nsg-create");
        var requests = new List<(HttpMethod Method, Uri Uri)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            if (requests.Count == 1)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", pollingUri.AbsoluteUri);
                return accepted;
            }

            return Json($$"""{ "name": "{{NsgName}}", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new CreateOrUpdateNetworkSecurityGroupsProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
            Location = "eastus",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Put, requests[0].Method);
        Assert.Equal((HttpMethod.Get, pollingUri), requests[1]);
        Assert.Equal(HttpMethod.Get, requests[2].Method);
        Assert.Contains("/networkSecurityGroups/my-nsg", requests[2].Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Network RP deletes answer 202 + Azure-AsyncOperation + Location: poll the status, then
    /// finish on the Location result, an empty 204, so the delete writes no output.
    /// </summary>
    [Fact]
    public async Task Delete_FinishesOnTheLocationResult_AndWritesNothing()
    {
        const string statusUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Network/locations/eastus/operations/op1?api-version=2025-07-01";
        const string resultUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Network/locations/eastus/operationResults/op1?api-version=2025-07-01";
        var gets = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Add("Azure-AsyncOperation", statusUri);
                accepted.Headers.Add("Location", resultUri);
                return accepted;
            }
            gets.Add(request.RequestUri!.ToString());
            return gets[^1] == statusUri
                ? Json("""{ "status": "Succeeded" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new DeleteNetworkSecurityGroupsProcessor
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal([statusUri, resultUri], gets);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_PollsLocationHeader()
    {
        var pollingUri = new Uri("https://management.azure.com/operations/nsg-delete");
        var requests = new List<(HttpMethod Method, Uri Uri)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            if (requests.Count == 1)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.TryAddWithoutValidation("Location", pollingUri.AbsoluteUri);
                return accepted;
            }

            return Json("""{ "status": "Succeeded" }""");
        });
        var processor = new DeleteNetworkSecurityGroupsProcessor
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Delete, requests[0].Method);
        Assert.All(requests.Skip(1), request => Assert.Equal((HttpMethod.Get, pollingUri), request));
    }

    [Theory]
    [InlineData("my group", true)]
    [InlineData(null, false)]
    public async Task Show_AppendsEncodedExpandOnlyWhenRequested(string? expand, bool expected)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json($$"""{ "name": "{{NsgName}}", "location": "eastus", "properties": {} }""");
        });
        var processor = new GetNetworkSecurityGroupsProcessor
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = ResourceGroupName,
            NetworkSecurityGroupName = NsgName,
            Expand = expand,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        Assert.Equal(expected, requested!.Query.Contains("$expand=", StringComparison.Ordinal));
        if (expected)
        {
            Assert.Contains("my%20group", requested.AbsoluteUri);
        }
    }

    [Theory]
    [InlineData("network nsg create", "g", "resource-group")]
    [InlineData("network nsg create", "l", "location")]
    [InlineData("network nsg create", "n", "name")]
    [InlineData("network nsg show", "g", "resource-group")]
    [InlineData("network nsg show", "n", "name")]
    [InlineData("network nsg list", "g", "resource-group")]
    [InlineData("network nsg delete", "g", "resource-group")]
    [InlineData("network nsg delete", "n", "name")]
    public void CliCommands_MapLegacyShortFlags(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.Equal(canonical, metadata.ParameterAliases[alias]);
        Assert.Contains(alias, metadata.Parameters[canonical].Aliases);
    }

    [Theory]
    [InlineData("New-AzNetworkSecurityGroup", typeof(CreateOrUpdateNetworkSecurityGroupsProcessor_Customization))]
    [InlineData("Get-AzNetworkSecurityGroup_Get", typeof(GetNetworkSecurityGroupsProcessor))]
    [InlineData("Get-AzNetworkSecurityGroup_List", typeof(ListAllNetworkSecurityGroupsProcessor_Orchestrator))]
    [InlineData("Remove-AzNetworkSecurityGroup", typeof(DeleteNetworkSecurityGroupsProcessor))]
    public void PowerShellCommands_RouteToProcessors(string command, Type processorType)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.PowerShell);

        Assert.Equal(processorType, metadata.ProcessorType);
    }
}
