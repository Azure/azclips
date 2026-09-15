using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the record→save→playback loop the <c>AZURE_TEST_MODE</c> switch drives, using an
/// explicit mode and a temp file so nothing touches the committed recordings. A fake backend
/// stands in for the live pipeline in record mode; playback runs against a backend that throws
/// if the network is reached.
/// </summary>
[Collection("Scenario Tests")]
public class RecordingSessionTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "session-switch-rg";
    private const string Location = "centralus";

    private sealed class FakeArmBackend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body =
                $"{{\"id\":\"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}\"," +
                $"\"name\":\"{ResourceGroupName}\",\"type\":\"Microsoft.Resources/resourceGroups\"," +
                $"\"location\":\"{Location}\",\"properties\":{{\"provisioningState\":\"Succeeded\"}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class UnreachableBackend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Playback must not reach the network, but sent: {request.Method} {request.RequestUri}");
    }

    [Fact]
    public void RecordMode_WritesFile_ThenPlaybackReplaysIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "azclips-mode-switch-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "GroupCreate.json");
        try
        {
            // Record: run the scenario against the fake pipeline, then persist.
            var recordSession = RecordingSession.Open(RecorderMode.Record, path);
            using (var host = new ScenarioHost(ClientType.CLI, recordSession.CreateInterceptor(), new FakeArmBackend()))
            {
                AssertCreated(RunCli(host));
            }
            Assert.DoesNotContain(recordSession.Record.Entries, e => e.Request.Headers.ContainsKey("Authorization"));
            recordSession.Save();

            Assert.True(File.Exists(path), "Record mode should write the recording to its file.");
            Assert.Single(SessionRecord.Load(path).Entries);

            // Playback: a new session over the written file replays both surfaces with no network.
            var playbackSession = RecordingSession.Open(RecorderMode.Playback, path);
            var before = File.ReadAllText(path);

            using (var cli = new ScenarioHost(ClientType.CLI, playbackSession.CreateInterceptor(), new UnreachableBackend()))
            {
                AssertCreated(RunCli(cli));
            }
            using (var ps = new ScenarioHost(ClientType.PowerShell, playbackSession.CreateInterceptor(), new UnreachableBackend()))
            {
                AssertCreated(RunPowerShell(ps));
            }

            // Save is a no-op in playback: the committed recording is never rewritten.
            playbackSession.Save();
            Assert.Equal(before, File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static IReadOnlyList<object> RunCli(ScenarioHost host) =>
        host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", SubscriptionId);

    private static IReadOnlyList<object> RunPowerShell(ScenarioHost host) =>
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

    private static void AssertCreated(IReadOnlyList<object> outputs)
    {
        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }
}
