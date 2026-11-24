// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cli.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs managed identity and federated credential CLI commands through the real engine and checks the request each one
/// sends to ARM: method, path, decoded query values and JSON body. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class ManagedIdentityCommandRequestTests : IDisposable
{
    private const string ExtensionName = "ManagedServiceIdentity";
    private const string OtherSubscriptionId = "11111111-2222-3333-4444-555555555555";
    private const string GroupName = "rg";
    private const string IdentityName = "id1";
    private const string SecondIdentityName = "id2";
    private const string CredentialName = "fic1";
    private const string Location = "westus";
    private const string Issuer = "https://issuer";
    private const string Subject = "sub1";
    private const string UpdatedSubject = "sub2";
    private const string ApiVersion = "2025-05-31-preview";
    private const string IdentitiesProvider = "providers/Microsoft.ManagedIdentity/userAssignedIdentities";
    private const string SubscriptionIdentitiesPath = $"/subscriptions/{SubscriptionId}/{IdentitiesProvider}";
    private const string GroupIdentitiesPath = $"/subscriptions/{SubscriptionId}/resourceGroups/{GroupName}/{IdentitiesProvider}";
    private const string IdentityPath = $"{GroupIdentitiesPath}/{IdentityName}";
    private const string SecondIdentityPath = $"{GroupIdentitiesPath}/{SecondIdentityName}";
    private const string OtherSubscriptionIdentityPath = $"/subscriptions/{OtherSubscriptionId}/resourceGroups/{GroupName}/{IdentitiesProvider}/{IdentityName}";
    private const string CredentialsPath = $"{IdentityPath}/federatedIdentityCredentials";
    private const string CredentialPath = $"{CredentialsPath}/{CredentialName}";
    private const string SubscriptionIdentitiesUrl = ArmUrl + SubscriptionIdentitiesPath;
    private const string IdentityUrl = ArmUrl + IdentityPath;
    private const string CredentialsUrl = ArmUrl + CredentialsPath;
    private const string CredentialUrl = ArmUrl + CredentialPath;
    private const string IdentityBody = $$$"""{"id":"{{{IdentityPath}}}","name":"{{{IdentityName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities","location":"{{{Location}}}","tags":{"env":"dev"},"properties":{"tenantId":"t","principalId":"p","clientId":"c","isolationScope":"None"},"systemData":{"createdBy":"x"}}""";
    private const string IdentityWithRestrictionBody = $$$"""{"id":"{{{IdentityPath}}}","name":"{{{IdentityName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities","location":"{{{Location}}}","tags":{"env":"dev"},"properties":{"tenantId":"t","principalId":"p","clientId":"c","isolationScope":"None","assignmentRestrictions":{"providers":["Microsoft.Sql"]}},"systemData":{"createdBy":"x"}}""";
    private const string SecondIdentityBody = $$"""{"id":"{{SecondIdentityPath}}","name":"{{SecondIdentityName}}"}""";
    private const string CredentialBody = $$$"""{"id":"{{{CredentialPath}}}","name":"{{{CredentialName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials","properties":{"issuer":"{{{Issuer}}}","subject":"{{{Subject}}}","audiences":["api://AzureADTokenExchange"]}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public ManagedIdentityCommandRequestTests() => _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command that sends one request, with the arguments it needs, the method it sends, the URL it sends to and a response for it.
    public static TheoryData<string[], string, string, string?> GetIdentityCommands() => new()
    {
        { ["identity", "create", "-n", IdentityName, "-g", GroupName, "-l", Location], "PUT", IdentityUrl, IdentityBody },
        { ["identity", "show", "-n", IdentityName, "-g", GroupName], "GET", IdentityUrl, IdentityBody },
        { ["identity", "list"], "GET", SubscriptionIdentitiesUrl, EmptyList },
        { ["identity", "update", "-n", IdentityName, "-g", GroupName, "--tags", "env=prod"], "PATCH", IdentityUrl, IdentityBody },
        { ["identity", "delete", "-n", IdentityName, "-g", GroupName], "DELETE", IdentityUrl, null },
        {
            ["identity", "federated-credential", "create", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--issuer", Issuer, "--subject", Subject],
            "PUT",
            CredentialUrl,
            CredentialBody
        },
        { ["identity", "federated-credential", "show", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName], "GET", CredentialUrl, CredentialBody },
        { ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName], "GET", CredentialsUrl, EmptyList },
        { ["identity", "federated-credential", "delete", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName], "DELETE", CredentialUrl, null },
    };

    [Theory]
    [MemberData(nameof(GetIdentityCommands))]
    public async Task RunAsync_IdentityCommand_SendsManagedIdentityApiVersion(string[] commandLine, string method, string url, string? responseBody)
    {
        _mockWire.RespondTo(new HttpMethod(method), url, HttpStatusCode.OK, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ApiVersion, Assert.Single(_mockWire.SentRequests()).QueryValue("api-version"));
    }

    // ── identity create ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityCreate_SendsPutToIdentityPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--name", IdentityName, "--resource-group", GroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityCreateRequiredArgs_SendsLocationOnlyBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--name", IdentityName, "--resource-group", GroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus"}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityCreateResourceNameAlias_SendsPutToIdentityPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "--resource-name", IdentityName, "-g", GroupName, "-l", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityCreateSubscription_SendsPutUnderGivenSubscription()
    {
        _mockWire.RespondTo(HttpMethod.Put, ArmUrl + OtherSubscriptionIdentityPath, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "-n", IdentityName, "-g", GroupName, "-l", Location, "--subscription", OtherSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {OtherSubscriptionIdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityCreateAllOptions_SendsTagsRestrictionsAndIsolationScope()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "create",
                "-n", IdentityName,
                "-g", GroupName,
                "-l", Location,
                "--tags", "env=dev", "team=a",
                "--isolation-scope", "Regional",
                "--resource-restriction", "Microsoft.Compute,Microsoft.Web", "Microsoft.Sql",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"location":"westus","tags":{"env":"dev","team":"a"},"properties":{"assignmentRestrictions":{"providers":["Microsoft.Compute","Microsoft.Web","Microsoft.Sql"]},"isolationScope":"Regional"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityCreateJsonTagsAndRestriction_SendsParsedJson()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "create",
                "-n", IdentityName,
                "-g", GroupName,
                "-l", Location,
                "--tags", """{"env":"dev"}""",
                "--resource-restriction", """{"providers":["Microsoft.Web"]}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"dev"},"properties":{"assignmentRestrictions":{"providers":["Microsoft.Web"]}}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityCreatePolicyTokenOptions_SendsLocationOnlyBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "-n", IdentityName, "-g", GroupName, "-l", Location, "--acquire-policy-token", "--change-reference", "CR-1"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: --acquire-policy-token and --change-reference are accepted but change nothing in the request.
        Assert.Equal("""{"location":"westus"}""", Assert.Single(sentBodies));
    }

    // ── identity show ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityShow_SendsGetToIdentityPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityShowTwoIds_SendsGetForEachId()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + SecondIdentityPath, HttpStatusCode.OK, SecondIdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "--ids", IdentityPath, SecondIdentityPath]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {IdentityPath}?api-version={ApiVersion}", $"GET {SecondIdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── identity list ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityList_SendsGetToSubscriptionScope()
    {
        _mockWire.RespondTo(HttpMethod.Get, SubscriptionIdentitiesUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {SubscriptionIdentitiesPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityListResourceGroup_SendsGetToResourceGroupScope()
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + GroupIdentitiesPath, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list", "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupIdentitiesPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityListNextLink_FollowsNextLink()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[],"nextLink":"{{SubscriptionIdentitiesUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"GET {SubscriptionIdentitiesPath}?api-version={ApiVersion}", $"GET {SubscriptionIdentitiesPath}?api-version={ApiVersion}&$skiptoken=p2"],
            _mockWire.FullRequestLines());
    }

    // ── identity update ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityUpdateTags_SendsSinglePatch()
    {
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateTags_SendsTagsOnlyBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"tags":{"env":"prod"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateIsolationScopeAndRestriction_SendsPropertiesOnlyBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--isolation-scope", "Regional", "--resource-restriction", "Microsoft.Web"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"assignmentRestrictions":{"providers":["Microsoft.Web"]},"isolationScope":"Regional"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateWithoutChanges_SendsEmptyPatchBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "update", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: an update with nothing to change still sends a PATCH with an empty object.
        Assert.Equal("{}", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSet_GetsIdentityThenPatches()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--set", "tags.owner=me"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {IdentityPath}?api-version={ApiVersion}", $"PATCH {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSet_PatchesMergedBodyWithoutReadOnlyProperties()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--set", "tags.owner=me"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"dev","owner":"me"},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSetAndTags_MergesTagsIntoFetchedTags()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--set", "tags.owner=me", "--tags", "team=a"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: identity update merges tags while group update replaces them.
        Assert.Equal("""{"location":"westus","tags":{"env":"dev","team":"a","owner":"me"},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateRemove_PatchesBodyWithoutRemovedTag()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--remove", "tags.env"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateAdd_AppendsToFetchedList()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityWithRestrictionBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--add", "properties.assignmentRestrictions.providers", "Microsoft.Web"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"location":"westus","tags":{"env":"dev"},"properties":{"isolationScope":"None","assignmentRestrictions":{"providers":["Microsoft.Sql","Microsoft.Web"]}}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSetNumber_SendsJsonNumber()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--set", "tags.count=5"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: a numeric tag value is sent as a JSON number although ARM tag values are strings.
        Assert.Equal("""{"location":"westus","tags":{"env":"dev","count":5},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSetNumberForceString_SendsJsonString()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--set", "tags.count=5", "--force-string", "true"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"dev","count":"5"},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateTwoIds_SendsPatchForEachId()
    {
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Patch, ArmUrl + SecondIdentityPath, HttpStatusCode.OK, SecondIdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "--ids", IdentityPath, SecondIdentityPath, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {IdentityPath}?api-version={ApiVersion}", $"PATCH {SecondIdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── identity delete ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityDelete_SendsDeleteToIdentityPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, IdentityUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "delete", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityDeleteTwoIds_SendsDeleteForEachId()
    {
        _mockWire.RespondTo(HttpMethod.Delete, IdentityUrl, HttpStatusCode.OK);
        _mockWire.RespondTo(HttpMethod.Delete, ArmUrl + SecondIdentityPath, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "delete", "--ids", IdentityPath, SecondIdentityPath]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {IdentityPath}?api-version={ApiVersion}", $"DELETE {SecondIdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── federated-credential create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialCreate_SendsPutToCredentialPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "-g", GroupName,
                "--identity-name", IdentityName,
                "-n", CredentialName,
                "--issuer", Issuer,
                "--subject", Subject,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CredentialCreateIssuerAndSubject_SendsDefaultAudience()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "-g", GroupName,
                "--identity-name", IdentityName,
                "-n", CredentialName,
                "--issuer", Issuer,
                "--subject", Subject,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"audiences":["api://AzureADTokenExchange"],"issuer":"https://issuer","subject":"sub1"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialCreateRequiredArgsOnly_SendsOnlyDefaultAudience()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "create", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: issuer and subject are optional, so a body ARM rejects is sent.
        Assert.Equal("""{"properties":{"audiences":["api://AzureADTokenExchange"]}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialCreateAudiencesAndClaimsExpression_SendsThemInBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "-g", GroupName,
                "--identity-name", IdentityName,
                "-n", CredentialName,
                "--issuer", Issuer,
                "--audiences", "aud1", "aud2",
                "--cme-version", "1",
                "--cme-value", "claims['sub'] eq 'x'",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: apostrophes are sent as \u0027 (System.Text.Json default escaping).
        Assert.Equal(
            """{"properties":{"audiences":["aud1","aud2"],"issuer":"https://issuer","claimsMatchingExpression":{"languageVersion":1,"value":"claims[\u0027sub\u0027] eq \u0027x\u0027"}}}""",
            Assert.Single(sentBodies));
    }

    // ── federated-credential show ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialShow_SendsGetToCredentialPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "show", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── federated-credential list ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialList_SendsGetToCredentialsPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CredentialsPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CredentialListTop_SendsTopQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName, "--top", "5"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("5", Assert.Single(_mockWire.SentRequests()).QueryValue("$top"));
    }

    [Fact]
    public async Task RunAsync_CredentialListSkipToken_SendsSkipTokenQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName, "--skiptoken", "abc def"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("abc def", Assert.Single(_mockWire.SentRequests()).QueryValue("$skiptoken"));
    }

    [Fact]
    public async Task RunAsync_CredentialListNextLink_FollowsNextLink()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[],"nextLink":"{{CredentialsUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"GET {CredentialsPath}?api-version={ApiVersion}", $"GET {CredentialsPath}?api-version={ApiVersion}&$skiptoken=p2"],
            _mockWire.FullRequestLines());
    }

    // ── federated-credential update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialUpdate_GetsCredentialThenPuts()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", UpdatedSubject]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CredentialPath}?api-version={ApiVersion}", $"PUT {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateSubject_PutsFetchedPropertiesWithNewSubject()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", UpdatedSubject]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"issuer":"https://issuer","subject":"sub2","audiences":["api://AzureADTokenExchange"]}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateAudiencesIssuerAndClaimsExpression_ReplacesThemInBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "update",
                "-g", GroupName,
                "--identity-name", IdentityName,
                "-n", CredentialName,
                "--audiences", "aud9",
                "--issuer", "https://issuer2",
                "--cme-version", "1",
                "--cme-value", "v",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"issuer":"https://issuer2","subject":"sub1","audiences":["aud9"],"claimsMatchingExpression":{"languageVersion":1,"value":"v"}}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateWithoutChanges_PutsFetchedProperties()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"issuer":"https://issuer","subject":"sub1","audiences":["api://AzureADTokenExchange"]}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateSet_PutsBodyWithSetValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--set", "properties.subject=sub3"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"issuer":"https://issuer","subject":"sub3","audiences":["api://AzureADTokenExchange"]}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateNotFound_SendsNoPut()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.NotFound, """{"error":{"code":"NotFound","message":"Federated credential fic1 was not found."}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", UpdatedSubject]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        _ = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal([$"GET {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── federated-credential delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialDelete_SendsDeleteToCredentialPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, CredentialUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "delete", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }
}
