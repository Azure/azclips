// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Sql.Models;
using Microsoft.Azure.Azclips.Extensions.Sql.Orchestrators;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="NewSqlDatabaseProcessor_Orchestrator"/>. <c>az sql db create</c> is
/// two API calls, not one: ARM requires <c>location</c> in the database PUT body but neither
/// shipping client accepts it, so the orchestrator GETs the parent server first and inherits its
/// location. These pin that ordering, the request shapes (verb, URI, pinned api-version, body
/// mapping), the fact that unset optionals are omitted rather than sent as nulls, that
/// <c>--elastic-pool</c> is expanded into a full ARM resource id, and that the LRO loop polls
/// <c>Azure-AsyncOperation</c> to a terminal state before writing output.
/// </summary>
public class NewSqlDatabaseOrchestratorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private const string ExpectedServerUri =
        "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735"
        + "/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001?api-version=2023-08-01";

    private const string ExpectedCreateUri =
        "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735"
        + "/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001/databases/mydb?api-version=2023-08-01";

    private const string ServerBody = """
        {
          "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001",
          "name": "myserver001",
          "type": "Microsoft.Sql/servers",
          "location": "eastus",
          "properties": { "administratorLogin": "sqladmin", "state": "Ready" }
        }
        """;

    private const string SucceededDatabaseBody = """
        {
          "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Sql/servers/myserver001/databases/mydb",
          "name": "mydb",
          "type": "Microsoft.Sql/servers/databases",
          "location": "eastus",
          "sku": { "name": "S0", "tier": "Standard", "capacity": 10 },
          "properties": {
            "collation": "SQL_Latin1_General_CP1_CI_AS",
            "maxSizeBytes": 268435456000,
            "status": "Online",
            "currentServiceObjectiveName": "S0",
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

    private static NewSqlDatabaseProcessor_Orchestrator CreateOrchestrator(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        string? subscriptionId = TestSubscriptionId)
    {
        var orchestrator = new NewSqlDatabaseProcessor_Orchestrator();
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        orchestrator.Init(context);
        orchestrator.SubscriptionId = subscriptionId ?? string.Empty;
        orchestrator.ResourceGroupName = "my-rg";
        orchestrator.ServerName = "myserver001";
        orchestrator.Name = "mydb";
        return orchestrator;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>GET the server, then PUT the database.</summary>
    private static Func<int, HttpResponseMessage> ServerThenDatabase() => sendCount => sendCount switch
    {
        1 => Json(HttpStatusCode.OK, ServerBody),
        _ => Json(HttpStatusCode.OK, SucceededDatabaseBody),
    };

    [Fact]
    public async Task ProcessAsync_GetsServerFirst_ThenPutsToDatabasesUri_WithPinnedApiVersion()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var output = new FakeOutputHandler();
        var orchestrator = CreateOrchestrator(pipeline, output);

        await orchestrator.ProcessAsync();

        Assert.Equal(HttpMethod.Get, recorder.Methods[0]);
        Assert.Equal(ExpectedServerUri, recorder.Uris[0]);
        Assert.Equal(HttpMethod.Put, recorder.Methods[1]);
        Assert.Equal(ExpectedCreateUri, recorder.Uris[1]);
        // Everything after the create PUT is LRO polling, which never leaves the database URI.
        Assert.All(recorder.Uris.Skip(1), uri => Assert.Equal(ExpectedCreateUri, uri));

        var database = Assert.IsType<SqlDatabase>(Assert.Single(output.WrittenValues));
        Assert.Equal("mydb", database.Name);
        Assert.Equal("Online", database.Properties?.Status);
    }

    [Fact]
    public async Task CreateRequestMessage_InheritsLocation_FromParentServer()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var orchestrator = CreateOrchestrator(pipeline, new FakeOutputHandler());

        await orchestrator.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[1]);
        Assert.Contains("\"location\":\"eastus\"", body);
    }

    [Fact]
    public async Task CreateRequestMessage_OmitsUnsetOptionalProperties_FromPutBody()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var orchestrator = CreateOrchestrator(pipeline, new FakeOutputHandler());

        await orchestrator.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[1]);
        // WhenWritingNull plus the empty-object guards: with no optional flags supplied, the body
        // carries a location and nothing else — no "sku": null, no "properties": null.
        Assert.Contains("\"location\":\"eastus\"", body);
        Assert.DoesNotContain("sku", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("properties", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRequestMessage_MapsSkuAndProperties_WhenSupplied()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var orchestrator = CreateOrchestrator(pipeline, new FakeOutputHandler());
        orchestrator.Edition = "GeneralPurpose";
        orchestrator.ServiceObjective = "GP_Gen5_2";
        orchestrator.Family = "Gen5";
        orchestrator.Capacity = 2;
        orchestrator.Collation = "SQL_Latin1_General_CP1_CI_AS";
        orchestrator.MaxSizeBytes = 268435456000L;
        orchestrator.ZoneRedundant = true;

        await orchestrator.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[1]);
        Assert.Contains("\"name\":\"GP_Gen5_2\"", body);
        Assert.Contains("\"tier\":\"GeneralPurpose\"", body);
        Assert.Contains("\"family\":\"Gen5\"", body);
        Assert.Contains("\"capacity\":2", body);
        Assert.Contains("\"collation\":\"SQL_Latin1_General_CP1_CI_AS\"", body);
        Assert.Contains("\"maxSizeBytes\":268435456000", body);
        Assert.Contains("\"zoneRedundant\":true", body);
    }

    [Fact]
    public async Task CreateRequestMessage_ExpandsElasticPoolName_IntoResourceId()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var orchestrator = CreateOrchestrator(pipeline, new FakeOutputHandler());
        orchestrator.ElasticPoolName = "mypool";

        await orchestrator.ProcessAsync();

        string body = Assert.IsType<string>(recorder.Bodies[1]);
        Assert.Contains(
            $"\"elasticPoolId\":\"/subscriptions/{TestSubscriptionId}/resourceGroups/my-rg"
            + "/providers/Microsoft.Sql/servers/myserver001/elasticPools/mypool\"",
            body);
    }

    [Fact]
    public async Task SendAsync_PollsAsyncOperation_UntilProvisioningSucceeded()
    {
        const string inProgress = """{ "status": "InProgress" }""";
        const string succeeded = """{ "status": "Succeeded" }""";
        const string asyncOperationUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Sql/locations/eastus/databaseAzureAsyncOperation/op1?api-version=2023-08-01";

        var recorder = new RequestRecorder(sendCount => sendCount switch
        {
            // 1: the server location lookup
            1 => Json(HttpStatusCode.OK, ServerBody),
            // 2: the create PUT -> 202 + Azure-AsyncOperation
            2 => Accepted(asyncOperationUri),
            // 3-4: polls that report the operation is still running, then done
            3 => Json(HttpStatusCode.OK, inProgress),
            4 => Json(HttpStatusCode.OK, succeeded),
            // 5: the final GET on the original resource URI
            _ => Json(HttpStatusCode.OK, SucceededDatabaseBody),
        });
        var pipeline = new FakeHttpPipeline(recorder.Respond) { PollingInterval = 0 };
        var output = new FakeOutputHandler();
        var orchestrator = CreateOrchestrator(pipeline, output);

        await orchestrator.ProcessAsync();

        Assert.Equal(5, pipeline.SendCount);
        Assert.Equal(HttpMethod.Put, recorder.Methods[1]);
        Assert.Equal(asyncOperationUri, recorder.Uris[2]);
        // The final GET goes back to the resource itself, not the operation endpoint.
        Assert.Equal(ExpectedCreateUri, recorder.Uris[^1]);

        var database = Assert.IsType<SqlDatabase>(Assert.Single(output.WrittenValues));
        Assert.Equal("S0", database.Sku?.Name);
    }

    [Fact]
    public async Task SendAsync_DoesNotCreateDatabase_WhenServerLookupFails()
    {
        var recorder = new RequestRecorder(_ => Json(HttpStatusCode.NotFound, """{ "error": { "code": "ResourceNotFound" } }"""));
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var output = new FakeOutputHandler();
        var orchestrator = CreateOrchestrator(pipeline, output);

        await Assert.ThrowsAnyAsync<Exception>(() => orchestrator.ProcessAsync());

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Get, Assert.Single(recorder.Methods));
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task SendAsync_ResolvesSubscriptionIdFromProfile_WhenParameterNotSupplied()
    {
        var recorder = new RequestRecorder(ServerThenDatabase());
        var pipeline = new FakeHttpPipeline(recorder.Respond);
        var orchestrator = CreateOrchestrator(pipeline, new FakeOutputHandler(), subscriptionId: null);

        await orchestrator.ProcessAsync();

        Assert.All(recorder.Uris, uri => Assert.StartsWith(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/",
            uri,
            StringComparison.Ordinal));
    }

    private static HttpResponseMessage Accepted(string asyncOperationUri)
    {
        var response = Json(HttpStatusCode.Accepted, """{ "status": "InProgress" }""");
        response.Headers.Add("Azure-AsyncOperation", asyncOperationUri);
        return response;
    }
}
