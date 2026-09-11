// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class ExtensionHelpManifestBuilderTests
{
    [Fact]
    public void BuildManifest_ShouldProduceLoginCommands_WithInlineExamples()
    {
        var index = new ExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(
            extensionIndex: index,
            extensionVersion: "1.0.0-alpha",
            verb: "login");

        Assert.Equal("login", manifest.Verb);
        Assert.Equal("1.0.0-alpha", manifest.Version);
        Assert.Single(manifest.Commands);

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.Equal("Connect-AzAccount", loginCommand.PsName);
        Assert.NotEmpty(loginCommand.Summary);
        Assert.NotEmpty(loginCommand.Examples.Cli);

        Assert.Contains(loginCommand.Parameters, p => p.Name == "subscription-id" && p.PsName == "SubscriptionId");
        Assert.Contains(loginCommand.Parameters, p => p.Name == "tenant-id" && p.PsName == "TenantId");
        Assert.Contains(loginCommand.Parameters, p => p.Name == "use-device-code" && p.PsName == "UseDeviceCode");
    }

    [Fact]
    public void BuildIndex_ShouldIncludeGroupAndCommands_FromManifest()
    {
        var index = new ExtensionIndex();

        // login and logout are peer top-level verbs; each yields its own manifest.
        var loginManifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0-alpha", "login");
        var logoutManifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0-alpha", "logout");
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([loginManifest, logoutManifest]);

        Assert.Equal(2, helpIndex.Groups.Count);
        Assert.Contains(helpIndex.Groups, g => g.Verb == "login");
        Assert.Contains(helpIndex.Groups, g => g.Verb == "logout");

        Assert.Equal(2, helpIndex.Commands.Count);
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login" && c.PsName == "Connect-AzAccount");
        Assert.Contains(helpIndex.Commands, c => c.CliName == "logout" && c.PsName == "Disconnect-AzAccount");
    }

    [Fact]
    public void BuildManifest_ShouldFallback_WhenNoMarkdownExists()
    {
        // Use a verb that does NOT have a help/ folder, forcing fallback to attributes/defaults.
        var index = new StubExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0", "stub");

        Assert.NotNull(manifest);
        Assert.Single(manifest.Commands);

        var command = manifest.Commands[0];
        // No markdown → Summary/Description should fall back to humanized command name (no exception thrown).
        Assert.False(string.IsNullOrEmpty(command.Summary));
        Assert.False(string.IsNullOrEmpty(command.Description));
        // No markdown examples → Cli/PowerShell example lists should be empty (graceful fallback).
        Assert.Empty(command.Examples.Cli);
        Assert.Empty(command.Examples.PowerShell);
    }

    [Fact]
    public void BuildManifest_ShouldHandleParametersWithEmptyDescriptions()
    {
        var index = new StubExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0", "stub");
        var command = manifest.Commands[0];

        // Parameters with empty descriptions should not throw; description should pass through as empty.
        var emptyDescParam = Assert.Single(command.Parameters, p => p.Name == "no-desc");
        Assert.Equal(string.Empty, emptyDescParam.Description);
    }

    [Fact]
    public void BuildManifest_ShouldPreserveParameterAliases()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0", "login");

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.All(loginCommand.Parameters, p => Assert.NotNull(p.Aliases));
    }

    [Fact]
    public void BuildManifest_ShouldHandleCommandsWithNoParameters()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0", "logout");

        // logout has no parameters
        var logoutCommand = Assert.Single(manifest.Commands, c => c.CliName == "logout");
        Assert.Empty(logoutCommand.Parameters);
    }

    [Fact]
    public void BuildManifest_ShouldSplitExamples_ByLanguageTag()
    {
        // Accounts extension has both CLI (bash) and PowerShell examples in its markdown.
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "1.0.0", "login");

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");

        // CLI examples should not contain PowerShell-style commands.
        Assert.NotEmpty(loginCommand.Examples.Cli);
        Assert.All(loginCommand.Examples.Cli, ex => Assert.DoesNotContain("Connect-AzAccount", ex.Command));

        // PowerShell examples (if present) should reference Connect-AzAccount.
        if (loginCommand.Examples.PowerShell.Count > 0)
        {
            Assert.Contains(loginCommand.Examples.PowerShell, ex => ex.Command.Contains("Connect-AzAccount"));
        }
    }

    /// <summary>
    /// Stub IExtensionIndex with a single command and no markdown help backing.
    /// Exercises fallback paths in <see cref="ExtensionHelpManifestBuilder"/>.
    /// Note: builder reads static `s_cliTable`/`s_psTable` fields via reflection, so this stub exposes both.
    /// </summary>
    private sealed class StubExtensionIndex : IExtensionIndex
    {
        private static readonly CommandMetadata s_stubCommand = new(
            Name: "StubCommand",
            InvocationName: "stub",
            Aliases: [],
            FullName: "Stub.StubCommand",
            AssemblyName: "Stub",
            ProcessorType: typeof(StubExtensionIndex),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["no-desc"] = new("NoDesc", "no-desc", typeof(string), string.Empty, IsRequired: false, Aliases: [], AllowedValues: []),
                ["with-desc"] = new("WithDesc", "with-desc", typeof(string), "A described parameter.", IsRequired: true, Aliases: ["wd"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>());

        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["stub"] = s_stubCommand,
        };

        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["Stub-Command"] = s_stubCommand,
        };

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
            => clientType == ClientType.CLI ? s_cliTable[name] : s_psTable[name];

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => clientType == ClientType.CLI ? s_cliTable : s_psTable;

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
            => GetCommandTable(clientType).Values;
    }
}
