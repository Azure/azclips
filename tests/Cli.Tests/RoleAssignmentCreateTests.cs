// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
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
    public async Task Create_RepeatedSlashScope_Throws()
    {
        // A leading "//" is not a well-formed scope. Left unchecked, Normalize's TrimEnd('/') would
        // collapse an all-slashes value to the same empty string used for the tenant-root scope,
        // silently turning malformed input into a tenant-root role assignment request.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.Scope = "//";
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
    public async Task Create_WhitespaceOnlyCondition_Throws()
    {
        // An explicitly bound but blank --condition/-Condition must not be silently treated as
        // omitted: that would turn an intended conditional (restricted) grant into an unconditional
        // one instead of failing.
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Condition = "   ";
        });
    }

    [Fact]
    public async Task Create_EmptyStringCondition_Throws()
    {
        // Same concern as above, for an explicitly bound empty string --condition "".
        await AssertThrows(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.AssigneeObjectId = AssigneeObjectId;
            p.RoleDefinitionId = RoleDefinitionId;
            p.Condition = string.Empty;
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
            metadata,
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

        MissingRequiredArgumentException ex = await Assert.ThrowsAsync<MissingRequiredArgumentException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("--", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_Conflict_WithGeneratedName_ReturnsExistingAssignment()
    {
        // No --name supplied, so the name is auto-generated. A RoleAssignmentExists conflict means the
        // same principal/role/scope is already assigned under another name; the create is idempotent and
        // returns that existing assignment instead of failing.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"The role assignment already exists."}}""")
                : JsonResponse($$"""
                    {
                      "value": [
                        {
                          "name": "existing-name",
                          "properties": {
                            "principalId": "{{AssigneeObjectId}}",
                            "scope": "/subscriptions/{{TestSubscriptionId}}",
                            "roleDefinitionId": "/subscriptions/{{TestSubscriptionId}}/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                          }
                        }
                      ]
                    }
                    """));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;
        // No Name: auto-generated.

        await processor.ProcessAsync();

        var existing = Assert.IsType<RoleAssignment>(Assert.Single(output.WrittenValues));
        Assert.Equal("existing-name", existing.Name);
        // The PUT plus the follow-up lookup GET.
        Assert.Equal(2, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_Conflict_WithExplicitName_Throws()
    {
        // An explicit --name means the caller asked for a specific assignment. A conflict must surface,
        // not silently return a differently-named existing assignment.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) =>
            ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"The role assignment already exists."}}"""));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;
        processor.Name = AssignmentName;

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        // Only the PUT: no idempotent lookup is attempted for an explicit name.
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_Conflict_GeneratedName_NoMatchingExisting_Rethrows()
    {
        // Generated name and a conflict, but the follow-up lookup finds no matching assignment (e.g. a
        // read-permission gap or a race). The original conflict is surfaced rather than swallowed.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"The role assignment already exists."}}""")
                : JsonResponse("""{"value":[]}"""));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Create_NonConflictError_Propagates_EvenWithGeneratedName()
    {
        // Only a RoleAssignmentExists 409 is treated as idempotent; any other failure propagates.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"code":"InternalServerError","message":"boom"}}""", Encoding.UTF8, "application/json"),
            });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_Conflict_GeneratedName_LooksUpByPrincipalAtScope()
    {
        // The idempotent lookup lists the principal's assignments at the grant scope.
        Uri? lookupUri = null;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                return ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""");
            }

            lookupUri = request.RequestUri;
            return JsonResponse($$"""
                {
                  "value": [
                    {
                      "name": "existing-name",
                      "properties": {
                        "principalId": "{{AssigneeObjectId}}",
                        "scope": "/subscriptions/{{TestSubscriptionId}}",
                        "roleDefinitionId": "/subscriptions/{{TestSubscriptionId}}/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                      }
                    }
                  ]
                }
                """);
        });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        Assert.NotNull(lookupUri);
        Assert.StartsWith(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01",
            lookupUri!.AbsoluteUri);
        Assert.Contains($"principalId%20eq%20%27{AssigneeObjectId}%27", lookupUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Create_Conflict_WithGeneratedName_PublishesNoFailureEvent()
    {
        // The idempotent success path must not emit ResourceOperationFailed: the expected 409 is handled,
        // so the engine must not surface an ERROR: / PowerShell error record for a successful create.
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"The role assignment already exists."}}""")
                : JsonResponse($$"""
                    {
                      "value": [
                        {
                          "name": "existing-name",
                          "properties": {
                            "principalId": "{{AssigneeObjectId}}",
                            "scope": "/subscriptions/{{TestSubscriptionId}}",
                            "roleDefinitionId": "/subscriptions/{{TestSubscriptionId}}/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                          }
                        }
                      ]
                    }
                    """));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output, publisher);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        Assert.Single(output.WrittenValues);
        Assert.DoesNotContain(Events.ResourceOperationFailed, publisher.PublishedIds);
    }

    [Fact]
    public async Task Create_Conflict_GeneratedName_FollowsPaginationToFindExisting()
    {
        // The matching assignment is on the second list page; the lookup must follow nextLink rather
        // than examining only the first page (which would return null and rethrow the 409).
        var output = new FakeOutputHandler();
        int listCall = 0;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                return ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""");
            }

            // First list page: a non-matching assignment + a nextLink. Second page: the match.
            listCall++;
            return listCall == 1
                ? JsonResponse("""
                    {
                      "value": [
                        { "name": "other", "properties": { "principalId": "99999999-9999-9999-9999-999999999999", "scope": "/subscriptions/x", "roleDefinitionId": "/x/roleDefinitions/00000000-0000-0000-0000-000000000000" } }
                      ],
                      "nextLink": "https://management.azure.com/rolepage2"
                    }
                    """)
                : JsonResponse($$"""
                    {
                      "value": [
                        {
                          "name": "existing-on-page-2",
                          "properties": {
                            "principalId": "{{AssigneeObjectId}}",
                            "scope": "/subscriptions/{{TestSubscriptionId}}",
                            "roleDefinitionId": "/subscriptions/{{TestSubscriptionId}}/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                          }
                        }
                      ]
                    }
                    """);
        });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        var existing = Assert.IsType<RoleAssignment>(Assert.Single(output.WrittenValues));
        Assert.Equal("existing-on-page-2", existing.Name);
        // PUT + list page 1 + list page 2.
        Assert.Equal(3, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_Conflict_GeneratedName_TenantRootScope_FindsExisting()
    {
        // Tenant-root (--scope /) normalizes to "" in the request path, but ARM reports the existing
        // assignment's scope as "/". The match must remap "" to "/" so a tenant-root create still recovers
        // its existing assignment instead of rethrowing the 409.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""")
                : JsonResponse($$"""
                    {
                      "value": [
                        {
                          "name": "existing-at-root",
                          "properties": {
                            "principalId": "{{AssigneeObjectId}}",
                            "scope": "/",
                            "roleDefinitionId": "/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                          }
                        }
                      ]
                    }
                    """));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.Scope = "/";
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        var existing = Assert.IsType<RoleAssignment>(Assert.Single(output.WrittenValues));
        Assert.Equal("existing-at-root", existing.Name);
    }

    [Fact]
    public async Task Create_Conflict_WrongErrorCode_NotSwallowed_EvenIfMessageMentionsToken()
    {
        // A 409 whose error.code is NOT RoleAssignmentExists must surface, even if its human-readable
        // message happens to mention the token. The idempotent path keys on error.code, not the message.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) =>
            ConflictResponse("""{"error":{"code":"SomeOtherConflict","message":"This is not a RoleAssignmentExists situation."}}"""));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;
        // Name auto-generated, so only the error.code guard stands between this and the idempotent path.

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        // Only the PUT: the wrong error code must not trigger a lookup.
        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Create_SuccessWithMalformedBody_PublishesInvalidJson_AndThrowsWrapped()
    {
        // A 2xx create whose body is not valid JSON must keep the base processor's diagnostics: publish
        // ResponseInvalidJson and throw a wrapped InvalidOperationException, not leak a bare JsonException.
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var pipeline = new FakeHttpPipeline((_, _) => JsonResponse("this is not json"));
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output, publisher);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.IsType<JsonException>(ex.InnerException);
        Assert.Contains(Events.ResponseInvalidJson, publisher.PublishedIds);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Create_Conflict_LookupPermissionFailure_PublishesNoExtraFailureEvent()
    {
        // The best-effort lookup must not route its own failure (here a 403) through EnsureSuccessAsync:
        // that would publish a second ResourceOperationFailed ahead of the one for the surfaced 409. With
        // the raw lookup transport, exactly one ResourceOperationFailed is published (for the final 409).
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""")
                : new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("""{"error":{"code":"AuthorizationFailed","message":"no read"}}""", Encoding.UTF8, "application/json"),
                });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output, publisher);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        // PUT + exactly one lookup GET (the 403), and only the final 409 publishes a failure event.
        Assert.Equal(2, pipeline.SendCount);
        Assert.Single(publisher.PublishedIds.FindAll(id => id == Events.ResourceOperationFailed));
    }

    [Fact]
    public async Task Create_Conflict_LookupCanceled_PropagatesCancellation()
    {
        // A cancellation raised during the lookup must surface as cancellation, not be swallowed by the
        // best-effort fallback (which would otherwise rethrow the original 409).
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
            request.Method == HttpMethod.Put
                ? ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""")
                : throw new OperationCanceledException());
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Create_Conflict_GeneratedName_TracesEachLookupPage()
    {
        // Every lookup page (not just the first) must publish its HTTP response trace; HttpPipeline.SendAsync
        // does not emit it on its own. A 2-page lookup therefore traces: the PUT + list page 1 + list page 2.
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        int listCall = 0;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                return ConflictResponse("""{"error":{"code":"RoleAssignmentExists","message":"exists"}}""");
            }

            listCall++;
            return listCall == 1
                ? JsonResponse("""
                    {
                      "value": [
                        { "name": "other", "properties": { "principalId": "99999999-9999-9999-9999-999999999999", "scope": "/subscriptions/x", "roleDefinitionId": "/x/roleDefinitions/00000000-0000-0000-0000-000000000000" } }
                      ],
                      "nextLink": "https://management.azure.com/rolepage2"
                    }
                    """)
                : JsonResponse($$"""
                    {
                      "value": [
                        {
                          "name": "existing-on-page-2",
                          "properties": {
                            "principalId": "{{AssigneeObjectId}}",
                            "scope": "/subscriptions/{{TestSubscriptionId}}",
                            "roleDefinitionId": "/subscriptions/{{TestSubscriptionId}}/providers/Microsoft.Authorization/roleDefinitions/{{RoleDefinitionId}}"
                          }
                        }
                      ]
                    }
                    """);
        });
        NewRoleAssignmentProcessor_Orchestrator processor = CreateProcessor(pipeline, output, publisher);
        processor.SubscriptionId = TestSubscriptionId;
        processor.AssigneeObjectId = AssigneeObjectId;
        processor.RoleDefinitionId = RoleDefinitionId;

        await processor.ProcessAsync();

        Assert.Equal(3, publisher.PublishedIds.FindAll(id => id == Events.TracingHttpResponseMessage).Count);
    }

    private static HttpResponseMessage ConflictResponse(string body) =>
        new(HttpStatusCode.Conflict)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

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

        UsageErrorException ex = await Assert.ThrowsAnyAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("--", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    private static NewRoleAssignmentProcessor_Orchestrator CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
        => CreateProcessor(pipeline, output, new FakeEventPublisher());

    private static NewRoleAssignmentProcessor_Orchestrator CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output, FakeEventPublisher publisher)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new NewRoleAssignmentProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            publisher,
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
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];
}
