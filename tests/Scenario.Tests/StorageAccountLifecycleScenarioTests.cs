using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The storage account lifecycle: create, show, list, delete, driven through the real
/// dispatcher/binder and ARM request pipeline. Record mode runs the CLI surface against live Azure
/// and rewrites the recording; playback replays the committed recording on both surfaces without
/// credentials.
/// <para>
/// The read commands are the reason this exists alongside the create scenario: show and list are
/// only meaningful against an account that exists, and list additionally proves the customization
/// picks the right one of the two API operations, since the resource-group form and the
/// subscription form are different requests.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class StorageAccountLifecycleScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-storage-lifecycle-rg";

    // Storage account names are globally unique, so this must differ from the one the create
    // scenario uses. The lifecycle deletes the account at the end, which keeps a re-record from
    // colliding with the previous run.
    private const string AccountName = "azclipslifecycletest";
    private const string Location = "westus2";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Storage];

    [Fact]
    public void CreateShowListDeleteStorageAccount()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(StorageAccountLifecycleScenarioTests),
            nameof(CreateShowListDeleteStorageAccount),
            // The subscription-wide list returns every account in the recording subscription; keep
            // only this scenario's group so nobody else's accounts reach the committed recording.
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(ResourceGroupName)]));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertAccount(RunCreate(host, subscriptionId));
            AssertAccount(RunShow(host, subscriptionId));
            AssertListContainsAccount(RunListByResourceGroup(host, subscriptionId));
            AssertListContainsAccount(RunListBySubscription(host, subscriptionId));
            AssertDeleted(RunDelete(host, subscriptionId));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertAccount(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertAccount(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertListContainsAccount(RunListByResourceGroup(cli, SubscriptionId));
            AssertListContainsAccount(RunListBySubscription(cli, SubscriptionId));
            AssertDeleted(RunDelete(cli, SubscriptionId));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertAccount(ps.RunPowerShell("New-AzStorageAccount", PsCreateParameters(SubscriptionId), extension: "Storage"), SubscriptionId);

            // Get-AzStorageAccount resolves its parameter set through the command name, so the two
            // forms are separate invocations here: the account-name set is show, and the
            // resource-group set is list.
            AssertAccount(ps.RunPowerShell("Get-AzStorageAccount_AccountName", PsAccountParameters(SubscriptionId), extension: "Storage"), SubscriptionId);
            AssertListContainsAccount(ps.RunPowerShell("Get-AzStorageAccount_ResourceGroup", PsGroupScopeParameters(SubscriptionId), extension: "Storage"));

            // Omitting the resource group takes the subscription-wide operation, which is the
            // branch the list customization exists for.
            AssertListContainsAccount(ps.RunPowerShell("Get-AzStorageAccount_ResourceGroup", new Dictionary<string, object> { ["SubscriptionId"] = SubscriptionId }, extension: "Storage"));

            AssertDeleted(ps.RunPowerShell("Remove-AzStorageAccount", PsAccountParameters(SubscriptionId), extension: "Storage"));
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("storage", "account", "create",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", AccountName,
            "--location", Location,
            "--sku", "Standard_LRS",
            "--kind", "StorageV2");

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId) =>
        host.RunCli("storage", "account", "show",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", AccountName);

    private static IReadOnlyList<object> RunListByResourceGroup(ScenarioHost host, string subscriptionId) =>
        host.RunCli("storage", "account", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunListBySubscription(ScenarioHost host, string subscriptionId) =>
        host.RunCli("storage", "account", "list",
            "--subscription-id", subscriptionId);

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId) =>
        host.RunCli("storage", "account", "delete",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", AccountName);

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["AccountName"] = AccountName,
        ["Location"] = Location,
        ["SkuName"] = "Standard_LRS",
        ["Kind"] = "StorageV2",
    };

    private static Dictionary<string, object> PsAccountParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = AccountName,
    };

    private static Dictionary<string, object> PsGroupScopeParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static string AccountId(string subscriptionId) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}";

    private static void AssertAccount(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var account = Assert.IsType<StorageAccount>(Assert.Single(outputs));
        Assert.Equal(AccountName, account.Name);
        Assert.Equal(Location, account.Location);
        Assert.Equal("Succeeded", account.Properties!.ProvisioningState);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(AccountId(expectedSubscriptionId), account.Id);
        }
    }

    /// <summary>
    /// Both list forms write one flat collection, so the account created above has to appear in it.
    /// The subscription-wide form returns unrelated accounts too, hence a containment check rather
    /// than an exact match.
    /// </summary>
    private static void AssertListContainsAccount(IReadOnlyList<object> outputs)
    {
        var accounts = outputs.OfType<StorageAccount>().ToList();
        if (accounts.Count == 0 && outputs.Count == 1 && outputs[0] is IEnumerable<StorageAccount> batch)
        {
            accounts = batch.ToList();
        }

        Assert.Contains(accounts, a => a.Name == AccountName);
    }

    /// <summary>
    /// delete has no response body, so the assertion is that the command completed and returned no
    /// account rather than an exact output count.
    /// </summary>
    private static void AssertDeleted(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is StorageAccount);
}
