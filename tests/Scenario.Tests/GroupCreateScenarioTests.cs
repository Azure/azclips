using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The authoring pattern scenario tests follow: one committed recording at
/// <c>SessionRecords/&lt;TestClass&gt;/&lt;TestName&gt;.json</c>, replayed through both client
/// surfaces with no network and no credentials. The recording checked in here is synthetic
/// (captured from the fake ARM backend); the live POC replaces it with a real one-time
/// recording without touching this test.
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
        var recordPath = SessionRecordFiles.GetPath(typeof(GroupCreateScenarioTests), nameof(CreatesResourceGroup));
        var record = SessionRecord.Load(recordPath);

        // CLI surface.
        using (var cli = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Playback, record)))
        {
            var outputs = cli.RunCli("group", "create",
                "--name", ResourceGroupName,
                "--location", Location,
                "--subscription-id", SubscriptionId);
            AssertCreated(outputs);
        }

        // PowerShell surface, same recording — the author-once model.
        using (var ps = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Playback, record)))
        {
            var outputs = ps.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["Name"] = ResourceGroupName,
                ["Location"] = Location,
            });
            AssertCreated(outputs);
        }
    }

    private static void AssertCreated(IReadOnlyList<object> outputs)
    {
        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }
}
