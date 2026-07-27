using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The authoring pattern scenario tests follow: a <see cref="RecordingSession"/> picks record or
/// playback from <c>AZURE_TEST_MODE</c>. Playback (default, CI) replays the committed recording
/// through both surfaces with no network; record refreshes it against the live pipeline. The
/// recording committed here is synthetic; the live POC refreshes it without touching this test.
/// </summary>
[Collection("Scenario Tests")]
public class GroupCreateScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-test-rg";
    private const string Location = "westus2";

    [Fact]
    public void CreatesResourceGroup()
    {
        var recording = RecordingSession.Open(typeof(GroupCreateScenarioTests), nameof(CreatesResourceGroup));

        if (recording.Mode == RecorderMode.Record)
        {
            // Capture once from a single surface against the live pipeline, then persist.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor());
            AssertCreated(RunCli(host));
            recording.Save();
            return;
        }

        // Playback: the one recording drives both surfaces, each with its own interceptor.
        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor()))
        {
            AssertCreated(RunCli(cli));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor()))
        {
            AssertCreated(RunPowerShell(ps));
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
