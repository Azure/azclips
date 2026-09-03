using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Core.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the managed identity CLI lifecycle through the real dispatcher/binder and ARM
/// request pipeline. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording without credentials.
/// </summary>
[Collection("Scenario Tests")]
public class IdentityLifecycleScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-identity-rg";
    private const string IdentityName = "scenario-identity";
    private const string Location = "westus2";

    [Fact]
    public void CreateListShowUpdateDeleteIdentity()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(IdentityLifecycleScenarioTests), nameof(CreateListShowUpdateDeleteIdentity));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Identity]);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertCreated(RunCreate(host, subscriptionId));
            AssertListed(RunList(host, subscriptionId));
            AssertShown(RunShow(host, subscriptionId));
            AssertUpdated(RunUpdate(host, subscriptionId));
            RunDelete(host, subscriptionId);
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Identity]))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertCreated(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertListed(RunList(cli, SubscriptionId));
            AssertShown(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertUpdated(RunUpdate(cli, SubscriptionId), SubscriptionId);
            RunDelete(cli, SubscriptionId);
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "create",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", IdentityName,
            "--location", Location,
            "--tags", "env=scenario",
            "--isolation-scope", "Regional",
            "--resource-restriction", """{"providers":["Microsoft.Compute"]}""");

    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "list",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "show",
            "--ids", IdentityId(subscriptionId));

    private static IReadOnlyList<object> RunUpdate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "update",
            "--ids", IdentityId(subscriptionId),
            "--isolation-scope", "None",
            "--tags", "env=scenario-updated");

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "delete",
            "--ids", IdentityId(subscriptionId));

    private static string IdentityId(string subscriptionId) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{IdentityName}";

    private static void AssertCreated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identity = Assert.IsType<UserAssignedIdentity>(Assert.Single(outputs));
        AssertIdentity(identity, "Regional", expectedSubscriptionId);
        Assert.Equal("scenario", identity.Tags?["env"]);
        Assert.Equal("Microsoft.Compute", Assert.Single(identity.Properties?.AssignmentRestrictions?.Providers ?? []));
    }

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(outputs));
        var identity = Assert.Single(identities);
        Assert.Equal(IdentityName, identity.Name);
        Assert.Equal(Location, identity.Location);
    }

    private static void AssertShown(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identity = Assert.IsType<UserAssignedIdentity>(Assert.Single(outputs));
        AssertIdentity(identity, "Regional", expectedSubscriptionId);
    }

    private static void AssertUpdated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var identity = Assert.IsType<UserAssignedIdentity>(Assert.Single(outputs));
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
