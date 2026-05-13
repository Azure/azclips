using Core.Abstractions;
using Core.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Login;
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
            extensionName: "login",
            extensionVersion: "1.0.0-alpha");

        Assert.Equal("login", manifest.Extension);
        Assert.Equal("1.0.0-alpha", manifest.Version);
        Assert.Equal(3, manifest.Commands.Count);

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.Equal("Connect-CirrusAccount", loginCommand.PsName);
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

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0-alpha");
        var helpIndex = ExtensionHelpManifestBuilder.BuildIndex([manifest]);

        var group = Assert.Single(helpIndex.Groups);
        Assert.Equal("login", group.Name);
        Assert.Equal("login", group.Prefix);

        Assert.Equal(3, helpIndex.Commands.Count);
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login" && c.PsName == "Connect-CirrusAccount");
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login logout" && c.PsName == "Disconnect-CirrusAccount");
        Assert.Contains(helpIndex.Commands, c => c.CliName == "login show" && c.PsName == "Get-CirrusAccount");
    }

    [Fact]
    public void BuildManifest_ShouldFallback_WhenNoMarkdownExists()
    {
        // Use an extension name that does NOT have a help/ folder, forcing fallback to attributes/defaults.
        var index = new StubExtensionIndex();

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "nonexistent-extension", "1.0.0");

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

        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "nonexistent-extension", "1.0.0");
        var command = manifest.Commands[0];

        // Parameters with empty descriptions should not throw; description should pass through as empty.
        var emptyDescParam = Assert.Single(command.Parameters, p => p.Name == "no-desc");
        Assert.Equal(string.Empty, emptyDescParam.Description);
    }

    [Fact]
    public void BuildManifest_ShouldPreserveParameterAliases()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0");

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.All(loginCommand.Parameters, p => Assert.NotNull(p.Aliases));
    }

    [Fact]
    public void BuildManifest_ShouldHandleCommandsWithNoParameters()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0");

        // login show / login logout have no parameters
        var showCommand = Assert.Single(manifest.Commands, c => c.CliName == "login show");
        Assert.Empty(showCommand.Parameters);
    }

    [Fact]
    public void BuildManifest_ShouldDefaultParameterSetName_WhenNotSpecified()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0");

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");
        Assert.All(loginCommand.Parameters, p => Assert.False(string.IsNullOrEmpty(p.ParameterSetName)));
    }

    // ─────────────────────────────────────────────────────────────────
    // 15.4.3 Surface-specific example filtering at manifest level
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildManifest_ShouldSplitExamples_ByLanguageTag()
    {
        // Login extension has both CLI (bash) and PowerShell examples in its markdown.
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0");

        var loginCommand = Assert.Single(manifest.Commands, c => c.CliName == "login");

        // CLI examples should not contain PowerShell-style commands.
        Assert.NotEmpty(loginCommand.Examples.Cli);
        Assert.All(loginCommand.Examples.Cli, ex => Assert.DoesNotContain("Connect-CirrusAccount", ex.Command));

        // PowerShell examples (if present) should reference Connect-CirrusAccount.
        if (loginCommand.Examples.PowerShell.Count > 0)
        {
            Assert.Contains(loginCommand.Examples.PowerShell, ex => ex.Command.Contains("Connect-CirrusAccount"));
        }
    }

    [Fact]
    public void BuildManifest_ShouldNotMixCliAndPowerShellExamples()
    {
        var index = new ExtensionIndex();
        var manifest = ExtensionHelpManifestBuilder.BuildManifest(index, "login", "1.0.0");

        foreach (var command in manifest.Commands)
        {
            // CLI examples must not contain PowerShell verbs (Get-/Set-/New-/Connect-/Disconnect-)
            Assert.All(command.Examples.Cli, ex =>
            {
                Assert.False(StartsWithPowerShellVerb(ex.Command),
                    $"CLI example '{ex.Title}' contains PowerShell verb: {ex.Command}");
            });
        }

        static bool StartsWithPowerShellVerb(string command)
        {
            var trimmed = command.TrimStart();
            return trimmed.StartsWith("Get-", StringComparison.Ordinal)
                || trimmed.StartsWith("Set-", StringComparison.Ordinal)
                || trimmed.StartsWith("New-", StringComparison.Ordinal)
                || trimmed.StartsWith("Connect-", StringComparison.Ordinal)
                || trimmed.StartsWith("Disconnect-", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Stub IExtensionIndex with a single command and no markdown help backing.
    /// Exercises fallback paths in <see cref="ExtensionHelpManifestBuilder"/>.
    /// Note: builder reads static `s_cliTable`/`s_psTable` fields via reflection, so this stub exposes both.
    /// </summary>
    private sealed class StubExtensionIndex : IExtensionIndex
    {
        public string CliRootSummary => "Stub extension for tests.";

        private static readonly CommandMetadata s_stubCommand = new(
            Name: "StubCommand",
            Aliases: [],
            FullName: "Stub.StubCommand",
            AssemblyName: "Stub",
            ProcessorType: typeof(StubExtensionIndex),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["no-desc"] = new("NoDesc", typeof(string), string.Empty, IsRequired: false, Aliases: [], AllowedValues: []),
                ["with-desc"] = new("WithDesc", typeof(string), "A described parameter.", IsRequired: true, Aliases: ["wd"], AllowedValues: []),
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
