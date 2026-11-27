// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using Tests.Common.Stubs;

namespace Extensions.Resources.Tests;

public class GroupUpdateTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

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
        Assert.Equal("Specify exactly one of resource group name or resource group id.", missingAddress.Message);

        processor.ResourceGroupName = "test-rg";
        InvalidInputException missingTags = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Equal("The Tag parameter is required.", missingTags.Message);
    }

    [Fact]
    public async Task PowerShellProcessAsync_NameAndId_ThrowsInvalidInput()
    {
        var processor = ResourcesProcessorTestHelpers.Initialize(new SetResourceGroupProcessor_Orchestrator
        {
            ResourceGroupName = "test-rg",
            Id = "/subscriptions/sub-1/resourceGroups/test-rg",
            Tags = new Dictionary<string, string> { ["Owner"] = "team-b" },
        }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Specify exactly one of resource group name or resource group id.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // A blank Id counts as "no address", so ResourceGroupIdResolver's own empty-id message is never reached here.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PowerShellProcessAsync_BlankId_ThrowsInvalidInput(string id)
    {
        var processor = ResourcesProcessorTestHelpers.Initialize(new SetResourceGroupProcessor_Orchestrator
        {
            Id = id,
            Tags = new Dictionary<string, string> { ["Owner"] = "team-b" },
        }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Specify exactly one of resource group name or resource group id.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task PowerShellProcessAsync_MalformedId_ThrowsInvalidInput()
    {
        var processor = ResourcesProcessorTestHelpers.Initialize(new SetResourceGroupProcessor_Orchestrator
        {
            Id = "not-a-resource-id",
            Tags = new Dictionary<string, string> { ["Owner"] = "team-b" },
        }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "Invalid resource group id 'not-a-resource-id'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}.",
            Assert.IsType<InvalidInputException>(exception).Message);
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

        Assert.Equal(
            "The --set path 'location' cannot be updated by the resource group PATCH API. Supported roots are name, managedBy, and tags.",
            exception.Message);
    }

    [Theory]
    [InlineData("=value")]
    [InlineData("   =value")]
    public async Task ProcessAsync_EmptyTagKey_ThrowsInvalidInput(string entry)
    {
        var processor = ResourcesProcessorTestHelpers.Initialize(new UpdateResourceGroupProcessor_Orchestrator { ResourceGroupName = "test-rg", TagsInput = [entry] }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal($"Invalid tag '{entry}'. Tag keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("location")]
    [InlineData("=westus")]
    public async Task ProcessAsync_MalformedSetExpression_ThrowsInvalidInput(string expression)
    {
        var processor = ResourcesProcessorTestHelpers.Initialize(new UpdateResourceGroupProcessor_Orchestrator { ResourceGroupName = "test-rg", Set = [expression] }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal($"Invalid --set expression '{expression}'. Expected PATH=VALUE.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static UpdateResourceGroupProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new UpdateResourceGroupProcessor_Orchestrator();
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
