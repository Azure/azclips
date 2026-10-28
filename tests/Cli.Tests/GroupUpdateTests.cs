// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

public class GroupUpdateTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public void CliMetadata_ExposesLegacyVisibleSurfaceOnly()
    {
        var index = new ExtensionIndex();
        CommandMetadata metadata = index.GetCommandMetadata("group update", ClientType.CLI);

        Assert.Equal(typeof(UpdateResourceGroupProcessor_Orchestrator), metadata.ProcessorType);
        Assert.Equal(["n", "resource-group", "g"], metadata.Parameters["name"].Aliases);
        Assert.True(metadata.Parameters["name"].IsRequired);
        Assert.Equal(typeof(string[]), metadata.Parameters["tags"].ParameterType);
        Assert.Equal(typeof(string[]), metadata.Parameters["set"].ParameterType);
        Assert.Equal(typeof(bool), metadata.Parameters["force-string"].ParameterType);
        Assert.DoesNotContain("add", metadata.Parameters.Keys);
        Assert.DoesNotContain("remove", metadata.Parameters.Keys);
    }

    [Fact]
    public void PowerShellMetadata_ExposesLegacyTagReplacementSurface()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata(
            "Set-AzResourceGroup",
            ClientType.PowerShell);

        Assert.Equal(typeof(SetResourceGroupProcessor_Orchestrator), metadata.ProcessorType);
        Assert.Equal("ResourceGroupName", metadata.Parameters["Name"].Name);
        Assert.Equal(["ResourceGroupName"], metadata.Parameters["Name"].Aliases);
        Assert.Equal(["ResourceGroupId", "ResourceId"], metadata.Parameters["Id"].Aliases);
        Assert.Equal("Tags", metadata.Parameters["Tag"].Name);
        Assert.Equal(typeof(Dictionary<string, string>), metadata.Parameters["Tag"].ParameterType);
        Assert.True(metadata.Parameters["Tag"].IsRequired);
        Assert.DoesNotContain("Set", metadata.Parameters.Keys);
        Assert.DoesNotContain("ForceString", metadata.Parameters.Keys);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("n")]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_BindsNameAliasesAndUpdateArguments(string nameArgument)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("group update", ClientType.CLI);
        var processor = (UpdateResourceGroupProcessor_Orchestrator)metadata.GetInstance();
        var binder = CreateBinder();

        binder.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [nameArgument] = Value("test-rg"),
                ["tags"] = Value("Owner=team-a"),
                ["set"] = Value("tags.CostCenter=42"),
                ["force-string"] = new List<List<string>>(),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
        Assert.NotNull(processor.TagsInput);
        Assert.Equal(["Owner=team-a"], processor.TagsInput);
        Assert.NotNull(processor.Set);
        Assert.Equal(["tags.CostCenter=42"], processor.Set);
        Assert.True(processor.ForceString);
    }

    [Fact]
    public async Task ProcessAsync_WithTags_SendsPatchWithoutGet()
    {
        HttpRequestMessage? sentRequest = null;
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequest = request;
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"name":"test-rg","location":"eastus","tags":{"Owner":"team-a","empty":""}}""");
        });
        var output = new FakeOutputHandler();
        UpdateResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "group with spaces";
        processor.TagsInput = ["Owner=team-a", "empty"];

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Patch, sentRequest!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/group%20with%20spaces?api-version=2025-04-01",
            sentRequest.RequestUri!.AbsoluteUri);
        using JsonDocument body = JsonDocument.Parse(requestBody!);
        Assert.Equal("team-a", body.RootElement.GetProperty("tags").GetProperty("Owner").GetString());
        Assert.Equal(string.Empty, body.RootElement.GetProperty("tags").GetProperty("empty").GetString());
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyTags_ClearsTags()
    {
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"name":"test-rg","location":"eastus","tags":{}}""");
        });
        UpdateResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.ResourceGroupName = "test-rg";
        processor.TagsInput = [""];

        await processor.ProcessAsync();

        using JsonDocument body = JsonDocument.Parse(requestBody!);
        Assert.Empty(body.RootElement.GetProperty("tags").EnumerateObject());
    }

    [Fact]
    public async Task PowerShellProcessAsync_ResolvesIdAndReplacesTagsWithOnePatch()
    {
        HttpRequestMessage? sentRequest = null;
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequest = request;
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"name":"test-rg","location":"eastus","tags":{"Owner":"team-b"}}""");
        });
        SetResourceGroupProcessor_Orchestrator processor = CreatePowerShellProcessor(
            pipeline,
            new FakeOutputHandler());
        processor.Id = "/subscriptions/id-subscription/resourceGroups/group with spaces";
        processor.Tags = new Dictionary<string, string> { ["Owner"] = "team-b" };

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Patch, sentRequest!.Method);
        Assert.Equal(
            "https://management.azure.com/subscriptions/id-subscription/resourcegroups/group%20with%20spaces?api-version=2025-04-01",
            sentRequest.RequestUri!.AbsoluteUri);
        using JsonDocument body = JsonDocument.Parse(requestBody!);
        Assert.Equal("team-b", body.RootElement.GetProperty("tags").GetProperty("Owner").GetString());
        Assert.Equal("id-subscription", processor.SubscriptionId);
        Assert.Equal("group with spaces", processor.ResourceGroupName);
    }

    [Fact]
    public async Task PowerShellProcessAsync_RequiresOneAddressAndTags()
    {
        SetResourceGroupProcessor_Orchestrator processor = CreatePowerShellProcessor(
            new FakeHttpPipeline((_, _) => throw new InvalidOperationException("HTTP should not be called.")),
            new FakeOutputHandler());

        InvalidInputException missingAddress = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Contains("exactly one", missingAddress.Message);

        processor.ResourceGroupName = "test-rg";
        InvalidInputException missingTags = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Contains("Tag parameter is required", missingTags.Message);
    }

    [Fact]
    public async Task ProcessAsync_WithSet_GetsCurrentGroupThenPatchesSupportedFields()
    {
        var requests = new List<(HttpMethod Method, string? Body)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            string? body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            requests.Add((request.Method, body));
            return requests.Count == 1
                ? Json("""{"id":"/subscriptions/sub/resourceGroups/test-rg","name":"test-rg","type":"Microsoft.Resources/resourceGroups","location":"eastus","managedBy":null,"tags":{"Owner":"old"},"properties":{"provisioningState":"Succeeded"}}""")
                : Json("""{"name":"test-rg","location":"eastus","tags":{"Owner":"new","CostCenter":"42"}}""");
        });
        UpdateResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.ResourceGroupName = "test-rg";
        processor.Set = ["Tags.Owner=new", "tags.CostCenter=42"];
        processor.ForceString = true;

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Get, HttpMethod.Patch], requests.Select(request => request.Method));
        using JsonDocument body = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal("new", body.RootElement.GetProperty("tags").GetProperty("Owner").GetString());
        Assert.Equal("42", body.RootElement.GetProperty("tags").GetProperty("CostCenter").GetString());
        Assert.False(body.RootElement.TryGetProperty("id", out _));
        Assert.False(body.RootElement.TryGetProperty("location", out _));
        Assert.False(body.RootElement.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task ProcessAsync_WithSetNull_SendsJsonNull()
    {
        var requests = new List<(HttpMethod Method, string? Body)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            string? body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            requests.Add((request.Method, body));
            return requests.Count == 1
                ? Json("""{"name":"test-rg","location":"eastus","managedBy":"/subscriptions/sub/resourceGroups/manager","tags":{}}""")
                : Json("""{"name":"test-rg","location":"eastus","managedBy":null,"tags":{}}""");
        });
        UpdateResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.ResourceGroupName = "test-rg";
        processor.Set = ["ManagedBy=null"];

        await processor.ProcessAsync();

        using JsonDocument body = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("managedBy").ValueKind);
    }

    [Fact]
    public async Task ProcessAsync_RejectsSetPathsOutsidePatchSchema()
    {
        UpdateResourceGroupProcessor_Orchestrator processor = CreateProcessor(
            new FakeHttpPipeline((_, _) => throw new InvalidOperationException("HTTP should not be called.")),
            new FakeOutputHandler());
        processor.ResourceGroupName = "test-rg";
        processor.Set = ["location=westus"];

        InvalidInputException exception = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());

        Assert.Contains("Supported roots", exception.Message);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];

    private static UpdateResourceGroupProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new UpdateResourceGroupProcessor_Orchestrator { SubscriptionId = TestSubscriptionId };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    private static SetResourceGroupProcessor_Orchestrator CreatePowerShellProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new SetResourceGroupProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }
}
