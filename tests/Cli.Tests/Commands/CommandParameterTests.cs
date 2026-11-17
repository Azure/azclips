// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands;

/// <summary>
/// Runs each listed extension's CLI commands through the real engine and checks the error the CLI binder or engine
/// gives for each rejected input, with cases generated from the extension indexes. The wire mock is strict with no
/// responses, so each test also proves no request was sent.
/// </summary>
public sealed class CommandParameterTests
{
    private const string Value = "value";

    // Add an extension here to give its commands every check in this file. Accounts is not listed: its processors
    // resolve services from the global AzclipsServiceProvider, which needs the non-parallel setup in
    // Accounts/AccountsCommandTests.cs, so its binder checks live there.
    private static readonly (string ExtensionName, IExtensionIndex Index)[] s_extensions =
    [
        ("KeyVault", new KeyVaultIndex()),
        ("Resources", new ResourcesIndex()),
        ("MsGraph", new MsGraphIndex()),
        ("ManagedServiceIdentity", new ManagedServiceIdentityIndex()),
    ];

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];

    // One case per required CLI parameter in each extension index: that parameter is left out and every
    // other required parameter is given a value, so the binder reports exactly the one that is missing.
    public static TheoryData<string, string, string[], string> GetRequiredParameters()
    {
        var data = new TheoryData<string, string, string[], string>();
        foreach ((string extension, IExtensionIndex index) in s_extensions)
        {
            foreach (CommandMetadata command in index.GetAllCommands(ClientType.CLI))
            {
                foreach (ParameterMetadata missing in command.Parameters.Values.Where(p => p.IsRequired))
                {
                    data.Add(extension, command.InvocationName, RequiredArgs(command, except: missing), $"--{missing.InvocationName}");
                }
            }
        }

        return data;
    }

    // The single-case checks below each use, per extension, the first command in index order with a suitable
    // parameter. Every required parameter except the one under test is given a value, so the binder or the engine
    // rejects the arguments before the extension sees them. An extension with no suitable parameter is skipped.

    public static TheoryData<string, string, string[]> GetUnknownParameterCases()
    {
        var data = new TheoryData<string, string, string[]>();
        foreach ((string extension, CommandMetadata command, _) in FirstPerExtension(IsRequiredString))
        {
            data.Add(extension, command.InvocationName, [.. RequiredArgs(command), "--not-a-parameter", Value]);
        }

        return data;
    }

    public static TheoryData<string, string, string[]> GetMissingProfileCases()
    {
        var data = new TheoryData<string, string, string[]>();
        foreach ((string extension, CommandMetadata command, _) in FirstPerExtension(IsRequiredString))
        {
            data.Add(extension, command.InvocationName, [.. RequiredArgs(command), "--profile", "missing"]);
        }

        return data;
    }

    public static TheoryData<string, string, string[], string> GetRequiredParameterWithoutValueCases() =>
        CasesFor(IsRequiredString, flag => [$"--{flag}"]);

    public static TheoryData<string, string, string[], string> GetSingleValueParameterWithTwoValuesCases() =>
        CasesFor(IsRequiredString, flag => [$"--{flag}", "first", "second"]);

    public static TheoryData<string, string, string[], string> GetNonBooleanValueForSwitchCases() =>
        CasesFor(p => p.ParameterType == typeof(bool), flag => [$"--{flag}", "maybe"]);

    public static TheoryData<string, string, string[], string> GetNonIntegerValueCases() =>
        CasesFor(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(int?), flag => [$"--{flag}", "ten"]);

    public static TheoryData<string, string, string[], string, string> GetValueNotAllowedCases()
    {
        var data = new TheoryData<string, string, string[], string, string>();
        foreach ((string extension, CommandMetadata command, ParameterMetadata parameter) in FirstPerExtension(p => p.AllowedValues.Length > 0))
        {
            data.Add(
                extension,
                command.InvocationName,
                [.. RequiredArgs(command, except: parameter), $"--{parameter.InvocationName}", "Bogus"],
                parameter.InvocationName,
                string.Join(", ", parameter.AllowedValues));
        }

        return data;
    }

    // ── missing required parameters ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetRequiredParameters))]
    public async Task RunAsync_MissingRequiredParameter_ThrowsMissingRequiredArgument(string extension, string command, string[] args, string missingFlag)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Invalid arguments: Required parameter '{missingFlag}' is missing.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    // ── shared CLI binder and engine checks ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetUnknownParameterCases))]
    public async Task RunAsync_UnknownParameter_ThrowsUsageError(string extension, string command, string[] args)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Unable to find parameter: not-a-parameter", Assert.IsType<UsageErrorException>(exception).Message);
    }

    // Current behaviour: extensions whose commands do not declare --profile still report a missing profile, because
    // the engine resolves it before binding (but an existing profile is rejected as an unknown parameter).
    [Theory]
    [MemberData(nameof(GetMissingProfileCases))]
    public async Task RunAsync_MissingProfile_ThrowsProfileNotFound(string extension, string command, string[] args)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Profile 'missing' does not exist.", Assert.IsType<AzclipsErrorException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetRequiredParameterWithoutValueCases))]
    public async Task RunAsync_RequiredParameterWithoutValue_ThrowsUsageError(string extension, string command, string[] args, string flag)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Failed to parse parameter '{flag}': Parameter '{flag}' requires a value.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetSingleValueParameterWithTwoValuesCases))]
    public async Task RunAsync_SingleValueParameterWithTwoValues_ThrowsUsageError(string extension, string command, string[] args, string flag)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Failed to parse parameter '{flag}': Parameter '{flag}' accepts a single value but received 2.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetNonBooleanValueForSwitchCases))]
    public async Task RunAsync_NonBooleanValueForSwitch_ThrowsUsageError(string extension, string command, string[] args, string flag)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Failed to parse parameter '{flag}': Property '{flag}' expects a boolean value, but received 'maybe'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetNonIntegerValueCases))]
    public async Task RunAsync_NonIntegerValue_ThrowsUsageError(string extension, string command, string[] args, string flag)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Failed to parse parameter '{flag}': Property '{flag}' expects an integer value, but received 'ten'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetValueNotAllowedCases))]
    public async Task RunAsync_ValueNotAllowed_ThrowsUsageError(string extension, string command, string[] args, string flag, string allowedValues)
    {
        using Engine engine = CreateEngine(extension);
        using ISession session = engine.OpenCliSession(extension, command, args, _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Invalid arguments: Parameter '{flag}' value 'Bogus' is not allowed. Allowed values: {allowedValues}",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private Engine CreateEngine(string extension) =>
        CommandTestHelpers.CreateEngine(s_extensions.Single(e => e.ExtensionName == extension).Index.GetType().Assembly, _mockWire);

    private static bool IsRequiredString(ParameterMetadata parameter) => parameter.IsRequired && parameter.ParameterType == typeof(string);

    // For each extension, the first command in index order with a parameter matching isMatch, and that parameter.
    private static IEnumerable<(string Extension, CommandMetadata Command, ParameterMetadata Parameter)> FirstPerExtension(
        Func<ParameterMetadata, bool> isMatch)
    {
        foreach ((string extension, IExtensionIndex index) in s_extensions)
        {
            (CommandMetadata Command, ParameterMetadata Parameter) match = index.GetAllCommands(ClientType.CLI)
                .SelectMany(c => c.Parameters.Values.Where(isMatch).Select(p => (c, p)))
                .FirstOrDefault();
            if (match.Command is not null)
            {
                yield return (extension, match.Command, match.Parameter);
            }
        }
    }

    // One case per extension: every required parameter except the one under test is given a value, followed by the
    // arguments for the parameter under test.
    private static TheoryData<string, string, string[], string> CasesFor(Func<ParameterMetadata, bool> isMatch, Func<string, string[]> argsFor)
    {
        var data = new TheoryData<string, string, string[], string>();
        foreach ((string extension, CommandMetadata command, ParameterMetadata parameter) in FirstPerExtension(isMatch))
        {
            data.Add(extension, command.InvocationName, [.. RequiredArgs(command, except: parameter), .. argsFor(parameter.InvocationName)], parameter.InvocationName);
        }

        return data;
    }

    // "--flag value" for every required parameter of the command except the one given.
    private static string[] RequiredArgs(CommandMetadata command, ParameterMetadata? except = null) =>
        command.Parameters.Values
            .Where(p => p.IsRequired && p != except)
            .SelectMany(p => new[] { $"--{p.InvocationName}", Value })
            .ToArray();
}
