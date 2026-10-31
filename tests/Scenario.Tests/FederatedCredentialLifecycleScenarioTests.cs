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
    private const string SubjectCredentialName = "scenario-fic-subject";
    private const string ExpressionCredentialName = "scenario-fic-expression";
    private const string Location = "westus2";
    private const string Issuer = "https://token.actions.githubusercontent.com";
    private const string Subject = "repo:contoso/example:ref:refs/heads/main";
    private const int ClaimsMatchingExpressionVersion = 1;
    private const string ClaimsMatchingExpressionValue =
        "claims['sub'] matches 'repo:contoso/example:ref:refs/heads/*' and claims['repository_id'] eq '456789'";

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
            AssertSubjectCredential(RunCreateSubject(host, subscriptionId));
            AssertExpressionCredential(RunCreateExpression(host, subscriptionId));
            AssertListed(RunList(host, subscriptionId));
            AssertSubjectCredential(RunShow(host, subscriptionId, SubjectCredentialName), subscriptionId);
            AssertExpressionCredential(RunShow(host, subscriptionId, ExpressionCredentialName), subscriptionId);
            AssertSubjectCredential(RunUpdate(host, subscriptionId, SubjectCredentialName), subscriptionId, ["api://CustomAudience"]);
            AssertExpressionCredential(RunUpdate(host, subscriptionId, ExpressionCredentialName), subscriptionId, ["api://CustomAudience"]);
            AssertGenericUpdated(RunGenericUpdate(host, subscriptionId));
            RunDelete(host, subscriptionId, SubjectCredentialName);
            RunDelete(host, subscriptionId, ExpressionCredentialName);
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
            AssertSubjectCredential(RunCreateSubject(cli, SubscriptionId));
            AssertExpressionCredential(RunCreateExpression(cli, SubscriptionId));
            AssertListed(RunList(cli, SubscriptionId));
            AssertSubjectCredential(RunShow(cli, SubscriptionId, SubjectCredentialName), SubscriptionId);
            AssertExpressionCredential(RunShow(cli, SubscriptionId, ExpressionCredentialName), SubscriptionId);
            AssertSubjectCredential(RunUpdate(cli, SubscriptionId, SubjectCredentialName), SubscriptionId, ["api://CustomAudience"]);
            AssertExpressionCredential(RunUpdate(cli, SubscriptionId, ExpressionCredentialName), SubscriptionId, ["api://CustomAudience"]);
            AssertGenericUpdated(RunGenericUpdate(cli, SubscriptionId));
            RunDelete(cli, SubscriptionId, SubjectCredentialName);
            RunDelete(cli, SubscriptionId, ExpressionCredentialName);
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ManagedServiceIdentity]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            ps.RunPowerShell("New-AzUserAssignedIdentity", PsIdentityParameters(SubscriptionId), extension: "ManagedServiceIdentity");
            AssertSubjectCredential(ps.RunPowerShell("New-AzFederatedIdentityCredential", PsCreateSubjectParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            AssertExpressionCredential(ps.RunPowerShell("New-AzFederatedIdentityCredential", PsCreateExpressionParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            AssertListed(ps.RunPowerShell("Get-AzFederatedIdentityCredential_List", PsListParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            AssertSubjectCredential(ps.RunPowerShell("Get-AzFederatedIdentityCredential_Get", PsNamedParameters(SubscriptionId, SubjectCredentialName), extension: "ManagedServiceIdentity"), SubscriptionId);
            AssertExpressionCredential(ps.RunPowerShell("Get-AzFederatedIdentityCredential_Get", PsNamedParameters(SubscriptionId, ExpressionCredentialName), extension: "ManagedServiceIdentity"), SubscriptionId);
            AssertSubjectCredential(ps.RunPowerShell("Update-AzFederatedIdentityCredential", PsUpdateParameters(SubscriptionId, SubjectCredentialName), extension: "ManagedServiceIdentity"), SubscriptionId, ["api://CustomAudience"]);
            AssertExpressionCredential(ps.RunPowerShell("Update-AzFederatedIdentityCredential", PsUpdateParameters(SubscriptionId, ExpressionCredentialName), extension: "ManagedServiceIdentity"), SubscriptionId, ["api://CustomAudience"]);
            AssertGenericUpdated(ps.RunPowerShell("Update-AzFederatedIdentityCredential", PsGenericUpdateParameters(SubscriptionId), extension: "ManagedServiceIdentity"));
            ps.RunPowerShell("Remove-AzFederatedIdentityCredential", PsNamedParameters(SubscriptionId, SubjectCredentialName), extension: "ManagedServiceIdentity");
            ps.RunPowerShell("Remove-AzFederatedIdentityCredential", PsNamedParameters(SubscriptionId, ExpressionCredentialName), extension: "ManagedServiceIdentity");
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
            "--name", ExpressionCredentialName,
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
            "--name", ExpressionCredentialName,
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

    private static IReadOnlyList<object> RunCreateSubject(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "create",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", SubjectCredentialName,
            "--issuer", Issuer,
            "--subject", Subject);

    private static IReadOnlyList<object> RunCreateExpression(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "create",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", ExpressionCredentialName,
            "--issuer", Issuer,
            "--claims-matching-expression-version", ClaimsMatchingExpressionVersion.ToString(),
            "--claims-matching-expression-value", ClaimsMatchingExpressionValue);

    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "list",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName);

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId, string credentialName) =>
        host.RunCli("identity", "federated-credential", "show",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", credentialName);

    private static IReadOnlyList<object> RunUpdate(ScenarioHost host, string subscriptionId, string credentialName) =>
        host.RunCli("identity", "federated-credential", "update",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", credentialName,
            "--audiences", "api://CustomAudience");

    // Exercises --set/--add/--remove together. Removing an absent expression is a safe no-op.
    private static IReadOnlyList<object> RunGenericUpdate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("identity", "federated-credential", "update",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", SubjectCredentialName,
            "--set", "properties.issuer=https://kubernetes-oauth-2.azure.com",
            "--add", "properties", "subject=system:serviceaccount:ns:regenerated-svcaccount",
            "--remove", "properties.claimsMatchingExpression");

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId, string credentialName) =>
        host.RunCli("identity", "federated-credential", "delete",
            "--subscription", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--identity-name", IdentityName,
            "--name", credentialName);

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

    private static Dictionary<string, object> PsCreateSubjectParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = SubjectCredentialName,
        ["Issuer"] = Issuer,
        ["Subject"] = Subject,
    };

    private static Dictionary<string, object> PsCreateExpressionParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = ExpressionCredentialName,
        ["Issuer"] = Issuer,
        ["ClaimsMatchingExpressionVersion"] = ClaimsMatchingExpressionVersion,
        ["ClaimsMatchingExpressionValue"] = ClaimsMatchingExpressionValue,
    };

    private static Dictionary<string, object> PsListParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
    };

    private static Dictionary<string, object> PsNamedParameters(string subscriptionId, string credentialName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = credentialName,
    };

    private static Dictionary<string, object> PsUpdateParameters(string subscriptionId, string credentialName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = credentialName,
        ["Audiences"] = new List<string> { "api://CustomAudience" },
    };

    // Same fixed set/add/remove order and absent-expression no-op as the CLI update.
    private static Dictionary<string, object> PsGenericUpdateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["IdentityName"] = IdentityName,
        ["Name"] = SubjectCredentialName,
        ["Set"] = new[] { "properties.issuer=https://kubernetes-oauth-2.azure.com" },
        ["Add"] = new[] { "properties", "subject=system:serviceaccount:ns:regenerated-svcaccount" },
        ["Remove"] = new[] { "properties.claimsMatchingExpression" },
    };

    private static string CredentialId(string subscriptionId, string credentialName) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{IdentityName}/federatedIdentityCredentials/{credentialName}";

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var credentials = Assert.IsType<List<FederatedIdentityCredential>>(Assert.Single(outputs));
        Assert.Equal(2, credentials.Count);
        Assert.Contains(credentials, credential => credential.Name == SubjectCredentialName);
        Assert.Contains(credentials, credential => credential.Name == ExpressionCredentialName);
    }

    private static void AssertSubjectCredential(
        IReadOnlyList<object> outputs,
        string? subscriptionId = null,
        string[]? expectedAudiences = null)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        AssertCredentialBase(
            credential,
            SubjectCredentialName,
            expectedAudiences ?? ["api://AzureADTokenExchange"],
            subscriptionId);
        Assert.Equal(Subject, credential.Properties?.Subject);
        Assert.Null(credential.Properties?.ClaimsMatchingExpression);
    }

    private static void AssertExpressionCredential(
        IReadOnlyList<object> outputs,
        string? subscriptionId = null,
        string[]? expectedAudiences = null)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        AssertCredentialBase(
            credential,
            ExpressionCredentialName,
            expectedAudiences ?? ["api://AzureADTokenExchange"],
            subscriptionId);
        Assert.Null(credential.Properties?.Subject);
        Assert.Equal(ClaimsMatchingExpressionVersion, credential.Properties?.ClaimsMatchingExpression?.LanguageVersion);
        Assert.Equal(ClaimsMatchingExpressionValue, credential.Properties?.ClaimsMatchingExpression?.Value);
    }

    private static void AssertGenericUpdated(IReadOnlyList<object> outputs)
    {
        var credential = Assert.IsType<FederatedIdentityCredential>(Assert.Single(outputs));
        Assert.Equal(SubjectCredentialName, credential.Name);
        Assert.Equal("https://kubernetes-oauth-2.azure.com", credential.Properties?.Issuer);
        Assert.Equal(["api://CustomAudience"], credential.Properties?.Audiences);
        Assert.Equal("system:serviceaccount:ns:regenerated-svcaccount", credential.Properties?.Subject);
        Assert.Null(credential.Properties?.ClaimsMatchingExpression);
    }

    private static void AssertCredentialBase(
        FederatedIdentityCredential credential,
        string credentialName,
        string[] expectedAudiences,
        string? expectedSubscriptionId)
    {
        Assert.Equal(credentialName, credential.Name);
        Assert.Equal("Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials", credential.Type);
        Assert.Equal(Issuer, credential.Properties?.Issuer);
        Assert.Equal(expectedAudiences, credential.Properties?.Audiences);
        if (expectedSubscriptionId is not null)
        {
            // ARM casing of the "resourcegroups"/"resourceGroups" id segment differs across
            // environments/API versions; compare case-insensitively since it's the same resource.
            Assert.Equal(CredentialId(expectedSubscriptionId, credentialName), credential.Id, ignoreCase: true);
        }
    }
}
