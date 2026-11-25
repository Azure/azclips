// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using AccountsIndex = Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex;

namespace Cli.Tests.Commands.Accounts;

/// <summary>
/// Runs Accounts CLI commands through the real engine and checks the error for each rejected input. The wire mock
/// and the profile, auth, subscription and logging service mocks are strict with no setups, so each test also
/// proves no request, sign-in or profile change was made.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class AccountsCommandTests : IDisposable
{
    private const string ExtensionName = "Accounts";
    private const string SubscriptionId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string ClientId = "bbbbbbbb-0000-0000-0000-000000000002";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly Mock<IAuthService> _mockAuth = new(MockBehavior.Strict);
    private readonly Mock<ISubscriptionService> _mockSubscriptions = new(MockBehavior.Strict);
    private readonly Mock<ILoggingService> _mockLogging = new(MockBehavior.Strict);
    private readonly List<object> _output = [];

    private readonly string _noLoginParameterSetMessage = string.Join(
        Environment.NewLine,
        "The supplied parameters [(none)] do not match any parameter set.",
        "Candidate sets:",
        "  - 'ManagedIdentity': missing mandatory [--identity, --subscription-id]",
        "  - 'DeviceCode': missing mandatory [--use-device-code, --subscription-id]",
        "  - 'FederatedIdentity': missing mandatory [--service-principal, --federated-token, --client-id, --tenant-id, --subscription-id]",
        "  - 'ProfileReauth': missing mandatory [--profile]",
        "  - 'Browser': missing mandatory [--subscription-id]",
        "  - 'SPWithSecret': missing mandatory [--client-id, --client-secret, --tenant-id, --subscription-id]");

    private readonly string _profileAndOutProfileParameterSetMessage = string.Join(
        Environment.NewLine,
        "The supplied parameters [--out-profile, --profile] do not match any parameter set.",
        "Candidate sets:",
        "  - 'ManagedIdentity': missing mandatory [--identity, --subscription-id]; does not allow [--profile]",
        "  - 'DeviceCode': missing mandatory [--use-device-code, --subscription-id]; does not allow [--profile]",
        "  - 'FederatedIdentity': missing mandatory [--service-principal, --federated-token, --client-id, --tenant-id, --subscription-id]; does not allow [--profile]",
        "  - 'ProfileReauth': does not allow [--out-profile]",
        "  - 'Browser': missing mandatory [--subscription-id]; does not allow [--profile]",
        "  - 'SPWithSecret': missing mandatory [--client-id, --client-secret, --tenant-id, --subscription-id]; does not allow [--profile]");

    private readonly Engine _engine;

    public AccountsCommandTests()
    {
        // The Accounts processors resolve these services from AzclipsServiceProvider, not from the engine.
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(_mockProfiles.Object);
            services.AddSingleton(_mockAuth.Object);
            services.AddSingleton(_mockSubscriptions.Object);
            services.AddSingleton(_mockLogging.Object);
        });
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        _engine = CreateEngine(typeof(AccountsIndex).Assembly, _mockWire);
    }

    public void Dispose()
    {
        _engine.Dispose();
        AzclipsServiceProvider.Dispose();
    }

    // One case per required CLI parameter in the extension index: that parameter is left out and every
    // other required parameter is given a value, so the binder reports exactly the one that is missing.
    public static TheoryData<string[], string> GetRequiredParameters()
    {
        var data = new TheoryData<string[], string>();
        foreach (CommandMetadata command in new AccountsIndex().GetAllCommands(ClientType.CLI))
        {
            string[] requiredFlags = command.Parameters.Values.Where(p => p.IsRequired).Select(p => p.InvocationName).ToArray();
            foreach (string missing in requiredFlags)
            {
                string[] args = requiredFlags.Where(flag => flag != missing).SelectMany(flag => new[] { $"--{flag}", "value" }).ToArray();
                data.Add([.. command.InvocationName.Split(' '), .. args], $"--{missing}");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GetRequiredParameters))]
    public async Task RunAsync_MissingRequiredParameter_ThrowsMissingRequiredArgument(string[] commandLine, string missingFlag)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Invalid arguments: Required parameter '{missingFlag}' is missing.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    // ── shared CLI binder and engine checks ────────────────────────────────────────────────────
    // These messages come from the CLI binder and the engine, not Accounts.

    [Fact]
    public async Task RunAsync_UnknownParameter_ThrowsUsageError()
    {
        string unknownFlag = "--not-a-parameter";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name", "test", unknownFlag, "value"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Unable to find parameter: not-a-parameter", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_MissingProfile_ThrowsProfileNotFound()
    {
        string missingProfile = "missing";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--profile", missingProfile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Profile 'missing' does not exist.", Assert.IsType<AzclipsErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RequiredParameterWithoutValue_ThrowsUsageError()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Failed to parse parameter 'name': Parameter 'name' requires a value.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_SingleValueParameterWithTwoValues_ThrowsUsageError()
    {
        string firstName = "first";
        string secondName = "second";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name", firstName, secondName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Failed to parse parameter 'name': Parameter 'name' accepts a single value but received 2.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_NonBooleanValueForSwitch_ThrowsUsageError()
    {
        string nonBooleanActive = "maybe";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", "--active", nonBooleanActive]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Failed to parse parameter 'active': Property 'active' expects a boolean value, but received 'maybe'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_ValueNotAllowed_ThrowsUsageError()
    {
        string unsupportedResourceType = "Bogus";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", "--resource-type", unsupportedResourceType]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Invalid arguments: Parameter 'resource-type' value 'Bogus' is not allowed. Allowed values: arm, aad-graph, ms-graph, batch, "
                + "data-lake, oss-rdbms, media, key-vault, storage, synapse, managed-hsm, communication-email, attestation, app-configuration, "
                + "analysis-services, operational-insights",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    // ── login ──────────────────────────────────────────────────────────────────────────────────
    // Current behaviour: login reports some input errors as InvalidInputException (the value checks with a
    // "usage error:" prefix) and others, the subscription id and profile name checks, as UsageErrorException.

    [Theory]
    [InlineData("--subscription-id")]
    [InlineData("--tenant-id")]
    [InlineData("--client-id")]
    [InlineData("--object-id")]
    [InlineData("--resource-id")]
    [InlineData("--federated-token")]
    [InlineData("--client-secret")]
    public async Task RunAsync_LoginWhitespaceValue_ThrowsInvalidInput(string flag)
    {
        string whitespaceValue = " ";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", flag, whitespaceValue]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"usage error: '{flag}' must not be whitespace.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginInvalidOutProfileName_ThrowsUsageError()
    {
        string invalidProfileName = "bad name!";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--subscription-id", SubscriptionId, "--out-profile", invalidProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Invalid profile name 'bad name!'. Profile names must be 1-100 characters and contain only letters, digits, '-', '_', or '.'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginReservedOutProfileName_ThrowsUsageError()
    {
        string reservedProfileName = "default";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--subscription-id", SubscriptionId, "--out-profile", reservedProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The profile name 'default' is reserved for bare login and cannot be created directly.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginSubscriptionIdNotGuid_ThrowsUsageError()
    {
        string nonGuidSubscriptionId = "not-a-guid";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--subscription-id", nonGuidSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Invalid subscription id 'not-a-guid' for '--subscription-id'",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData("--client-id")]
    [InlineData("--object-id")]
    public async Task RunAsync_LoginIdentityIdNotGuid_ThrowsInvalidInput(string flag)
    {
        string nonGuidId = "abc";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--subscription-id", SubscriptionId, flag, nonGuidId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"usage error: '{flag}' must be a GUID. 'abc' is not.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginWithoutArguments_ThrowsInvalidInput()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(_noLoginParameterSetMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginProfileAndOutProfile_ThrowsInvalidInput()
    {
        string profile = "test";
        string outProfile = "other";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--profile", profile, "--out-profile", outProfile]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(_profileAndOutProfileParameterSetMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_LoginIdentityWithTwoIdentitySelectors_ThrowsInvalidInput()
    {
        string resourceId = "/subscriptions/x/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "login",
                "--identity",
                "--subscription-id", SubscriptionId,
                "--client-id", ClientId,
                "--resource-id", resourceId,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "usage error: Provide only one of '--client-id', '--object-id', '--resource-id'.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── profile show ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileShowNameAndActive_ThrowsInvalidInput()
    {
        string profileName = "other";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", "--name", profileName, "--active"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify either a profile name or --active, not both.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // ── account get-access-token ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GetAccessTokenResourceAndScope_ThrowsUsageError()
    {
        string resource = "https://vault.azure.net";
        string scope = "https://graph.microsoft.com/.default";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["account", "get-access-token", "--resource", resource, "--scope", scope]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Specify at most one of a resource, a resource type, or a scope list - these inputs are mutually exclusive.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenBlankScope_ThrowsUsageError()
    {
        string blankScope = " ";
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["account", "get-access-token", "--scope", "https://graph.microsoft.com/.default", blankScope]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Scope values must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenBlankResource_ThrowsUsageError()
    {
        string blankResource = " ";
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", "--resource", blankResource]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Resource must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }
}
