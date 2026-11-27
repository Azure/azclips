// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs the rest CLI command through the real engine against canned ARM responses and checks the response it writes,
/// or the error it throws for a response.
/// </summary>
public sealed class RestCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string GroupName = "rg";
    private const string Location = "westus";
    private const string ApiVersion = "2021-04-01";
    private const string GroupUrl = $"{ArmUrl}/subscriptions/{SubscriptionId}/resourcegroups/{GroupName}";
    private const string GroupUrlWithApiVersion = $"{GroupUrl}?api-version={ApiVersion}";
    private const string GroupBody = $$"""{"id":"/subscriptions/{{SubscriptionId}}/resourceGroups/{{GroupName}}","name":"{{GroupName}}","location":"{{Location}}"}""";
    private const string NotFoundError = """{"error":{"code":"ResourceGroupNotFound"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RestCommandResponseTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── rest ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RestOk_WritesStatusCode()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(200, Assert.IsType<RestResponse>(Assert.Single(_output)).StatusCode);
    }

    [Fact]
    public async Task RunAsync_RestOk_WritesRawContent()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(GroupBody, Assert.IsType<RestResponse>(Assert.Single(_output)).Content);
    }

    [Fact]
    public async Task RunAsync_RestOk_WritesParsedJson()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(Location, Assert.IsType<RestResponse>(Assert.Single(_output)).Json?.GetProperty("location").GetString());
    }

    [Fact]
    public async Task RunAsync_RestOk_WritesResponseHeaders()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody, "x-ms-request-id: req-1");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("req-1", Assert.IsType<RestResponse>(Assert.Single(_output)).Headers["x-ms-request-id"]);
    }

    [Fact]
    public async Task RunAsync_RestNoContent_WritesEmptyContent()
    {
        _mockWire.RespondTo(HttpMethod.Delete, GroupUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--method", "delete", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("", Assert.IsType<RestResponse>(Assert.Single(_output)).Content);
    }

    [Fact]
    public async Task RunAsync_RestNoContent_WritesNullJson()
    {
        _mockWire.RespondTo(HttpMethod.Delete, GroupUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--method", "delete", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Null(Assert.IsType<RestResponse>(Assert.Single(_output)).Json);
    }

    [Fact]
    public async Task RunAsync_RestNotFound_ThrowsStatusAndUrl()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.NotFound, NotFoundError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"404 Not Found from {GroupUrlWithApiVersion}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RestNotFound_WritesErrorResponseBeforeThrowing()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.NotFound, NotFoundError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["rest", "--uri", GroupUrlWithApiVersion]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        _ = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(404, Assert.IsType<RestResponse>(Assert.Single(_output)).StatusCode);
    }
}
