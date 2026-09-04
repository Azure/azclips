// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Sql.Models;
using Microsoft.Azure.Azclips.Extensions.Sql.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="NewSqlServerFirewallRuleProcessor"/>.
/// <c>az sql server firewall-rule create</c> is a single SYNCHRONOUS PUT to
/// <c>Microsoft.Sql/servers/firewallRules</c>: these pin the request shape (verb, URI, pinned
/// api-version, body mapping), the absence of a <c>location</c> (proxy resource), and above all
/// that exactly one request is issued - a 201 Created must not enter the LRO polling loop.
/// </summary>
public class NewSqlServerFirewallRuleProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ExpectedCreateUri =
        "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735"
        + "/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001"
        + "/firewallRules/AllowMyIp?api-version=2023-08-01";

    private const string CreatedRuleBody = """
        {
          "id": "/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001/firewallRules/AllowMyIp",
          "name": "AllowMyIp",
          "type": "Microsoft.Sql/servers/firewallRules",
          "properties": { "startIpAddress": "203.0.113.5", "endIpAddress": "203.0.113.10" }
        }
        """;

    private const string NotIpv4ErrorBody = """
        {
          "error": {
            "code": "FirewallRuleNotIPv4Address",
            "message": "The specified value is not a valid IPv4 address."
          }
        }
        """;

    /// <summary>Records every outbound request (and its body, before the pipeline disposes it).</summary>
    private sealed class RequestRecorder
    {
        private readonly Func<int, HttpResponseMessage> _responder;

        public RequestRecorder(Func<int, HttpResponseMessage> responder) => _responder = responder;

        public List<HttpMethod> Methods { get; } = new();
        public List<string> Uris { get; } = new();
        public List<string?> Bodies { get; } = new();

        public HttpResponseMessage Respond(HttpRequestMessage request, IEventPublisher _)
        {
            Methods.Add(request.Method);
            Uris.Add(request.RequestUri!.AbsoluteUri);
            Bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return _responder(Methods.Count);
        }
    }

    private static NewSqlServerFirewallRuleProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        string? subscriptionId = TestSubscriptionId)
    {
        var processor = new NewSqlServerFirewallRuleProcessor();
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        processor.SubscriptionId = subscriptionId ?? string.Empty;
        processor.ResourceGroupName = "my-rg";
        processor.ServerName = "myserver001";
        processor.Name = "AllowMyIp";
        processor.StartIpAddress = "203.0.113.5";
        processor.EndIpAddress = "203.0.113.10";
        return processor;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_PutsToFirewallRulesUri_WithPinnedApiVersion()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Put, recorder.Methods[0]);
        Assert.Equal(ExpectedCreateUri, recorder.Uris[0]);
    }

    [Fact]
    public async Task CreateRequestMessage_SendsStartAndEndIpAddress_UnderProperties()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        Assert.Contains("\"startIpAddress\":\"203.0.113.5\"", body);
        Assert.Contains("\"endIpAddress\":\"203.0.113.10\"", body);
        Assert.Contains("\"properties\"", body);
    }

    [Fact]
    public async Task CreateRequestMessage_OmitsLocation_BecauseFirewallRuleIsProxyResource()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        Assert.DoesNotContain("location", body, StringComparison.OrdinalIgnoreCase);
        // WhenWritingNull: unset members must be absent, not sent as explicit nulls.
        Assert.DoesNotContain("null", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_IssuesExactlyOneRequest_BecauseCreateIsNotLongRunning()
    {
        // 201 Created is the terminal ARM answer here. If this processor ever routed through
        // SendAsyncLRO, the loop condition would treat 201 as "keep polling" and burn a GET.
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.Created, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond) { PollingInterval = 0 };
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_DeserializesCreatedRule_AndWritesOutput()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.Created, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        var rule = Assert.IsType<SqlFirewallRule>(Assert.Single(output.WrittenValues));
        Assert.Equal("AllowMyIp", rule.Name);
        Assert.Equal("Microsoft.Sql/servers/firewallRules", rule.Type);
        Assert.Equal("203.0.113.5", rule.Properties?.StartIpAddress);
        Assert.Equal("203.0.113.10", rule.Properties?.EndIpAddress);
    }

    [Fact]
    public async Task SendAsync_ResolvesSubscriptionIdFromProfile_WhenParameterNotSupplied()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, CreatedRuleBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler(), subscriptionId: null);

        await processor.ProcessAsync();

        Assert.StartsWith(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/",
            recorder.Uris[0],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_Throws_AndWritesNothing_WhenArmRejectsTheRequest()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.BadRequest, NotIpv4ErrorBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.StartIpAddress = "not-an-ip";

        await Assert.ThrowsAnyAsync<Exception>(() => processor.ProcessAsync());

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }
}
