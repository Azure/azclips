using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The proof-of-concept scenario, and the pattern others follow. <see cref="RecordingSession"/>
/// picks the mode from <c>AZURE_TEST_MODE</c>: playback (default, CI) replays the committed
/// recording through both surfaces with no credentials; record runs once against real Azure.
/// </summary>
[Collection("Scenario Tests")]
public class GroupCreateScenarioTests
{
    // Playback resolves the subscription from the stub profile, which the sanitisers also wrote
    // into the recording, so both the request and the recorded id carry this placeholder.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    private const string ResourceGroupName = "scenario-test-rg";
    private const string Location = "westus2";

    [Fact]
    public void CreatesResourceGroup()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(GroupCreateScenarioTests), nameof(CreatesResourceGroup));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM. The subscription comes from the developer's signed-in profile, not this test,
            // and the sanitisers scrub it before it reaches disk, so nothing about it is asserted here.
            // The group name carries a run-scoped suffix so simultaneous record runs cannot collide;
            // the sanitisers strip it, so the committed recording keeps the base name.
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true);
            try
            {
                AssertCreated(RunCli(host, groupName), name: groupName);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor()))
        {
            AssertCreated(RunCli(cli), SubscriptionId);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor()))
        {
            AssertCreated(RunPowerShell(ps), SubscriptionId);
        }
    }

    // Subscription is omitted so it resolves from the profile: the signed-in one when recording,
    // the placeholder stub on playback.
    private static IReadOnlyList<object> RunCli(ScenarioHost host, string? name = null) =>
        host.RunCli("group", "create", "--name", name ?? ResourceGroupName, "--location", Location);

    private static IReadOnlyList<object> RunPowerShell(ScenarioHost host) =>
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

    // Asserts on the emitted object so an output regression fails the test. The id is checked only
    // when the expected subscription is known (playback); a record run's is the developer's.
    private static void AssertCreated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null, string? name = null)
    {
        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(name ?? ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
        Assert.Equal("Microsoft.Resources/resourceGroups", resourceGroup.Type);
        Assert.Equal("Succeeded", resourceGroup.Properties?.ProvisioningState);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal($"/subscriptions/{expectedSubscriptionId}/resourceGroups/{name ?? ResourceGroupName}", resourceGroup.Id);
        }
    }
}
