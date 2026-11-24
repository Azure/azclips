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
    private const string ResourceGroupName = "rg";
    private const string IdentityName = "id1";
    private const string SecondIdentityName = "id2";
    private const string CredentialName = "fic1";
    private const string Location = "westus";
    private const string Issuer = "https://issuer";
    private const string Subject = "sub1";
    private const string ApiVersion = "2025-05-31-preview";
    private const string IdentitiesProvider = "providers/Microsoft.ManagedIdentity/userAssignedIdentities";
    private const string SubscriptionIdentitiesPath = $"/subscriptions/{SubscriptionId}/{IdentitiesProvider}";
    private const string ResourceGroupIdentitiesPath = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/{IdentitiesProvider}";
    private const string IdentityPath = $"{ResourceGroupIdentitiesPath}/{IdentityName}";
    private const string SecondIdentityPath = $"{ResourceGroupIdentitiesPath}/{SecondIdentityName}";
    private const string OtherSubscriptionIdentityPath = $"/subscriptions/{OtherSubscriptionId}/resourceGroups/{ResourceGroupName}/{IdentitiesProvider}/{IdentityName}";
    private const string CredentialsPath = $"{IdentityPath}/federatedIdentityCredentials";
    private const string CredentialPath = $"{CredentialsPath}/{CredentialName}";
    private const string IdentityUrl = ArmUrl + IdentityPath;
    private const string CredentialsUrl = ArmUrl + CredentialsPath;
    private const string CredentialUrl = ArmUrl + CredentialPath;
    private const string IdentityBody = $$$"""{"id":"{{{IdentityPath}}}","name":"{{{IdentityName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities","location":"{{{Location}}}","tags":{"env":"dev"},"properties":{"tenantId":"t","principalId":"p","clientId":"c","isolationScope":"None"},"systemData":{"createdBy":"x"}}""";
    private const string SecondIdentityBody = $$"""{"id":"{{SecondIdentityPath}}","name":"{{SecondIdentityName}}"}""";
    private const string CredentialBody = $$$"""{"id":"{{{CredentialPath}}}","name":"{{{CredentialName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials","properties":{"issuer":"{{{Issuer}}}","subject":"{{{Subject}}}","audiences":["api://AzureADTokenExchange"]}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public ManagedIdentityCommandRequestTests() => _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command with the arguments it needs and a response it accepts.
    public static TheoryData<string[], string> GetIdentityCommands() => new()
    {
        { ["identity", "create", "-n", IdentityName, "-g", ResourceGroupName, "-l", Location], IdentityBody },
        { ["identity", "show", "-n", IdentityName, "-g", ResourceGroupName], IdentityBody },
        { ["identity", "list"], EmptyList },
        { ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--tags", "env=prod"], IdentityBody },
        { ["identity", "delete", "-n", IdentityName, "-g", ResourceGroupName], IdentityBody },
        { ["identity", "federated-credential", "create", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName, "--issuer", Issuer, "--subject", Subject], CredentialBody },
        { ["identity", "federated-credential", "show", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName], CredentialBody },
        { ["identity", "federated-credential", "list", "-g", ResourceGroupName, "--identity-name", IdentityName], EmptyList },
        { ["identity", "federated-credential", "delete", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName], CredentialBody },
    };

    [Theory]
    [MemberData(nameof(GetIdentityCommands))]
    public async Task RunAsync_IdentityCommand_SendsManagedIdentityApiVersion(string[] commandLine, string responseBody)
    {
        _mockWire.RespondToAnyRequest(responseBody);
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
            ["identity", "create", "--name", IdentityName, "--resource-group", ResourceGroupName, "--location", Location]);
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
            ["identity", "create", "--name", IdentityName, "--resource-group", ResourceGroupName, "--location", Location]);
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
            ["identity", "create", "--resource-name", IdentityName, "-g", ResourceGroupName, "-l", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityCreateSubscription_SendsPutUnderGivenSubscription()
    {
        _mockWire.RespondTo(HttpMethod.Put, ArmUrl + OtherSubscriptionIdentityPath, HttpStatusCode.OK, IdentityBody.Replace(SubscriptionId, OtherSubscriptionId));
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "-n", IdentityName, "-g", ResourceGroupName, "-l", Location, "--subscription", OtherSubscriptionId]);
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
                "-g", ResourceGroupName,
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
                "-g", ResourceGroupName,
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
            ["identity", "create", "-n", IdentityName, "-g", ResourceGroupName, "-l", Location, "--acquire-policy-token", "--change-reference", "CR-1"]);
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
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "-n", IdentityName, "-g", ResourceGroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {IdentityPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityShowTwoIds_SendsGetForEachId()
    {
        const string secondPath = $"/subscriptions/{SubscriptionId}/resourceGroups/rg2/{IdentitiesProvider}/{SecondIdentityName}";
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + secondPath, HttpStatusCode.OK, $$"""{"id":"{{secondPath}}","name":"{{SecondIdentityName}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "--ids", IdentityPath, secondPath]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {IdentityPath}?api-version={ApiVersion}", $"GET {secondPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── identity list ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityList_SendsGetToSubscriptionScope()
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + SubscriptionIdentitiesPath, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {SubscriptionIdentitiesPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityListResourceGroup_SendsGetToResourceGroupScope()
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + ResourceGroupIdentitiesPath, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list", "-g", ResourceGroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {ResourceGroupIdentitiesPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_IdentityListNextLink_FollowsNextLink()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[],"nextLink":"{{ArmUrl}}{{SubscriptionIdentitiesPath}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("p2", _mockWire.SentRequests()[1].QueryValue("$skiptoken"));
    }

    // ── identity update ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityUpdateTags_SendsSinglePatch()
    {
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--tags", "env=prod"]);
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
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--tags", "env=prod"]);
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
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--isolation-scope", "Regional", "--resource-restriction", "Microsoft.Web"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"properties":{"assignmentRestrictions":{"providers":["Microsoft.Web"]},"isolationScope":"Regional"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateWithoutChanges_SendsEmptyPatchBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName]);
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
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--set", "tags.owner=me"]);
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
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--set", "tags.owner=me"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"dev","owner":"me"},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSetAndTags_AppliesTagsOverFetchedTags()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--set", "tags.owner=me", "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"prod","owner":"me"},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateRemove_PatchesBodyWithoutRemovedTag()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--remove", "tags.env"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateAdd_AppendsToFetchedList()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK,
            IdentityBody.Replace("\"isolationScope\":\"None\"", "\"isolationScope\":\"None\",\"assignmentRestrictions\":{\"providers\":[\"Microsoft.Sql\"]}"));
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--add", "properties.assignmentRestrictions.providers", "Microsoft.Web"]);
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
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--set", "tags.count=5"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus","tags":{"env":"dev","count":5},"properties":{"isolationScope":"None"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateSetNumberForceString_SendsJsonString()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", ResourceGroupName, "--set", "tags.count=5", "--force-string", "true"]);
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
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "delete", "-n", IdentityName, "-g", ResourceGroupName]);
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
                "-g", ResourceGroupName,
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
                "-g", ResourceGroupName,
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
            ["identity", "federated-credential", "create", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

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
                "-g", ResourceGroupName,
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
            ["identity", "federated-credential", "show", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName]);
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
            ["identity", "federated-credential", "list", "-g", ResourceGroupName, "--identity-name", IdentityName]);
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
            ["identity", "federated-credential", "list", "-g", ResourceGroupName, "--identity-name", IdentityName, "--top", "5"]);
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
            ["identity", "federated-credential", "list", "-g", ResourceGroupName, "--identity-name", IdentityName, "--skiptoken", "abc def"]);
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
            ["identity", "federated-credential", "list", "-g", ResourceGroupName, "--identity-name", IdentityName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("p2", _mockWire.SentRequests()[1].QueryValue("$skiptoken"));
    }

    // ── federated-credential update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialUpdate_GetsCredentialThenPuts()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", "sub2"]);
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
            ["identity", "federated-credential", "update", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", "sub2"]);
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
                "-g", ResourceGroupName,
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
            ["identity", "federated-credential", "update", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName]);
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
            ["identity", "federated-credential", "update", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName, "--set", "properties.subject=sub3"]);
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
            ["identity", "federated-credential", "update", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", "sub2"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal([$"GET {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── federated-credential delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialDelete_SendsDeleteToCredentialPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, CredentialUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "delete", "-g", ResourceGroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {CredentialPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }
}
