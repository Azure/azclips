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
/// Runs the rest CLI command through the real engine and checks the request it sends: method, URL, headers and body.
/// The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class RestCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string OtherSubscriptionId = "11111111-2222-3333-4444-555555555555";
    private const string GroupName = "rg";
    private const string StorageProvider = "Microsoft.Storage";
    private const string StorageAccountType = "storageAccounts";
    private const string StorageAccountName = "sa1";
    private const string ApiVersion = "2021-04-01";
    private const string SubscriptionsApiVersion = "2022-12-01";
    private const string StorageApiVersion = "2023-01-01";
    private const string CustomHeaderName = "x-ms-custom";
    private const string CustomHeaderValue = "v1";
    private const string CustomHeader = $"{CustomHeaderName}={CustomHeaderValue}";
    private const string PlaceholderGroupsPath = "/subscriptions/{subscriptionId}/resourcegroups";
    private const string GroupsPath = $"/subscriptions/{SubscriptionId}/resourcegroups";
    private const string GroupPath = $"{GroupsPath}/{GroupName}";
    private const string StorageAccountPath = $"/subscriptions/{SubscriptionId}/resourceGroups/{GroupName}/providers/{StorageProvider}/{StorageAccountType}/{StorageAccountName}";
    private const string OtherSubscriptionStorageAccountsPath = $"/subscriptions/{OtherSubscriptionId}/providers/{StorageProvider}/{StorageAccountType}";
    private const string GroupsUrl = ArmUrl + GroupsPath;
    private const string GroupUrl = ArmUrl + GroupPath;
    private const string GroupUrlWithApiVersion = $"{GroupUrl}?api-version={ApiVersion}";
    private const string StorageAccountUrl = ArmUrl + StorageAccountPath;
    private const string EmptyObject = "{}";
    private const string LocationBody = """{"location":"westus"}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RestCommandRequestTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── rest --uri ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RestUriWithSubscriptionPlaceholder_SendsGetUnderProfileSubscription()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["rest", "--uri", $"{PlaceholderGroupsPath}?api-version={ApiVersion}"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupsPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestFullUri_SendsGetToUri()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestRelativeUriWithoutSlash_SendsGetUnderArmEndpoint()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{ArmUrl}/subscriptions", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", $"subscriptions?api-version={SubscriptionsApiVersion}"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /subscriptions?api-version={SubscriptionsApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestSubscriptionWithPlaceholder_SendsGetUnderProfileSubscription()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["rest", "--uri", $"{PlaceholderGroupsPath}?api-version={ApiVersion}", "--subscription", OtherSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: --subscription does not fill the {subscriptionId} placeholder; the profile subscription does.
        Assert.Equal([$"GET {GroupsPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestUriParameters_SendsParametersAsQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "rest",
                "--uri", PlaceholderGroupsPath,
                "--uri-parameters", $"api-version={ApiVersion}", "$filter=tagName eq 'a'",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupsPath}?api-version={ApiVersion}&$filter=tagName eq 'a'"], _mockWire.FullRequestLines());
    }

    // ── rest --method / --body ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("put", "PUT")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("delete", "DELETE")]
    public async Task RunAsync_RestMethod_SendsMethodInUpperCase(string method, string expected)
    {
        _mockWire.RespondTo(new HttpMethod(expected), GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--method", method, "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{expected} {GroupPath}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_RestBody_SendsBodyAsGiven()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--method", "put", "--uri", GroupUrlWithApiVersion, "--body", LocationBody]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"location":"westus"}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RestBody_SendsJsonContentType()
    {
        _mockWire.RespondTo(HttpMethod.Put, GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--method", "put", "--uri", GroupUrlWithApiVersion, "--body", LocationBody]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("application/json", Assert.Single(_mockWire.SentRequests()).Content?.Headers.ContentType?.ToString());
    }

    // ── rest --headers ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RestHeaders_SendsCustomHeader()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion, "--headers", CustomHeader]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(CustomHeaderValue, Assert.Single(_mockWire.SentRequests()).HeaderValues(CustomHeaderName));
    }

    [Fact]
    public async Task RunAsync_RestRepeatedHeadersFlag_SendsHeadersFromEveryFlag()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["rest", "--uri", GroupUrlWithApiVersion, "--headers", CustomHeader, "--headers", "If-Match=*"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([CustomHeaderValue, "*"], new[] { CustomHeaderName, "If-Match" }.Select(Assert.Single(_mockWire.SentRequests()).HeaderValues));
    }

    [Fact]
    public async Task RunAsync_RestContentTypeHeader_SendsBodyWithGivenContentType()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{ArmUrl}/x", HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "rest",
                "--method", "POST",
                "--uri", "/x?api-version=1",
                "--body", "a=b",
                "--headers", "Content-Type=application/x-www-form-urlencoded",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("application/x-www-form-urlencoded", Assert.Single(_mockWire.SentRequests()).Content?.Headers.ContentType?.ToString());
    }

    // ── rest by resource parameters ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RestResourceParameters_SendsGetToResourcePath()
    {
        _mockWire.RespondTo(HttpMethod.Get, StorageAccountUrl, HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "rest",
                "--resource-group", GroupName,
                "--resource-provider", StorageProvider,
                "--resource-type", StorageAccountType,
                "--name", StorageAccountName,
                "--api-version", StorageApiVersion,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {StorageAccountPath}?api-version={StorageApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestNestedResourceParameters_SendsGetToNestedResourcePath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{StorageAccountUrl}/blobServices/default", HttpStatusCode.OK, EmptyObject);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "rest",
                "--resource-group", GroupName,
                "--resource-provider", StorageProvider,
                "--resource-type", StorageAccountType, "blobServices",
                "--name", StorageAccountName, "default",
                "--api-version", StorageApiVersion,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {StorageAccountPath}/blobServices/default?api-version={StorageApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RestResourceParametersWithoutNameOrGroup_SendsGetToGivenSubscriptionCollection()
    {
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + OtherSubscriptionStorageAccountsPath, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "rest",
                "--subscription", OtherSubscriptionId,
                "--resource-provider", StorageProvider,
                "--resource-type", StorageAccountType,
                "--api-version", StorageApiVersion,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {OtherSubscriptionStorageAccountsPath}?api-version={StorageApiVersion}"], _mockWire.FullRequestLines());
    }
}
