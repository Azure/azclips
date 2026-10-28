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
/// Offline tests for <c>az role assignment delete</c> and <c>Remove-AzRoleAssignment</c>. Delete
/// issues a DELETE to <c>{scope}/providers/Microsoft.Authorization/roleAssignments/{name}</c>. Unlike
/// classic az/PowerShell, this surface only supports an exact single-resource delete by scope + name
/// (no bulk/filter-based delete by principal, role, or scope alone) — see
/// <see cref="DeleteRoleAssignmentProcessor_Orchestrator"/> for the rationale.
/// </summary>
public class RoleAssignmentDeleteTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string AssignmentName = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Fact]
    public void CliMetadata_ExposesRoleAssignmentDelete()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment delete", ClientType.CLI);

        Assert.Equal(typeof(DeleteRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);
        Assert.False(metadata.Parameters["scope"].IsRequired);
        Assert.True(metadata.Parameters["name"].IsRequired);
        Assert.Equal("Name", metadata.Parameters["name"].Name);
        Assert.Equal("ResourceGroupName", metadata.Parameters["resource-group"].Name);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
        Assert.Equal("resource-group", metadata.ParameterAliases["g"]);
    }

    [Fact]
    public void PowerShellMetadata_ExposesRemoveAzRoleAssignment()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("Remove-AzRoleAssignment", ClientType.PowerShell);

        Assert.Equal(typeof(DeleteRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);
        Assert.True(metadata.Parameters["Name"].IsRequired);
        Assert.False(metadata.Parameters["Scope"].IsRequired);
        Assert.False(metadata.Parameters["ResourceGroupName"].IsRequired);
    }

    [Fact]
    public async Task Delete_SendsDeleteToScopedRoleAssignment()
    {
        (Uri uri, HttpMethod method) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = $"/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg";
            p.Name = AssignmentName;
        });

        Assert.Equal(HttpMethod.Delete, method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Delete_DefaultsScopeToSubscription()
    {
        (Uri uri, _) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Delete_ResourceGroupScope_BuildsResourceGroupScope()
    {
        (Uri uri, _) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Delete_ManagementGroupScope_SendsDeleteAtScope()
    {
        (Uri uri, _) = await Capture(p =>
        {
            p.Scope = "/providers/Microsoft.Management/managementGroups/mg";
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Delete_MissingName_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
        });
    }

    [Fact]
    public async Task Delete_NameNotGuid_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Name = "not-a-guid";
        });
    }

    [Fact]
    public async Task Delete_ScopeAndResourceGroup_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = $"/subscriptions/{TestSubscriptionId}";
            p.ResourceGroupName = "test-rg";
            p.Name = AssignmentName;
        });
    }

    [Fact]
    public async Task Delete_NoScopeAndNoSubscription_Throws()
    {
        await AssertThrows(p =>
        {
            // SubscriptionId left empty and no scope provided.
            p.Name = AssignmentName;
        });
    }

    [Fact]
    public async Task Delete_WhitespaceOnlyScope_Throws()
    {
        // Same hardening as create/list: a bound-but-blank scope must not be silently treated as
        // omitted, which would otherwise widen the delete target to the whole subscription.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = "   ";
            p.Name = AssignmentName;
        });
    }

    [Fact]
    public async Task Delete_WhitespaceOnlyResourceGroup_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "   ";
            p.Name = AssignmentName;
        });
    }

    [Fact]
    public async Task Delete_EmptyStringScope_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = string.Empty;
            p.Name = AssignmentName;
        });
    }

    [Fact]
    public async Task Delete_EmptyStringResourceGroup_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = string.Empty;
            p.Name = AssignmentName;
        });
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment delete", ClientType.CLI);
        var processor = (DeleteRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [argumentName] = Value("test-rg"),
                ["name"] = Value(AssignmentName),
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("n")]
    public void CliBinding_BindsNameAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment delete", ClientType.CLI);
        var processor = (DeleteRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [argumentName] = Value(AssignmentName) },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal(AssignmentName, processor.Name);
    }

    [Fact]
    public async Task Delete_NoContentResponse_WritesNothing()
    {
        // ARM returns 204 when the assignment did not exist; this is a successful no-op, not an
        // error, and nothing should be written to the output pipeline.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        DeleteRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Name = AssignmentName;

        await processor.ProcessAsync();

        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_OkResponseWithBody_WritesDeletedRoleAssignment()
    {
        // ARM returns 200 with the deleted RoleAssignment when it existed; this must be written to
        // the output pipeline, matching classic Remove-AzRoleAssignment's OutputType(RoleAssignment).
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse(
            $$"""{ "name": "{{AssignmentName}}", "properties": { "principalId": "11111111-2222-3333-4444-555555555555" } }"""));
        DeleteRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Name = AssignmentName;

        await processor.ProcessAsync();

        var written = Assert.Single(output.WrittenValues);
        var assignment = Assert.IsType<RoleAssignment>(written);
        Assert.Equal(AssignmentName, assignment.Name);
    }

    private static async Task<(Uri Uri, HttpMethod Method)> Capture(Action<DeleteRoleAssignmentProcessor_Orchestrator> configure)
    {
        Uri? sentUri = null;
        HttpMethod? sentMethod = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            sentMethod = request.Method;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        DeleteRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        configure(processor);

        await processor.ProcessAsync();
        return (sentUri!, sentMethod!);
    }

    private static async Task AssertThrows(Action<DeleteRoleAssignmentProcessor_Orchestrator> configure)
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        DeleteRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        configure(processor);

        UsageErrorException ex = await Assert.ThrowsAnyAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("--", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    private static DeleteRoleAssignmentProcessor_Orchestrator CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new DeleteRoleAssignmentProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
