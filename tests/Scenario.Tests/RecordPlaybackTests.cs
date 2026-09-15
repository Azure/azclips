using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Acceptance tests for the record/playback <c>SendAsyncStep</c>, run through the full
/// engine: real dispatcher, binder, auth step, and pipeline. The "wire" behind the pipeline's
/// terminal <c>HttpClient</c> is a fake handler, so record mode is exercised end-to-end with no
/// real network and no credentials.
/// </summary>
[Collection("Scenario Tests")]
public class RecordPlaybackTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "azclips-recorded-rg";
    private const string Location = "eastus2";

    private static readonly string s_expectedUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}?api-version=2025-04-01";

    /// <summary>Stands in for ARM behind the pipeline's terminal HttpClient and counts hits.</summary>
    private sealed class FakeArmBackend : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body =
                $"{{\"id\":\"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}\"," +
                $"\"name\":\"{ResourceGroupName}\",\"type\":\"Microsoft.Resources/resourceGroups\"," +
                $"\"location\":\"{Location}\",\"properties\":{{\"provisioningState\":\"Succeeded\"}}}}";
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "11111111-1111-1111-1111-111111111111");
            return Task.FromResult(response);
        }
    }

    /// <summary>Fails the test loudly if playback ever lets a request through to the wire.</summary>
    private sealed class UnreachableBackend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Playback must not reach the network, but sent: {request.Method} {request.RequestUri}");
    }

    private static IReadOnlyList<object> RunCliGroupCreate(ScenarioHost host) =>
        host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", SubscriptionId);

    private static SessionRecord RecordCliGroupCreate(out int backendCalls)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        var backend = new FakeArmBackend();
        using (var host = new ScenarioHost(ClientType.CLI, recorder, backend))
        {
            RunCliGroupCreate(host);
        }
        backendCalls = backend.Calls;
        return record;
    }

    [Fact]
    public void RecordMode_CapturesRequestAndResponse()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        var backend = new FakeArmBackend();
        using var host = new ScenarioHost(ClientType.CLI, recorder, backend);

        var outputs = RunCliGroupCreate(host);

        Assert.Equal(1, backend.Calls);
        var entry = Assert.Single(record.Entries);

        Assert.Equal("PUT", entry.Request.Method);
        Assert.Equal(s_expectedUri, entry.Request.Uri);
        Assert.NotNull(entry.Request.Body);
        Assert.Contains($"\"name\":\"{ResourceGroupName}\"", entry.Request.Body);
        Assert.Contains($"\"location\":\"{Location}\"", entry.Request.Body);

        // The token was really sent (the seam is post-auth) but must never be stored.
        Assert.True(Assert.Single(recorder.Sent).Headers.ContainsKey("Authorization"));
        Assert.False(entry.Request.Headers.ContainsKey("Authorization"));

        Assert.Equal(201, entry.Response.StatusCode);
        Assert.NotNull(entry.Response.Body);
        Assert.Contains("\"provisioningState\":\"Succeeded\"", entry.Response.Body);
        Assert.Equal("11111111-1111-1111-1111-111111111111", entry.Response.Headers["x-ms-request-id"].Single());
        Assert.Contains("application/json", entry.Response.ContentHeaders["Content-Type"].Single());

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }

    [Fact]
    public void RecordedSession_RoundTripsThroughJsonFile()
    {
        var record = RecordCliGroupCreate(out _);
        var path = Path.Combine(Path.GetTempPath(), "azclips-scenario-tests", Guid.NewGuid().ToString("N"), "GroupCreate.json");
        try
        {
            record.Save(path);
            var loaded = SessionRecord.Load(path);

            var original = Assert.Single(record.Entries);
            var roundTripped = Assert.Single(loaded.Entries);
            Assert.Equal(original.Request.Method, roundTripped.Request.Method);
            Assert.Equal(original.Request.Uri, roundTripped.Request.Uri);
            Assert.Equal(original.Request.Body, roundTripped.Request.Body);
            Assert.Equal(original.Request.Headers.Keys.Order(), roundTripped.Request.Headers.Keys.Order());
            Assert.Equal(original.Response.StatusCode, roundTripped.Response.StatusCode);
            Assert.Equal(original.Response.Body, roundTripped.Response.Body);
            Assert.Equal(original.Response.Headers["x-ms-request-id"], roundTripped.Response.Headers["x-ms-request-id"]);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void Playback_ServesRecordedResponse_WithoutNetwork()
    {
        // Record once on the CLI surface, then replay the same session on the PowerShell
        // surface, the author-once model end-to-end. The backend throws if playback leaks
        // a request past the interceptor.
        var record = RecordCliGroupCreate(out _);

        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.PowerShell, playback, new UnreachableBackend());

        var outputs = host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }

    [Fact]
    public void Playback_UnmatchedRequest_FailsWithClearDiagnostic()
    {
        // The record holds a different command's traffic, so group create cannot match.
        var record = new SessionRecord
        {
            Entries =
            [
                new SessionRecordEntry
                {
                    Request = new RecordedRequest
                    {
                        Method = "GET",
                        Uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/some-other-rg?api-version=2025-04-01",
                    },
                    Response = new RecordedResponse { StatusCode = 200, Body = "{}" },
                },
            ],
        };
        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.CLI, playback, new UnreachableBackend());

        var exception = Assert.Throws<InvalidOperationException>(() => RunCliGroupCreate(host));

        // The diagnostic names the unmatched request and lists what the record still holds.
        Assert.Contains($"PUT {s_expectedUri}", exception.Message);
        Assert.Contains("GET https://management.azure.com", exception.Message);
        Assert.Contains("Unconsumed recorded entries (1 of 1)", exception.Message);
    }
}
