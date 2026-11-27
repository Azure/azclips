// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs resource group CLI commands through the real engine and checks the request each one sends to ARM: method,
/// path, decoded query values and JSON body. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class GroupCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string OtherSubscriptionId = "11111111-2222-3333-4444-555555555555";
    private const string GroupName = "rg";
    private const string SpacedGroupName = "my rg";
    private const string Location = "westus";
    private const string ForceDeletionType = "Microsoft.Compute/virtualMachines";
    private const string ApiVersion = "2025-04-01";
    private const string GroupsPath = $"/subscriptions/{SubscriptionId}/resourcegroups";
    private const string GroupPath = $"{GroupsPath}/{GroupName}";
    private const string OperationPath = $"/subscriptions/{SubscriptionId}/operationresults/op1";
    private const string GroupsUrl = ArmUrl + GroupsPath;
    private const string GroupUrl = ArmUrl + GroupPath;
    private const string OperationUrl = ArmUrl + OperationPath;
    private const string GroupBody = $$$"""{"id":"/subscriptions/{{{SubscriptionId}}}/resourceGroups/{{{GroupName}}}","name":"{{{GroupName}}}","type":"Microsoft.Resources/resourceGroups","location":"{{{Location}}}","managedBy":"me","tags":{"env":"dev"},"properties":{"provisioningState":"Succeeded"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public GroupCommandRequestTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command with the arguments it needs, the method it sends, the URL it sends to and a response for it.
    public static TheoryData<string[], string, string, string?> GetGroupCommands() => new()
    {
        { ["group", "create", "--name", GroupName, "--location", Location], "PUT", GroupUrl, GroupBody },
        { ["group", "update", "--name", GroupName, "--tags", "env=dev"], "PATCH", GroupUrl, GroupBody },
        { ["group", "show", "--name", GroupName], "GET", GroupUrl, GroupBody },
        { ["group", "exists", "--name", GroupName], "HEAD", GroupUrl, null },
        { ["group", "delete", "--name", GroupName], "DELETE", GroupUrl, null },
        { ["group", "list"], "GET", GroupsUrl, EmptyList },
    };

    // Each command that takes the resource group name under an alias, with the arguments it needs, the method it sends
    // and a response for it.
    public static TheoryData<string[], string, string?> GetGroupNameAliasCommands() => new()
    {
        { ["group", "update", "-n", GroupName, "--tags", "env=dev"], "PATCH", GroupBody },
        { ["group", "exists", "-n", GroupName], "HEAD", null },
        { ["group", "delete", "-n", GroupName], "DELETE", null },
        { ["group", "update", "--resource-group", GroupName, "--tags", "env=dev"], "PATCH", GroupBody },
        { ["group", "exists", "--resource-group", GroupName], "HEAD", null },
        { ["group", "delete", "--resource-group", GroupName], "DELETE", null },
        { ["group", "update", "-g", GroupName, "--tags", "env=dev"], "PATCH", GroupBody },
        { ["group", "exists", "-g", GroupName], "HEAD", null },
        { ["group", "delete", "-g", GroupName], "DELETE", null },
    };

    [Theory]
    [MemberData(nameof(GetGroupCommands))]
    public async Task RunAsync_GroupCommand_SendsResourceGroupApiVersion(string[] commandLine, string method, string url, string? responseBody)
    {
        _mockWire.RespondTo(new HttpMethod(method), url, HttpStatusCode.OK, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ApiVersion, Assert.Single(_mockWire.SentRequests()).QueryValue("api-version"));
    }

    [Theory]
    [MemberData(nameof(GetGroupCommands))]
    public async Task RunAsync_GroupCommandSubscriptionId_SendsRequestUnderGivenSubscription(string[] commandLine, string method, string url, string? responseBody)
    {
        string otherUrl = url.Replace(SubscriptionId, OtherSubscriptionId);
        _mockWire.RespondTo(new HttpMethod(method), otherUrl, HttpStatusCode.OK, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--subscription-id", OtherSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{method} {new Uri(otherUrl).AbsolutePath}"], _mockWire.RequestLines());
    }

    [Theory]
    [MemberData(nameof(GetGroupNameAliasCommands))]
    public async Task RunAsync_GroupCommandNameAlias_SendsRequestToGroupPath(string[] commandLine, string method, string? responseBody)
    {
        _mockWire.RespondTo(new HttpMethod(method), GroupUrl, HttpStatusCode.OK, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{method} {GroupPath}"], _mockWire.RequestLines());
    }

    // ── group create ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupCreate_SendsPutToGroupPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "create", "--name", GroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupCreate_SendsBodyWithEmptyReadOnlyFields()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "create", "--name", GroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: the body also carries the empty read-only id and type, an empty managedBy and null tags.
        Assert.Equal("""{"id":"","name":"rg","type":"","location":"westus","managedBy":"","tags":null}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupCreateNameWithSpace_SendsEscapedPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, $"{GroupsUrl}/{SpacedGroupName}", HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "create", "--name", SpacedGroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {GroupsPath}/my%20rg"], _mockWire.RequestLines());
    }

    // ── group show ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupShow_SendsGetToGroupPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "show", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── group exists ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupExists_SendsHeadToGroupPath()
    {
        _mockWire.RespondTo(HttpMethod.Head, GroupUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "exists", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"HEAD {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── group delete ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupDelete_SendsDeleteToGroupPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, GroupUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "delete", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Theory]
    [InlineData("--force-deletion-types")]
    [InlineData("-f")]
    public async Task RunAsync_GroupDeleteForceDeletionTypes_SendsForceDeletionTypesQuery(string flag)
    {
        _mockWire.RespondTo(HttpMethod.Delete, GroupUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["group", "delete", "--name", GroupName, flag, ForceDeletionType]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ForceDeletionType, Assert.Single(_mockWire.SentRequests()).QueryValue("forceDeletionTypes"));
    }

    [Fact]
    public async Task RunAsync_GroupDeleteAccepted_PollsLocationUntilDone()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Delete, GroupUrl, HttpStatusCode.Accepted, null,
            $"Location: {OperationUrl}?api-version={ApiVersion}", "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, OperationUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "delete", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"DELETE {GroupPath}?api-version={ApiVersion}", $"GET {OperationPath}?api-version={ApiVersion}"],
            _mockWire.FullRequestLines());
    }

    // ── group list ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupList_SendsGetToSubscriptionGroups()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupsPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupListNextLink_FollowsNextLink()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"name":"rg1"}],"nextLink":"{{GroupsUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            """{"value":[{"name":"rg2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [$"GET {GroupsPath}?api-version={ApiVersion}", $"GET {GroupsPath}?api-version={ApiVersion}&$skiptoken=p2"],
            _mockWire.FullRequestLines());
    }

    // ── group update ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupUpdateTags_SendsPatchToGroupPath()
    {
        _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--tags", "env=dev"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupUpdateTags_SplitsOnFirstEqualsAndDefaultsEmptyValue()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--tags", "env=dev", "team=a=b", "solo"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"tags":{"env":"dev","team":"a=b","solo":""}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateWithoutChanges_SendsEmptyPatchBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: with nothing to change the command still sends a PATCH.
        Assert.Equal("{}", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSet_SendsGetThenPatch()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--set", "tags.owner=me"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupPath}", $"PATCH {GroupPath}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSet_SendsCurrentGroupMergedWithChange()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--set", "tags.owner=me"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"name":"rg","managedBy":"me","tags":{"env":"dev","owner":"me"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetAndTags_SendsTagsReplacedBeforeSet()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["group", "update", "--name", GroupName, "--set", "tags.owner=me", "--tags", "a=1"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"name":"rg","managedBy":"me","tags":{"a":"1","owner":"me"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetNumber_SendsJsonNumber()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--set", "tags.count=5"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: a numeric tag value is sent as a JSON number although ARM tag values are strings.
        Assert.Equal("""{"name":"rg","managedBy":"me","tags":{"env":"dev","count":5}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetNumberForceString_SendsJsonString()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["group", "update", "--name", GroupName, "--set", "tags.count=5", "--force-string"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"name":"rg","managedBy":"me","tags":{"env":"dev","count":"5"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupUpdateSetManagedByAnyCase_SendsManagedBy()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--set", "ManagedBy=you"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"name":"rg","managedBy":"you","tags":{"env":"dev"}}""", Assert.Single(sentBodies));
    }
}
