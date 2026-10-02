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
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <c>az role assignment create</c> and <c>New-AzRoleAssignment</c>. Create issues a
/// PUT to <c>{scope}/providers/Microsoft.Authorization/roleAssignments/{name}</c> with a
/// <c>{ properties: { principalId, roleDefinitionId, ... } }</c> body. The orchestrator resolves the
/// scope, expands the role definition GUID to a full path, supplies the principal ID, and generates the
/// assignment name when omitted.
/// </summary>
public class RoleAssignmentCreateTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string AssigneeObjectId = "11111111-2222-3333-4444-555555555555";
    private const string RoleDefinitionId = "b24988ac-6180-42a0-ab88-20f7382dd24c";
    private const string AssignmentName = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Fact]
    public void CliMetadata_ExposesRoleAssignmentCreate()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment create", ClientType.CLI);

        Assert.Equal(typeof(NewRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);
        Assert.False(metadata.Parameters["scope"].IsRequired);
        Assert.True(metadata.Parameters["role"].IsRequired);
        Assert.False(metadata.Parameters["assignee-object-id"].IsRequired);
        Assert.Equal("RoleDefinitionId", metadata.Parameters["role"].Name);
        Assert.Equal("PrincipalType", metadata.Parameters["assignee-principal-type"].Name);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
    }

    [Fact]
    public async Task Create_PutsToScopedRoleAssignment_WithExpandedBody()
    {
        (Uri uri, HttpMethod method, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = $"/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(HttpMethod.Put, method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);

        JsonElement props = body.GetProperty("properties");
        Assert.Equal(AssigneeObjectId, props.GetProperty("principalId").GetString());
        Assert.Equal(
            $"/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{RoleDefinitionId}",
            props.GetProperty("roleDefinitionId").GetString());
        // Optional fields not supplied must be omitted (WhenWritingNull).
        Assert.False(props.TryGetProperty("principalType", out _));
        Assert.False(props.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task Create_DefaultsScopeToSubscription()
    {
        (Uri uri, _, _) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Create_GeneratesAssignmentName_WhenOmitted()
    {
        (Uri uri, _, _) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });

        string name = uri.AbsoluteUri.Split("/roleAssignments/")[1].Split('?')[0];
        Assert.True(Guid.TryParse(name, out _), $"generated name '{name}' should be a GUID");
    }

    [Fact]
    public async Task Create_IncludesPrincipalTypeAndDescription_WhenSupplied()
    {
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.PrincipalType = "ServicePrincipal";
            p.Description = "grant CI access";
        });

        JsonElement props = body.GetProperty("properties");
        Assert.Equal("ServicePrincipal", props.GetProperty("principalType").GetString());
        Assert.Equal("grant CI access", props.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Create_AssigneeAsName_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Assignee = "john@contoso.com";
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_AssigneeAndAssigneeObjectId_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Assignee = AssigneeObjectId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_RoleAsName_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = "Reader";
        });
    }

    [Fact]
    public async Task Create_MissingPrincipal_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_NameNotGuid_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = "not-a-guid";
        });
    }

    [Fact]
    public async Task Create_MissingRole_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
        });
    }

    [Fact]
    public async Task Create_ScopeAndResourceGroup_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = $"/subscriptions/{TestSubscriptionId}";
            p.ResourceGroupName = "test-rg";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_WhitespaceOnlyResourceGroup_Throws()
    {
        // An explicitly bound but blank -ResourceGroupName/--resource-group must not be silently
        // treated as omitted: that would widen the resolved scope to the whole subscription instead
        // of failing, granting broader access than requested.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "   ";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_WhitespaceOnlyScope_Throws()
    {
        // Same concern as above, for an explicitly bound but blank -Scope/--scope.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = "   ";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_EmptyStringScope_Throws()
    {
        // An explicitly bound empty string (--scope "") is distinct from an unbound (null) scope and
        // must be rejected too, not just whitespace: the CLI parser preserves an empty quoted string
        // verbatim, so this must not be silently treated as "not supplied" either.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = string.Empty;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_EmptyStringResourceGroup_Throws()
    {
        // Same concern as above, for an explicitly bound empty string --resource-group/-ResourceGroupName.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = string.Empty;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_NoScopeAndNoSubscription_Throws()
    {
        await AssertThrows(p =>
        {
            // SubscriptionId left empty and no scope provided.
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });
    }

    [Fact]
    public async Task Create_ManagementGroupScope_RootsRoleDefinitionAtScope()
    {
        (Uri uri, _, JsonElement body) = await Capture(p =>
        {
            p.Scope = "/providers/Microsoft.Management/managementGroups/mg";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
        Assert.Equal(
            $"/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Authorization/roleDefinitions/{RoleDefinitionId}",
            body.GetProperty("properties").GetProperty("roleDefinitionId").GetString());
    }

    [Fact]
    public async Task Create_ResourceScope_RootsRoleDefinitionAtSubscription()
    {
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.Scope = $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/acct";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{RoleDefinitionId}",
            body.GetProperty("properties").GetProperty("roleDefinitionId").GetString());
    }

    [Fact]
    public async Task Create_ConditionWithoutVersion_DefaultsVersionToTwoPointZero()
    {
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Condition = "@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:Name] stringEquals 'foo'";
        });

        JsonElement props = body.GetProperty("properties");
        Assert.Equal(
            "@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:Name] stringEquals 'foo'",
            props.GetProperty("condition").GetString());
        Assert.Equal("2.0", props.GetProperty("conditionVersion").GetString());
    }

    [Fact]
    public async Task Create_ConditionWithExplicitVersion_PassesThrough()
    {
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Condition = "@Resource[foo:Name] stringEquals 'bar'";
            p.ConditionVersion = "2.0";
        });

        JsonElement props = body.GetProperty("properties");
        Assert.Equal("2.0", props.GetProperty("conditionVersion").GetString());
    }

    [Fact]
    public async Task Create_NoCondition_OmitsConditionFields()
    {
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
        });

        JsonElement props = body.GetProperty("properties");
        Assert.False(props.TryGetProperty("condition", out _));
        Assert.False(props.TryGetProperty("conditionVersion", out _));
    }

    [Fact]
    public async Task Create_ConditionVersionWithoutCondition_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.ConditionVersion = "2.0";
        });
    }

    [Fact]
    public async Task Create_UnsupportedConditionVersion_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Condition = "@Resource[foo:Name] stringEquals 'bar'";
            p.ConditionVersion = "1.0";
        });
    }

    [Fact]
    public async Task Create_FullyQualifiedRoleDefinitionId_PassesThrough()
    {
        string fullId = $"/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{RoleDefinitionId}";
        (_, _, JsonElement body) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = fullId;
            p.Name = AssignmentName;
        });

        Assert.Equal(fullId, body.GetProperty("properties").GetProperty("roleDefinitionId").GetString());
    }

    [Fact]
    public async Task Create_InvalidRoleDefinitionPath_Throws()
    {
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = "/not-a-role";
        });
    }

    [Fact]
    public async Task Create_ResourceGroupScope_BuildsResourceGroupScope()
    {
        (Uri uri, _, _) = await Capture(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Name = AssignmentName;
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Authorization/roleAssignments/{AssignmentName}?api-version=2022-04-01",
            uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment create", ClientType.CLI);
        var processor = (NewRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        // --role is a required parameter on create (unlike list), so it must be supplied for the
        // binder's required-parameter check to pass; this test otherwise isolates the -g/--resource-group
        // alias -> ResourceGroupName binding.
        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [argumentName] = Value("test-rg"),
                ["role"] = Value(RoleDefinitionId),
                ["assignee-object-id"] = Value(AssigneeObjectId),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Fact]
    public void CliMetadata_ExposesResourceGroupOnCreate()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment create", ClientType.CLI);

        Assert.True(metadata.Parameters.ContainsKey("resource-group"));
        Assert.Equal("ResourceGroupName", metadata.Parameters["resource-group"].Name);
        Assert.False(metadata.Parameters["resource-group"].IsRequired);
        Assert.Equal("resource-group", metadata.ParameterAliases["g"]);
    }

    [Fact]
    public async Task Create_ResourceGroupWithoutSubscription_ReportsMissingSubscription()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("{}"));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "test-rg";
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;
        // SubscriptionId intentionally left empty.

        UsageErrorException ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Contains("subscription", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    private static async Task<(Uri Uri, HttpMethod Method, JsonElement Body)> Capture(Action<NewRoleAssignmentProcessor_Orchestrator> configure)
    {
        Uri? sentUri = null;
        HttpMethod? sentMethod = null;
        string sentBody = "{}";
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            sentMethod = request.Method;
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse($$"""{ "name": "created", "properties": { "principalId": "{{AssigneeObjectId}}" } }""");
        });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        configure(processor);

        await processor.ProcessAsync();
        using var doc = JsonDocument.Parse(sentBody);
        return (sentUri!, sentMethod!, doc.RootElement.Clone());
    }

    private static async Task AssertThrows(Action<NewRoleAssignmentProcessor_Orchestrator> configure)
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("{}"));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        configure(processor);

        UsageErrorException ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("--", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    private static NewRoleAssignmentProcessor_Orchestrator CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new NewRoleAssignmentProcessor_Orchestrator();
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

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];
}
