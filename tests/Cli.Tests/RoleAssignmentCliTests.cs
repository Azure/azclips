// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <c>az role assignment list</c>. The command lists role assignments at a scope
/// that defaults to the active subscription and can be narrowed via <c>--resource-group</c> or
/// overridden with <c>--scope</c>. ARM returns a <c>{ "value": [...] }</c> envelope that the
/// processor unwraps into a flat array.
/// </summary>
public class RoleAssignmentCliTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string AssigneeObjectId = "11111111-2222-3333-4444-555555555555";
    private const string Scope = "/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourceGroups/test-rg";

    [Fact]
    public void CliMetadata_ExposesRoleAssignmentList()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);

        Assert.Equal(typeof(ListRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);
        Assert.Equal("SubscriptionId", metadata.Parameters["subscription-id"].Name);
        Assert.False(metadata.Parameters["subscription-id"].IsRequired);
        Assert.False(metadata.Parameters["scope"].IsRequired);
        Assert.Equal("ResourceGroupName", metadata.Parameters["resource-group"].Name);
        Assert.Equal("resource-group", metadata.ParameterAliases["g"]);
        Assert.Equal("Assignee", metadata.Parameters["assignee"].Name);
        Assert.Equal("AssigneeObjectId", metadata.Parameters["assignee-object-id"].Name);
    }

    [Fact]
    public void CliBinding_BindsListParameters()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["subscription-id"] = Value(TestSubscriptionId),
                ["resource-group"] = Value("test-rg"),
                ["assignee-object-id"] = Value(AssigneeObjectId),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
        Assert.Equal("test-rg", processor.ResourceGroupName);
        Assert.Equal(AssigneeObjectId, processor.AssigneeObjectId);
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [argumentName] = Value("test-rg") },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Fact]
    public async Task List_DefaultsToSubscriptionScope()
    {
        Uri? sentUri = await CaptureUri(p => p.SubscriptionId = TestSubscriptionId);

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_UsesResourceGroupScope()
    {
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "test-rg";
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_UsesExplicitScope()
    {
        Uri? sentUri = await CaptureUri(p => p.Scope = Scope);

        Assert.Equal(
            $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_NormalizesUserSuppliedScope()
    {
        Uri? sentUri = await CaptureUri(p => p.Scope = "subscriptions/sub/resourceGroups/rg/");

        Assert.Equal(
            "https://management.azure.com/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_AppliesAssigneeObjectIdFilter()
    {
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=principalId%20eq%20%27{AssigneeObjectId}%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_AssigneeAsObjectId_AppliesSameFilter()
    {
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Assignee = AssigneeObjectId;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=principalId%20eq%20%27{AssigneeObjectId}%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_AssigneeAsName_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Assignee = "john@contoso.com";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_AssigneeAndAssigneeObjectId_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Assignee = AssigneeObjectId;
        processor.AssigneeObjectId = AssigneeObjectId;

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_AssigneeObjectIdNotGuid_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = "not-a-guid";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_ScopeAndResourceGroup_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Scope = Scope;
        processor.ResourceGroupName = "test-rg";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_NoSubscriptionAndNoScope_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        // No subscription and no scope: cannot build a scope.
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);

        UsageErrorException ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.True(ex.HasMissingRequiredParameters);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_UnwrapsValueEnvelope_IntoFlatArray()
    {
        const string body = """
            {
              "value": [
                { "id": "/subscriptions/s/providers/Microsoft.Authorization/roleAssignments/a1", "name": "a1" },
                { "id": "/subscriptions/s/providers/Microsoft.Authorization/roleAssignments/a2", "name": "a2" }
              ]
            }
            """;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse(body));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;

        await processor.ProcessAsync();

        var written = Assert.Single(output.WrittenValues);
        var assignments = Assert.IsType<List<RoleAssignment>>(written);
        Assert.Equal(2, assignments.Count);
        Assert.Equal("a1", assignments[0].Name);
        Assert.Equal("a2", assignments[1].Name);
    }

    [Fact]
    public async Task Atomic_BuildsUriFromScopeAndFilter()
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse("""{"value":[]}""");
        });
        ListRoleAssignmentProcessor processor = CreateProcessor(new ListRoleAssignmentProcessor(), pipeline, output);
        processor.Scope = Scope;
        processor.Filter = "atScope()";

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
            sentUri!.AbsoluteUri);
    }

    private static async Task<Uri?> CaptureUri(Action<ListRoleAssignmentProcessor_Orchestrator> configure)
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse("""{"value":[]}""");
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        configure(processor);

        await processor.ProcessAsync();
        return sentUri;
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];

    private static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
        where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
