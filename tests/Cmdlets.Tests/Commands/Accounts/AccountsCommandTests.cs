// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using AccountsIndex = Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Accounts;

/// <summary>
/// Runs Accounts cmdlets in PowerShell through the real engine and checks the error for each rejected input. The wire
/// mock and the profile, auth, subscription and logging service mocks are strict with no setups, so each test also
/// proves no request, sign-in or profile change was made.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class AccountsCommandTests : IDisposable
{
    private const string AccountSubscriptionId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string ClientId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string ParameterSetMessage = "Parameter set cannot be resolved using the specified named parameters. One or more parameters issued cannot be used together or an insufficient number of parameters were provided.";

    private readonly string _noLoginParameterSetMessage = string.Join(
        Environment.NewLine,
        "The supplied parameters [(none)] do not match any parameter set.",
        "Candidate sets:",
        "  - 'ManagedIdentity': missing mandatory [-Identity, -SubscriptionId]",
        "  - 'DeviceCode': missing mandatory [-UseDeviceCode, -SubscriptionId]",
        "  - 'FederatedIdentity': missing mandatory [-ServicePrincipal, -FederatedToken, -ClientId, -TenantId, -SubscriptionId]",
        "  - 'ProfileReauth': missing mandatory [-Profile]",
        "  - 'Browser': missing mandatory [-SubscriptionId]",
        "  - 'SPWithSecret': missing mandatory [-ClientId, -ClientSecret, -TenantId, -SubscriptionId]");

    private readonly string _profileAndOutProfileParameterSetMessage = string.Join(
        Environment.NewLine,
        "The supplied parameters [-OutProfile, -Profile] do not match any parameter set.",
        "Candidate sets:",
        "  - 'ManagedIdentity': missing mandatory [-Identity, -SubscriptionId]; does not allow [-Profile]",
        "  - 'DeviceCode': missing mandatory [-UseDeviceCode, -SubscriptionId]; does not allow [-Profile]",
        "  - 'FederatedIdentity': missing mandatory [-ServicePrincipal, -FederatedToken, -ClientId, -TenantId, -SubscriptionId]; does not allow [-Profile]",
        "  - 'ProfileReauth': does not allow [-OutProfile]",
        "  - 'Browser': missing mandatory [-SubscriptionId]; does not allow [-Profile]",
        "  - 'SPWithSecret': missing mandatory [-ClientId, -ClientSecret, -TenantId, -SubscriptionId]; does not allow [-Profile]");

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly Mock<IAuthService> _mockAuth = new(MockBehavior.Strict);
    private readonly Mock<ISubscriptionService> _mockSubscriptions = new(MockBehavior.Strict);
    private readonly Mock<ILoggingService> _mockLogging = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public AccountsCommandTests()
    {
        _engine = CreateEngine(typeof(AccountsIndex).Assembly, _mockWire);

        // The Accounts processors resolve these services from AzclipsServiceProvider, not from the engine.
        RegisterEngine(_engine, services =>
        {
            services.AddSingleton(_mockProfiles.Object);
            services.AddSingleton(_mockAuth.Object);
            services.AddSingleton(_mockSubscriptions.Object);
            services.AddSingleton(_mockLogging.Object);
        });
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── Connect-AzAccount: cmdlet checks ───────────────────────────────────────────────────────
    // ConnectAccountCmdletTests already covers -Credential without -ServicePrincipal, -Credential with -ClientId or
    // -ClientSecret, and an empty secret.

    [Fact]
    public void Invoke_ConnectAccountCredentialWithWhitespaceUserName_ThrowsPSArgumentException()
    {
        PSCredential credentialWithWhitespaceUserName = new(" ", ToSecureString("secret"));
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("ServicePrincipal").AddParameter("Credential", credentialWithWhitespaceUserName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "-Credential must contain an application ID and a nonempty client secret.",
            Assert.IsType<PSArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Connect-AzAccount: value checks ────────────────────────────────────────────────────────
    // Current behaviour: login reports some input errors as InvalidInputException (the value checks with a
    // "usage error:" prefix) and others, the subscription id and profile name checks, as UsageErrorException.

    [Theory]
    [InlineData("SubscriptionId")]
    [InlineData("TenantId")]
    [InlineData("ClientId")]
    [InlineData("ObjectId")]
    [InlineData("ResourceId")]
    [InlineData("FederatedToken")]
    public void Invoke_ConnectAccountWhitespaceValue_ThrowsInvalidInput(string parameter)
    {
        string whitespaceValue = " ";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter(parameter, whitespaceValue);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"usage error: '-{parameter}' must not be whitespace.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }













    // The cmdlet only rejects an empty SecureString, so a whitespace secret reaches the extension.
    [Fact]
    public void Invoke_ConnectAccountWhitespaceClientSecret_ThrowsInvalidInput()
    {
        string whitespaceClientSecret = " ";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("ClientSecret", ToSecureString(whitespaceClientSecret));

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "usage error: '-ClientSecret' must not be whitespace.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ConnectAccountInvalidOutProfileName_ThrowsUsageError()
    {
        string invalidOutProfile = "bad name!";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("SubscriptionId", AccountSubscriptionId).AddParameter("OutProfile", invalidOutProfile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid profile name 'bad name!'. Profile names must be 1-100 characters and contain only letters, digits, '-', '_', or '.'.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ConnectAccountReservedOutProfileName_ThrowsUsageError()
    {
        string reservedOutProfile = "default";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("SubscriptionId", AccountSubscriptionId).AddParameter("OutProfile", reservedOutProfile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The profile name 'default' is reserved for bare login and cannot be created directly.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ConnectAccountSubscriptionIdNotGuid_ThrowsUsageError()
    {
        string nonGuidSubscriptionId = "not-a-guid";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("SubscriptionId", nonGuidSubscriptionId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid subscription id 'not-a-guid' for '-SubscriptionId'",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Theory]
    [InlineData("ClientId")]
    [InlineData("ObjectId")]
    public void Invoke_ConnectAccountIdentityIdNotGuid_ThrowsInvalidInput(string parameter)
    {
        string nonGuidId = "abc";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("SubscriptionId", AccountSubscriptionId).AddParameter(parameter, nonGuidId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"usage error: '-{parameter}' must be a GUID. 'abc' is not.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }





    // ── Connect-AzAccount: parameter sets ──────────────────────────────────────────────────────
    // The login sets are resolved by the extension, not PowerShell: the cmdlet has no parameter sets.

    [Fact]
    public void Invoke_ConnectAccountWithoutArguments_ThrowsInvalidInput()
    {
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            _noLoginParameterSetMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Current behaviour: a whitespace profile is dropped, so the message lists no supplied parameters.
    [Fact]
    public void Invoke_ConnectAccountWhitespaceProfile_ThrowsInvalidInput()
    {
        string whitespaceProfile = " ";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("Profile", whitespaceProfile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            _noLoginParameterSetMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // The engine rejects a profile that does not exist before login runs, so this uses the profile the test engine
    // signs in as.
    [Fact]
    public void Invoke_ConnectAccountProfileAndOutProfile_ThrowsInvalidInput()
    {
        string outProfile = "other";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("Profile", ProfileName).AddParameter("OutProfile", outProfile);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            _profileAndOutProfileParameterSetMessage,
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_ConnectAccountIdentityWithTwoIdentitySelectors_ThrowsInvalidInput()
    {
        string resourceId = "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u";
        using PowerShell shell = CreateShell<ConnectAccountCmdlet>();
        shell.AddParameter("Identity")
            .AddParameter("SubscriptionId", AccountSubscriptionId)
            .AddParameter("ClientId", ClientId)
            .AddParameter("ResourceId", resourceId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "usage error: Provide only one of '-ClientId', '-ObjectId', '-ResourceId'.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // ── Get-AzProfile ──────────────────────────────────────────────────────────────────────────

    // Name and Active are in different parameter sets, so PowerShell rejects both together before the extension's
    // "not both" check.
    [Fact]
    public void Invoke_GetProfileNameAndActive_ThrowsParameterBindingException()
    {
        string profileName = "p";
        using PowerShell shell = CreateShell<GetProfileCmdlet>();
        shell.AddParameter("Name", profileName).AddParameter("Active");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // ── Get-AzAccessToken ──────────────────────────────────────────────────────────────────────

    // ResourceUrl and Scope are in different parameter sets, so PowerShell rejects both together before the extension's
    // "at most one" check.
    [Fact]
    public void Invoke_GetAccessTokenResourceUrlAndScope_ThrowsParameterBindingException()
    {
        string[] scope = ["https://graph.microsoft.com/.default"];
        using PowerShell shell = CreateShell<GetAccessTokenCmdlet>();
        shell.AddParameter("ResourceUrl", "https://vault.azure.net").AddParameter("Scope", scope);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }

    [Fact]
    public void Invoke_GetAccessTokenBlankScope_ThrowsUsageError()
    {
        string[] scopesWithBlank = ["https://graph.microsoft.com/.default", " "];
        using PowerShell shell = CreateShell<GetAccessTokenCmdlet>();
        shell.AddParameter("Scope", scopesWithBlank);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Scope values must not be empty.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetAccessTokenWhitespaceResourceUrl_ThrowsUsageError()
    {
        string whitespaceResourceUrl = " ";
        using PowerShell shell = CreateShell<GetAccessTokenCmdlet>();
        shell.AddParameter("ResourceUrl", whitespaceResourceUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Resource must not be empty.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // OssRdbms resolves on the CLI (oss-rdbms) but has no PowerShell spelling, so [ValidateSet] rejects it while binding.
    // The binder throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_GetAccessTokenResourceTypeNameNotInSet_ThrowsParameterBindingException()
    {
        string cliOnlyResourceType = "OssRdbms";
        using PowerShell shell = CreateShell<GetAccessTokenCmdlet>();
        shell.AddParameter("ResourceTypeName", cliOnlyResourceType);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'ResourceTypeName'. The argument \"OssRdbms\" does not belong to the set \"Arm,ResourceManager,AadGraph,MSGraph,Batch,DataLake,KeyVault,Storage,Synapse,ManagedHsm,CommunicationEmail,Attestation,AppConfiguration,AnalysisServices,OperationalInsights\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }
}
