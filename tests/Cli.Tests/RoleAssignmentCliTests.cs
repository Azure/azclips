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
    private const string RoleDefinitionId = "cccccccc-1111-2222-3333-444444444444";
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
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
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
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_UsesExplicitScope()
    {
        Uri? sentUri = await CaptureUri(p => p.Scope = Scope);

        Assert.Equal(
            $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_NormalizesUserSuppliedScope()
    {
        Uri? sentUri = await CaptureUri(p => p.Scope = "subscriptions/sub/resourceGroups/rg/");

        Assert.Equal(
            "https://management.azure.com/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_AppliesAssigneeObjectIdFilter()
    {
        // ARM rejects atScope() combined with "principalId eq", so a scoped assignee filter is applied
        // client-side; the request itself carries only atScope().
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
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
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
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
    public async Task List_WhitespaceOnlyResourceGroup_Throws()
    {
        // Shared RoleAssignmentScope.Resolve: a bound-but-blank resource group must not be silently
        // treated as omitted, which would otherwise widen the list to the whole subscription.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "   ";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_EmptyStringScope_Throws()
    {
        // An explicitly bound empty string (--scope "") is distinct from an unbound (null) scope and
        // must be rejected too: the CLI parser preserves an empty quoted string verbatim.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Scope = string.Empty;

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_RepeatedSlashScope_Throws()
    {
        // Shared RoleAssignmentScope.Normalize: a leading "//" is not a well-formed scope. Left
        // unchecked, TrimEnd('/') would collapse an all-slashes value to the same empty string used
        // for the tenant-root scope, silently turning malformed input into a tenant-root list request.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Scope = "//";

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

        MissingRequiredArgumentException ex = await Assert.ThrowsAsync<MissingRequiredArgumentException>(() => processor.ProcessAsync());
        Assert.True(ex.HasMissingRequiredParameters);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_UnwrapsValueEnvelope_IntoFlatArray()
    {
        string body = $$"""
            {
              "value": [
                { "id": "/subscriptions/s/providers/Microsoft.Authorization/roleAssignments/a1", "name": "a1", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } },
                { "id": "/subscriptions/s/providers/Microsoft.Authorization/roleAssignments/a2", "name": "a2", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } }
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

    [Fact]
    public async Task List_FollowsNextLink_AcrossPages()
    {
        var sentUris = new List<string>();
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count switch
            {
                1 => JsonResponse("""
                    {
                      "value": [ { "name": "a1" }, { "name": "a2" } ],
                      "nextLink": "https://management.azure.com/rolepage2"
                    }
                    """),
                2 => JsonResponse("""
                    {
                      "value": [ { "name": "a3" }, { "name": "a4" } ],
                      "nextLink": "https://management.azure.com/rolepage3"
                    }
                    """),
                _ => JsonResponse("""{ "value": [ { "name": "a5" } ] }""")
            };
        });
        ListRoleAssignmentProcessor processor = CreateProcessor(new ListRoleAssignmentProcessor(), pipeline, output);
        processor.Scope = Scope;

        await processor.ProcessAsync();

        Assert.Equal(3, sentUris.Count);
        Assert.Equal("https://management.azure.com/rolepage2", sentUris[1]);
        Assert.Equal("https://management.azure.com/rolepage3", sentUris[2]);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["a1", "a2", "a3", "a4", "a5"], assignments.Select(a => a.Name!).ToArray());
    }

    [Fact]
    public void CliMetadata_ExposesRoleParameter()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);

        Assert.Equal("RoleDefinitionId", metadata.Parameters["role"].Name);
        Assert.False(metadata.Parameters["role"].IsRequired);
    }

    [Fact]
    public async Task List_FiltersByRoleDefinitionId_ClientSide()
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "a1", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "roleDefinitionId": "/subscriptions/s/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}" } },
                    { "name": "a2", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "roleDefinitionId": "/subscriptions/s/providers/Microsoft.Authorization/roleDefinitions/99999999-8888-7777-6666-555555555555" } },
                    { "name": "a3", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "roleDefinitionId": "/subscriptions/s/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId.ToUpperInvariant()}}" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        // Role filtering is client-side: the request carries only atScope() (no role in $filter).
        Assert.DoesNotContain("principalId", sentUri!.AbsoluteUri);
        Assert.Contains("atScope", sentUri!.AbsoluteUri);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["a1", "a3"], assignments.Select(a => a.Name!).ToArray());
    }

    [Fact]
    public async Task List_RoleFilter_CombinesWithAssigneeClientFilter()
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "match", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "principalId": "{{AssigneeObjectId}}", "roleDefinitionId": "/x/roleDefinitions/{{RoleDefinitionId}}" } },
                    { "name": "wrongRole", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "principalId": "{{AssigneeObjectId}}", "roleDefinitionId": "/x/roleDefinitions/99999999-8888-7777-6666-555555555555" } },
                    { "name": "wrongPrincipal", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "principalId": "00000000-0000-0000-0000-000000000000", "roleDefinitionId": "/x/roleDefinitions/{{RoleDefinitionId}}" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        // ARM forbids atScope() + "principalId eq", so the request carries only atScope(); both the
        // assignee and role filters are applied client-side on top.
        Assert.Contains("$filter=atScope%28%29", sentUri!.AbsoluteUri);
        Assert.DoesNotContain("principalId", sentUri!.AbsoluteUri);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("match", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_RoleNotGuid_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.RoleDefinitionId = "Reader";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_Default_TrimsInheritedAssignmentsToExactScope()
    {
        // atScope() returns assignments at this scope AND inherited from ancestors; the default
        // (no --include-inherited) trims the ancestor ones away, leaving only the exact scope.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse($$"""
            {
              "value": [
                { "name": "atSub", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } },
                { "name": "inheritedFromMg", "properties": { "scope": "/providers/Microsoft.Management/managementGroups/mg1" } }
              ]
            }
            """));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;

        await processor.ProcessAsync();

        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("atSub", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_IncludeInherited_KeepsAncestorAssignments()
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "atSub", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } },
                    { "name": "inheritedFromMg", "properties": { "scope": "/providers/Microsoft.Management/managementGroups/mg1" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.IncludeInherited = true;

        await processor.ProcessAsync();

        Assert.Contains("$filter=atScope%28%29", sentUri!.AbsoluteUri);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["atSub", "inheritedFromMg"], assignments.Select(a => a.Name!).ToArray());
    }

    [Fact]
    public async Task List_All_ListsSubscriptionWithoutAtScope_AndKeepsNestedAssignments()
    {
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "atSub", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } },
                    { "name": "inChildRg", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}/resourceGroups/child-rg" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.All = true;

        await processor.ProcessAsync();

        // --all sends a plain List For Scope at the subscription: no atScope() filter, so nested
        // (subscope) assignments are returned and not trimmed.
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            sentUri!.AbsoluteUri);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["atSub", "inChildRg"], assignments.Select(a => a.Name!).ToArray());
    }

    [Fact]
    public async Task List_AllWithScope_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.All = true;
        processor.Scope = Scope;

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_AllWithResourceGroup_Throws()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.All = true;
        processor.ResourceGroupName = "test-rg";

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_AllWithAssignee_UsesServerSidePrincipalFilter()
    {
        // With no atScope() in play (--all), ARM accepts the principalId filter server-side.
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.All = true;
            p.AssigneeObjectId = AssigneeObjectId;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=principalId%20eq%20%27{AssigneeObjectId}%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_IncludeGroups_WithScopeAndAssignee_UsesAtScopeAndAssignedTo()
    {
        // atScope() + assignedTo() is the one server-side combination ARM allows. assignedTo() expands
        // group membership, so an assignment whose principalId is a group (not the queried object) is
        // kept — there is no client-side principal trim in this path.
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "viaGroup", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "principalId": "99999999-9999-9999-9999-999999999999" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.IncludeGroups = true;

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29%20and%20assignedTo%28%27{AssigneeObjectId}%27%29",
            sentUri!.AbsoluteUri);
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("viaGroup", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_AllWithAssigneeAndGroups_UsesAssignedToFilter()
    {
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.All = true;
            p.AssigneeObjectId = AssigneeObjectId;
            p.IncludeGroups = true;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=assignedTo%28%27{AssigneeObjectId}%27%29",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public void CliMetadata_ExposesScopeBreadthFlags()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);

        Assert.Equal("IncludeInherited", metadata.Parameters["include-inherited"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["include-inherited"].ParameterType);
        Assert.Equal("All", metadata.Parameters["all"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["all"].ParameterType);
        Assert.Equal("IncludeGroups", metadata.Parameters["include-groups"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["include-groups"].ParameterType);
    }

    [Fact]
    public void CliBinding_BindsScopeBreadthFlags()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["include-inherited"] = new List<List<string>> { new() },
                ["all"] = new List<List<string>> { new() },
                ["include-groups"] = new List<List<string>> { new() },
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.True(processor.IncludeInherited);
        Assert.True(processor.All);
        Assert.True(processor.IncludeGroups);
    }

    [Fact]
    public async Task List_AssigneeObjectIdBracedGuid_NormalizedForClientTrim()
    {
        // Guid.TryParse accepts a braced GUID, but ARM's principalId is the canonical hyphenated form.
        // The orchestrator must normalize before the client-side principal trim, or the result is empty.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse($$"""
            {
              "value": [
                { "name": "match", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "principalId": "{{AssigneeObjectId}}" } }
              ]
            }
            """));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = "{" + AssigneeObjectId + "}";

        await processor.ProcessAsync();

        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("match", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_AllWithBracedAssignee_NormalizesServerFilter()
    {
        // The server-side principalId filter must also receive the canonical GUID, not the braced input.
        Uri? sentUri = await CaptureUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.All = true;
            p.AssigneeObjectId = "{" + AssigneeObjectId + "}";
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=principalId%20eq%20%27{AssigneeObjectId}%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_RoleDefinitionIdBracedGuid_NormalizedForClientFilter()
    {
        // Same normalization concern as the assignee: a braced role GUID must still match ARM's
        // canonical roleDefinitionId tail in the client-side role filter.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse($$"""
            {
              "value": [
                { "name": "match", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}", "roleDefinitionId": "/subscriptions/s/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}" } }
              ]
            }
            """));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.RoleDefinitionId = "{" + RoleDefinitionId + "}";

        await processor.ProcessAsync();

        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("match", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_TenantRootScope_KeepsTenantRootAssignments()
    {
        // Normalize("/") yields "" (the request-path form of tenant root), but ARM returns those
        // assignments with scope "/". The exact-scope trim must compare against "/", not "", or it
        // would discard every tenant-root assignment.
        Uri? sentUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    { "name": "atRoot", "properties": { "scope": "/" } },
                    { "name": "atSub", "properties": { "scope": "/subscriptions/{{TestSubscriptionId}}" } }
                  ]
                }
                """);
        });
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.Scope = "/";

        await processor.ProcessAsync();

        // Request path is tenant root (no scope segment before /providers).
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=atScope%28%29",
            sentUri!.AbsoluteUri);
        // Only the tenant-root assignment is kept by the default exact-scope trim.
        var assignments = Assert.IsType<List<RoleAssignment>>(Assert.Single(output.WrittenValues));
        Assert.Equal("atRoot", Assert.Single(assignments).Name);
    }

    [Fact]
    public async Task List_IncludeGroupsWithoutAssignee_Throws()
    {
        // --include-groups / -ExpandPrincipalGroups filters a principal's group memberships, so it
        // requires an assignee; without one it must error rather than be a silent no-op.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.IncludeGroups = true;

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task List_AllWithIncludeInherited_Throws()
    {
        // --all (nested scopes below) and --include-inherited (ancestor scopes above) widen in opposite
        // directions and cannot both be honoured by one list call, so the combination is rejected.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("""{"value":[]}"""));
        ListRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(new ListRoleAssignmentProcessor_Orchestrator(), pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.All = true;
        processor.IncludeInherited = true;

        await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Equal(0, pipeline.SendCount);
    }

    private static async Task<Uri?> CaptureUri(Action<ListRoleAssignmentProcessor_Orchestrator> configure)
    {
        Uri? sentUri = null;        var output = new FakeOutputHandler();
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
