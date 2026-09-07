// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Sql.Processors;
using Xunit;
using SqlServerModel = Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="NewSqlServerProcessor"/>. `az sql server create` is a single
/// long-running PUT to <c>Microsoft.Sql/servers</c>: these pin the request shape (verb, URI,
/// pinned api-version, body mapping), the fact that unset optional properties are omitted from
/// the PUT body rather than sent as nulls, and that the LRO loop polls
/// <c>Azure-AsyncOperation</c> to a terminal state before writing output.
/// </summary>
public class NewSqlServerProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ExpectedCreateUri =
        "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735"
        + "/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001?api-version=2025-01-01";

    private const string SucceededServerBody = """
        {
          "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001",
          "name": "myserver001",
          "type": "Microsoft.Sql/servers",
          "location": "eastus",
          "properties": {
            "administratorLogin": "sqladmin",
            "fullyQualifiedDomainName": "myserver001.database.windows.net",
            "state": "Ready",
            "provisioningState": "Succeeded"
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

    private static NewSqlServerProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        string? subscriptionId = TestSubscriptionId)
    {
        var processor = new NewSqlServerProcessor();
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
        processor.Name = "myserver001";
        processor.Location = "eastus";
        processor.AdministratorLogin = "sqladmin";
        processor.AdministratorLoginPassword = "not-a-real-password";
        return processor;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_PutsToServersUri_WithPinnedApiVersion()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Put, recorder.Methods[0]);
        Assert.Equal(ExpectedCreateUri, recorder.Uris[0]);

        var server = Assert.IsType<SqlServerModel>(Assert.Single(output.WrittenValues));
        Assert.Equal("myserver001", server.Name);
        Assert.Equal("myserver001.database.windows.net", server.Properties?.FullyQualifiedDomainName);
    }

    [Fact]
    public async Task CreateRequestMessage_OmitsUnsetOptionalProperties_FromPutBody()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        Assert.Contains("\"location\":\"eastus\"", body);
        Assert.Contains("\"administratorLogin\":\"sqladmin\"", body);
        Assert.Contains("\"administratorLoginPassword\":\"not-a-real-password\"", body);
        // WhenWritingNull: unset optionals must be absent, not sent as explicit nulls.
        Assert.DoesNotContain("version", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publicNetworkAccess", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRequestMessage_MapsOptionalProperties_WhenSupplied()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.ServerVersion = "12.0";
        processor.MinimalTlsVersion = "1.2";
        processor.PublicNetworkAccess = "Disabled";

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        Assert.Contains("\"version\":\"12.0\"", body);
        Assert.Contains("\"minimalTlsVersion\":\"1.2\"", body);
        Assert.Contains("\"publicNetworkAccess\":\"Disabled\"", body);
    }

    [Fact]
    public async Task CreateRequestMessage_PlacesTagsOnEnvelope_NotUnderProperties()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Tags = ["env=prod", "cost=cc-1234"];

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        // Tags belong to TrackedResource, so they must sit beside "location", never inside "properties".
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        JsonElement tags = root.GetProperty("tags");
        Assert.Equal("prod", tags.GetProperty("env").GetString());
        Assert.Equal("cc-1234", tags.GetProperty("cost").GetString());
        Assert.False(root.GetProperty("properties").TryGetProperty("tags", out _), "tags must not be nested under properties");
    }

    [Fact]
    public async Task CreateRequestMessage_TreatsBareTagKeyAsEmptyValue()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Tags = ["archived", "env=prod"];

        await processor.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[0]);
        // `az --tags key[=value]` makes the value optional; a bare key is an empty-valued tag.
        Assert.Contains("\"tags\":{\"archived\":\"\",\"env\":\"prod\"}", body);
    }

    [Fact]
    public async Task CreateRequestMessage_RejectsTagWithEmptyKey()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Tags = ["=orphan-value"];

        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
    }

    [Fact]
    public async Task SendAsync_PollsAsyncOperation_UntilProvisioningSucceeded()
    {
        const string inProgress = """{ "status": "InProgress" }""";
        const string succeeded = """{ "status": "Succeeded" }""";
        const string asyncOperationUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Sql/locations/eastus/serverAzureAsyncOperation/op1?api-version=2025-01-01";

        var recorder = new RequestRecorder(sendCount => sendCount switch
        {
            // 1: the create PUT -> 202 + Azure-AsyncOperation
            1 => Accepted(asyncOperationUri),
            // 2-3: polls that report the operation is still running
            2 => Json(HttpStatusCode.OK, inProgress),
            3 => Json(HttpStatusCode.OK, succeeded),
            // 4: the final GET on the original resource URI
            _ => Json(HttpStatusCode.OK, SucceededServerBody),
        });
        var pipeline = new FakeHttpPipeline(recorder.Respond) { PollingInterval = 0 };
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(4, pipeline.SendCount);
        Assert.Equal(HttpMethod.Put, recorder.Methods[0]);
        Assert.Equal(asyncOperationUri, recorder.Uris[1]);
        Assert.All(recorder.Methods.Skip(1), method => Assert.Equal(HttpMethod.Get, method));
        // The final GET goes back to the resource itself, not the operation endpoint.
        Assert.Equal(ExpectedCreateUri, recorder.Uris[^1]);

        var server = Assert.IsType<SqlServerModel>(Assert.Single(output.WrittenValues));
        Assert.Equal("Ready", server.Properties?.State);
    }

    [Fact]
    public async Task SendAsync_ResolvesSubscriptionIdFromProfile_WhenParameterNotSupplied()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.OK, SucceededServerBody));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var processor = CreateProcessor(pipeline, new FakeOutputHandler(), subscriptionId: null);

        await processor.ProcessAsync();

        Assert.StartsWith(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/",
            recorder.Uris[0],
            StringComparison.Ordinal);
    }

    private static HttpResponseMessage Accepted(string asyncOperationUri)
    {
        var response = Json(HttpStatusCode.Accepted, """{ "status": "InProgress" }""");
        response.Headers.Add("Azure-AsyncOperation", asyncOperationUri);
        return response;
    }
}
