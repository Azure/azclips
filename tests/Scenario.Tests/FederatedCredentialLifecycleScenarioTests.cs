using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the federated identity credential CLI lifecycle through the real dispatcher/binder
/// and ARM request pipeline. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording without credentials.
/// </summary>
[Collection("Scenario Tests")]
public class FederatedCredentialLifecycleScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-fic-rg";
    private const string IdentityName = "scenario-fic-identity";
    private const string CredentialName = "scenario-fic";
    private const string Location = "westus2";
    private const string Issuer = "https://token.actions.githubusercontent.com";
    private const string Subject = "repo:contoso/example:ref:refs/heads/main";

    [Fact]
    public void CreateListShowUpdateDeleteFederatedCredential()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(FederatedCredentialLifecycleScenarioTests), nameof(CreateListShowUpdateDeleteFederatedCredential));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            RunIdentityCreate(host, subscriptionId);
            AssertCreated(RunCreate(host, subscriptionId));
            AssertListed(RunList(host, subscriptionId));
            AssertShown(RunShow(host, subscriptionId), subscriptionId);
            AssertUpdated(RunUpdate(host, subscriptionId), subscriptionId);
            AssertGenericUpdated(RunGenericUpdate(host, subscriptionId));
            RunDelete(host, subscriptionId);
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]))
        {
            RunGroupCreate(cli, SubscriptionId);
            RunIdentityCreate(cli, SubscriptionId);
            AssertCreated(RunCreate(cli, SubscriptionId));
            AssertListed(RunList(cli, SubscriptionId));
            AssertShown(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertUpdated(RunUpdate(cli, SubscriptionId), SubscriptionId);
            AssertGenericUpdated(RunGenericUpdate(cli, SubscriptionId));
            RunDelete(cli, SubscriptionId);
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            ps.RunPowerShell("New-AzUserAssignedIdentity", PsIdentityParameters(SubscriptionId), extension: "ManagedServiceIdentity");
            AssertCreated(ps.RunPowerShell("New-AzFederatedIdentityCredential", PsCreateParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            AssertListed(ps.RunPowerShell("Get-AzFederatedIdentityCredential_List", PsListParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            AssertShown(ps.RunPowerShell("Get-AzFederatedIdentityCredential_Get", PsShowParameters(SubscriptionId), extension: "ManagedServiceIdentity"), SubscriptionId);
            AssertUpdated(ps.RunPowerShell("Update-AzFederatedIdentityCredential", PsUpdateParameters(SubscriptionId), extension: "ManagedServiceIdentity"), SubscriptionId);
            AssertGenericUpdated(ps.RunPowerShell("Update-AzFederatedIdentityCredential", PsGenericUpdateParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            ps.RunPowerShell("Remove-AzFederatedIdentityCredential", PsShowParameters(SubscriptionId), extension: "ManagedServiceIdentity");
        }
    }

    // --set/--add/--remove never touch the wire (BeforeProcessAsync throws first), so these run
    // without any recording, live credentials, or network access.
    [Theory]
    [InlineData(1, null)]
    [InlineData(null, "claims['sub'] eq 'foo'")]
    public void UpdateRejectsClaimsMatchingExpressionVersionOrValueAlone(int? version, string? value)
    {
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            extensions: [ExtensionUnderTest.ManagedServiceIdentity]);

        List<string> args =
        [
            "identity", "federated-credential", "update",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName,
        ];
        if (version is not null) args.AddRange(["--claims-matching-expression-version", version.Value.ToString()]);
        if (value is not null) args.AddRange(["--claims-matching-expression-value", value]);

        var error = Assert.ThrowsAny<Exception>(() => host.RunCli([.. args]));
        Assert.Contains("must be specified together", error.ToString());
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, "claims['sub'] eq 'foo'")]
    public void CreateRejectsClaimsMatchingExpressionVersionOrValueAlone(int? version, string? value)
    {
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            extensions: [ExtensionUnderTest.ManagedServiceIdentity]);

        List<string> args =
        [
            "identity", "federated-credential", "create",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName,
            "--issuer", Issuer,
        ];
        if (version is not null) args.AddRange(["--claims-matching-expression-version", version.Value.ToString()]);
        if (value is not null) args.AddRange(["--claims-matching-expression-value", value]);

        var error = Assert.ThrowsAny<Exception>(() => host.RunCli([.. args]));
        Assert.Contains("must be specified together", error.ToString());
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunIdentityCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "create",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", IdentityName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "create",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName,
            "--issuer", Issuer,
            "--subject", Subject);

    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "list",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName);

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "show",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName);

    private static IReadOnlyList<object> RunUpdate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "update",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName,
            "--audiences", "api://CustomAudience");

    // Exercises --set, --add, and --remove together. (claimsMatchingExpression itself needs a
    // subscription enrolled in that ARM preview feature, confirmed via live 400s here — "Expression
    // is not supported for applications in this cloud 'Public'" regardless of issuer — so --remove
    // targets that still-absent property instead, verifying removal of a path that was never set is
    // a safe no-op rather than exercising array/index removal, which ApplyGenericUpdate doesn't
    // support anyway. set/add/remove always apply in that fixed order regardless of CLI arg order.)
    private static IReadOnlyList<object> RunGenericUpdate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "update",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName,
            "--set", "properties.issuer=https://kubernetes-oauth-2.azure.com",
            "--add", "properties", "subject=system:serviceaccount:ns:regenerated-svcaccount",
            "--remove", "properties.claimsMatchingExpression");

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "delete",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", CredentialName);

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsIdentityParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = IdentityName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = CredentialName,
        ["Issuer"] = Issuer,
        ["Subject"] = Subject,
    };

    private static Dictionary<string, object> PsListParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
    };

    private static Dictionary<string, object> PsShowParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = CredentialName,
    };

    private static Dictionary<string, object> PsUpdateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = CredentialName,
        ["Audiences"] = new List<string> { "api://CustomAudience" },
    };

    // Same fixed set/add/remove order and still-absent-property target as the CLI generic update
    // above; see that comment for why.
    private static Dictionary<string, object> PsGenericUpdateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = CredentialName,
        ["Set"] = new[] { "properties.issuer=https://kubernetes-oauth-2.azure.com" },
        ["Add"] = new[] { "properties", "subject=system:serviceaccount:ns:regenerated-svcaccount" },
        ["Remove"] = new[] { "properties.claimsMatchingExpression" },
    };

    private static string CredentialId(string subscriptionId) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{IdentityName}/federatedIdentityCredentials/{CredentialName}";

    private static void AssertCreated(IReadOnlyList<object> outputs)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        AssertCredential(credential, ["api://AzureADTokenExchange"]);
    }

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var credentials = Assert.IsType<List<FederatedIdentityCredential>>(Assert.Single(outputs));
        var credential = Assert.Single(credentials);
        Assert.Equal(CredentialName, credential.Name);
    }

    private static void AssertShown(IReadOnlyList<object> outputs, string subscriptionId)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        AssertCredential(credential, ["api://AzureADTokenExchange"], subscriptionId);
    }

    private static void AssertUpdated(IReadOnlyList<object> outputs, string subscriptionId)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        AssertCredential(credential, ["api://CustomAudience"], subscriptionId);
    }

    private static void AssertGenericUpdated(IReadOnlyList<object> outputs)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        Assert.Equal(CredentialName, credential.Name);
        Assert.Equal("https://kubernetes-oauth-2.azure.com", credential.Properties?.Issuer);
        Assert.Equal(["api://CustomAudience"], credential.Properties?.Audiences);
        Assert.Equal("system:serviceaccount:ns:regenerated-svcaccount", credential.Properties?.Subject);
        Assert.Null(credential.Properties?.ClaimsMatchingExpression);
    }

    private static void AssertCredential(FederatedIdentityCredential credential, string[] expectedAudiences, string? expectedSubscriptionId = null)
    {
        Assert.Equal(CredentialName, credential.Name);
        Assert.Equal("Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials", credential.Type);
        Assert.Equal(Issuer, credential.Properties?.Issuer);
        Assert.Equal(Subject, credential.Properties?.Subject);
        Assert.Equal(expectedAudiences, credential.Properties?.Audiences);
        if (expectedSubscriptionId is not null)
        {
            // ARM casing of the "resourcegroups"/"resourceGroups" id segment differs across
            // environments/API versions; compare case-insensitively since it's the same resource.
            Assert.Equal(CredentialId(expectedSubscriptionId), credential.Id, ignoreCase: true);
        }
    }
}
