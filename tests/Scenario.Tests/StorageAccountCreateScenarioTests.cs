using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// End-to-end scenario for <c>az storage account create</c> / <c>New-AzStorageAccount</c>.
/// <see cref="RecordingSession"/> picks the mode from <c>AZURE_TEST_MODE</c>: playback (default,
/// CI) replays the committed recording through both surfaces with no credentials; record runs
/// once against real Azure.
/// <para>
/// The unit tests in <c>Cli.Tests.NewStorageAccountProcessorTests</c> already pin the request
/// body exhaustively by setting processor properties directly. What they cannot cover is
/// argument binding: that <c>--min-tls-version</c> and <c>-MinimumTlsVersion</c> both reach the
/// same processor property through the real dispatcher, parser and command tables. This scenario
/// exists for that, so the options below are chosen to cover one of each distinct binding shape
/// rather than to be exhaustive.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class StorageAccountCreateScenarioTests
{
    // Playback resolves the subscription from the stub profile, which the sanitisers also wrote
    // into the recording, so both the request and the recorded id carry this placeholder.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    // The scenario creates the group itself, so a record run needs nothing else in the
    // subscription. Creating a group is idempotent, so sharing the name with other scenarios is fine.
    private const string ResourceGroupName = "scenario-test-rg";

    // Storage account names are globally unique. Re-recording against an account that already
    // exists is fine: the service treats a repeat create with the same properties as success.
    private const string AccountName = "azclipsscenariotest";
    private const string Location = "westus2";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Storage];

    [Fact]
    public void CreatesStorageAccount()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageAccountCreateScenarioTests), nameof(CreatesStorageAccount));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM. The subscription comes from the developer's signed-in profile, not this
            // test, and the sanitisers scrub it before it reaches disk, so nothing about it is
            // asserted here.
            using var host = new ScenarioHost(
                ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);
            RunCliGroupCreate(host);
            AssertCreated(RunCli(host));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunCliGroupCreate(cli);
            AssertCreated(RunCli(cli), SubscriptionId);
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["Name"] = ResourceGroupName,
                ["Location"] = Location,
            }, extension: "Resources");
            AssertCreated(RunPowerShell(ps), SubscriptionId);
        }
    }

    // Subscription is omitted so it resolves from the profile: the signed-in one when recording,
    // the placeholder stub on playback. The optional arguments cover one of each binding shape:
    // a plain string, an enum constrained by AllowedValues, a tri-state boolean, a list, and an
    // option that builds a nested request object.
    private static IReadOnlyList<object> RunCliGroupCreate(ScenarioHost host) =>
        host.RunCli("group", "create", "--name", ResourceGroupName, "--location", Location);

    private static IReadOnlyList<object> RunCli(ScenarioHost host) =>
        host.RunCli("storage", "account", "create",
            "-g", ResourceGroupName,
            "-n", AccountName,
            "-l", Location,
            "--sku", "Standard_LRS",
            "--access-tier", "Cool",
            "--min-tls-version", "TLS1_2",
            "--https-only", "true",
            "--tags", "env=scenario",
            "--default-action", "Allow");

    private static IReadOnlyList<object> RunPowerShell(ScenarioHost host) =>
        host.RunPowerShell("New-AzStorageAccount", new Dictionary<string, object>
        {
            ["ResourceGroupName"] = ResourceGroupName,
            ["AccountName"] = AccountName,
            ["Location"] = Location,
            ["SkuName"] = "Standard_LRS",
            ["Kind"] = "StorageV2",
            ["AccessTier"] = "Cool",
            ["MinimumTlsVersion"] = "TLS1_2",
            ["SupportsHttpsTrafficOnly"] = true,
            ["Tags"] = new[] { "env=scenario" },
            ["DefaultAction"] = "Allow",
        }, extension: "Storage");

    // Asserts on the emitted object so an output regression fails the test. Both surfaces bind to
    // the same processor, so identical assertions holding for each is the parity check. The id is
    // checked only when the expected subscription is known (playback); a record run's is the
    // developer's.
    private static void AssertCreated(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var account = Assert.IsType<StorageAccount>(Assert.Single(outputs));
        Assert.Equal(AccountName, account.Name);
        Assert.Equal(Location, account.Location);
        Assert.Equal("Microsoft.Storage/storageAccounts", account.Type);
        Assert.Equal("StorageV2", account.Kind);
        Assert.Equal("Standard_LRS", account.Sku?.Name);

        // Derived by the service from the SKU name, so its presence proves the read-only member
        // round-trips into the response model.
        Assert.Equal("Standard", account.Sku?.Tier);

        // The long-running operation is followed to a terminal state before the account is written.
        Assert.Equal("Succeeded", account.Properties?.ProvisioningState);

        // Options that were bound from arguments come back set, which is what makes this a binding
        // test rather than a serialization one.
        Assert.Equal("Cool", account.Properties?.AccessTier);
        Assert.Equal("TLS1_2", account.Properties?.MinimumTlsVersion);
        Assert.True(account.Properties?.SupportsHttpsTrafficOnly);
        Assert.Equal("Allow", account.Properties?.NetworkAcls?.DefaultAction);
        Assert.Equal("scenario", Assert.Contains("env", account.Tags!));

        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(
                $"/subscriptions/{expectedSubscriptionId}/resourceGroups/{ResourceGroupName}"
                + $"/providers/Microsoft.Storage/storageAccounts/{AccountName}",
                account.Id);
        }
    }
}
