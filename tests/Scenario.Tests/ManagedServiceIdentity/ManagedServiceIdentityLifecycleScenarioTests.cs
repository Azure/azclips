using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the managed identity CLI lifecycle through the real dispatcher/binder and ARM
/// request pipeline. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording without credentials.
/// </summary>
[Collection("Scenario Tests")]
public class ManagedServiceIdentityLifecycleScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-identity-rg";
    private const string IdentityName = "scenario-identity";
    private const string Location = "westus2";

    [Fact]
    public void CreateListShowUpdateDeleteIdentity()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(ManagedServiceIdentityLifecycleScenarioTests), nameof(CreateListShowUpdateDeleteIdentity));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunGroupCreate(host, subscriptionId, groupName);
                AssertCreated(RunCreate(host, subscriptionId, groupName));
                AssertListed(RunList(host, subscriptionId, groupName));
                AssertShown(RunShow(host, subscriptionId, groupName));
                AssertUpdated(RunUpdate(host, subscriptionId, groupName));
                RunDelete(host, subscriptionId, groupName);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertCreated(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertListed(RunList(cli, SubscriptionId));
            AssertShown(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertUpdated(RunUpdate(cli, SubscriptionId), SubscriptionId);
            RunDelete(cli, SubscriptionId);
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", groupName ?? ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("identity", "create",
            "--subscription", subscriptionId,
            "--resource-group", groupName ?? ResourceGroupName,
            "--name", IdentityName,
            "--location", Location,
            "--tags", "env=scenario",
            "--isolation-scope", "Regional",
            "--resource-restriction", """{"providers":["Microsoft.Compute"]}""");

    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("identity", "list",
            "--subscription", subscriptionId,
            "--resource-group", groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("identity", "show",
            "--ids", IdentityId(subscriptionId, groupName));

    private static IReadOnlyList<object> RunUpdate(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("identity", "update",
            "--ids", IdentityId(subscriptionId, groupName),
            "--isolation-scope", "None",
            "--tags", "env=scenario-updated");

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("identity", "delete",
            "--ids", IdentityId(subscriptionId, groupName));

    private static string IdentityId(string subscriptionId, string? groupName = null) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{groupName ?? ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{IdentityName}";

    private static void AssertCreated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identity = Assert.IsType<UserAssignedIdentity>(Assert.Single(outputs));
        AssertIdentity(identity, "Regional", expectedSubscriptionId);
        Assert.Equal("scenario", identity.Tags?["env"]);
        Assert.Equal("Microsoft.Compute", Assert.Single(identity.Properties?.AssignmentRestrictions?.Providers ?? []));
    }

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var identities = Assert.IsType<List<UserAssignedIdentity>>(Assert.Single(outputs));
        var identity = Assert.Single(identities);
        Assert.Equal(IdentityName, identity.Name);
        Assert.Equal(Location, identity.Location);
    }

    private static void AssertShown(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(outputs));
        var identity = Assert.Single(identities);
        AssertIdentity(identity, "Regional", expectedSubscriptionId);
    }

    private static void AssertUpdated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(outputs));
        var identity = Assert.Single(identities);
        AssertIdentity(identity, "None", expectedSubscriptionId);
        Assert.Equal("scenario-updated", identity.Tags?["env"]);
    }

    private static void AssertIdentity(UserAssignedIdentity identity, string isolationScope, string? expectedSubscriptionId)
    {
        Assert.Equal(IdentityName, identity.Name);
        Assert.Equal(Location, identity.Location);
        Assert.Equal("Microsoft.ManagedIdentity/userAssignedIdentities", identity.Type);
        Assert.Equal(isolationScope, identity.Properties?.IsolationScope);
        Assert.False(string.IsNullOrWhiteSpace(identity.Properties?.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(identity.Properties?.PrincipalId));
        Assert.False(string.IsNullOrWhiteSpace(identity.Properties?.TenantId));
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(IdentityId(expectedSubscriptionId), identity.Id);
        }
    }
}
