// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Help.Tool.Validation;
using Core.Abstractions;
using Core.Cli.Parser;

namespace Azclips.Help.Tool.Tests;

public sealed class CommandTreeConventionValidatorTests
{
    [Theory]
    [InlineData("sample item", "sample item show")]
    [InlineData("sample", "sample show")]
    [InlineData(" SAMPLE   ITEM ", "sample item\tshow")]
    public void ExecutableParent_ReportsBothPathsAndOwner(string parent, string child)
    {
        var report = Validate(new StubIndex([parent, child]));

        var error = Assert.Single(report.Errors);
        Assert.True(report.HasErrors);
        Assert.Equal(ProblemIds.CLI_CommandGroupCollision, error.ProblemId);
        Assert.Contains("extension 'First'", error.Message);
        Assert.Contains("is also a group containing", error.Message);
        Assert.Contains("az sample", error.Message);
        Assert.Contains("show", error.Message);
        Assert.Contains("Move the parent action", error.Message);
    }

    [Fact]
    public void ParentAcrossExtensions_ReportsBothOwners()
    {
        var report = new CommandTreeConventionValidator().Validate(new Dictionary<string, IExtensionIndex>
        {
            ["First"] = new StubIndex(["sample item"]),
            ["Second"] = new StubIndex(["sample item show"]),
        });

        var error = Assert.Single(report.Errors);
        Assert.Equal(ProblemIds.CLI_CommandGroupCollision, error.ProblemId);
        Assert.Contains("'az sample item'", error.Message);
        Assert.Contains("'az sample item show'", error.Message);
        Assert.Contains("extension 'First'", error.Message);
        Assert.Contains("extension 'Second'", error.Message);
    }

    [Fact]
    public void EveryExecutableAncestor_IsReported()
    {
        var report = Validate(new StubIndex(["sample", "sample item", "sample item show"]));

        Assert.Equal(3, report.Errors.Count());
        Assert.All(report.Errors, error => Assert.Equal(ProblemIds.CLI_CommandGroupCollision, error.ProblemId));
    }

    [Fact]
    public void SiblingsTokenPrefixLookalikesAndStandaloneRoots_Pass()
    {
        var report = Validate(new StubIndex(["sample bar", "sample barcode", "sample other show", "login", "rest"]));

        Assert.False(report.HasErrors);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void MigratedAgentTree_Passes()
    {
        var report = Validate(new StubIndex([
            "agent tool docs list", "agent tool docs show", "agent tool docs generate",
            "agent tool env show", "agent tool resource find", "agent tool resource show",
            "agent tool resource sku", "agent tool resource tree", "agent tool resource refresh",
        ]));

        Assert.Empty(report.Issues);
    }

    [Fact]
    public void FormerAgentTree_FailsBothRules()
    {
        var report = Validate(new StubIndex(["agent tool help", "agent tool help generate"]));

        Assert.True(report.HasErrors);
        Assert.Single(report.Errors, error => error.ProblemId == ProblemIds.CLI_CommandGroupCollision);
        Assert.Equal(2, report.Errors.Count(error => error.ProblemId == ProblemIds.CLI_ReservedGlobalArgument));
    }

    public static IEnumerable<object[]> GlobalNamesAtEveryDepth()
    {
        foreach (var name in KnownGlobalFlags.All.SelectMany(flag => flag.Aliases.Prepend(flag.Name))
                     .Select(name => name.TrimStart('-')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return [name, $"{name} item show"];
            yield return [name, $"sample {name} show"];
            yield return [name, $"sample item {name}"];
        }
    }

    [Theory]
    [MemberData(nameof(GlobalNamesAtEveryDepth))]
    public void GlobalNamesAndAliases_AreReservedAtEveryDepth(string name, string command)
    {
        var report = Validate(new StubIndex([command.ToUpperInvariant()]));

        var error = Assert.Single(report.Errors);
        Assert.True(report.HasErrors);
        Assert.Equal(ProblemIds.CLI_ReservedGlobalArgument, error.ProblemId);
        Assert.Contains($"reserved segment '{name}'", error.Message);
        Assert.Contains("global argument", error.Message);
        Assert.Contains("extension 'First'", error.Message);
    }

    [Theory]
    [InlineData("sample --output")]
    [InlineData("sample -h")]
    [InlineData("sample --?")]
    public void FlagSpellingsAsRegisteredSegments_AreAlsoRejected(string command)
    {
        var report = Validate(new StubIndex([command]));

        Assert.Equal(ProblemIds.CLI_ReservedGlobalArgument, Assert.Single(report.Errors).ProblemId);
    }

    [Fact]
    public void InternalHyphensArePreserved_AndSimilarNamesPass()
    {
        Assert.Equal(ProblemIds.CLI_ReservedGlobalArgument,
            Assert.Single(Validate(new StubIndex(["sample no-cache show"])).Errors).ProblemId);
        Assert.Empty(Validate(new StubIndex(["sample nocache show", "sample help-docs", "sample output-format"])).Issues);
    }

    [Fact]
    public void AdditionalRoutedAliasKeys_AreValidated()
    {
        var index = new StubIndex(["sample item show"], ["sample item", "sample help"]);

        var report = Validate(index);

        Assert.Single(report.Errors, error => error.ProblemId == ProblemIds.CLI_CommandGroupCollision);
        Assert.Single(report.Errors, error => error.ProblemId == ProblemIds.CLI_ReservedGlobalArgument);
    }

    [Fact]
    public void DuplicateDiscoveryKeys_DoNotDuplicateDiagnostics()
    {
        var report = Validate(new StubIndex(["sample help"], ["sample help", "SAMPLE HELP"]));

        Assert.Single(report.Errors);
    }

    [Fact]
    public void MetadataAliasesAndPowerShellCommands_AreNotCliPaths()
    {
        var index = new StubIndex(["sample show"]);
        index.GetCommandMetadata("sample show", ClientType.CLI).Aliases.Add("sample help");
        Assert.NotEmpty(index.GetCommandTable(ClientType.PowerShell));

        Assert.Empty(Validate(index).Issues);
    }

    [Fact]
    public void RegisteredCommands_AreCheckedEvenWhenDiscoveryKeysAreEmpty()
    {
        var report = Validate(new StubIndex(["sample help"]));

        Assert.Equal(ProblemIds.CLI_ReservedGlobalArgument, Assert.Single(report.Errors).ProblemId);
    }

    [Fact]
    public void MissingIndexesAndEmptyCommandPaths_FailExplicitly()
    {
        var empty = new CommandTreeConventionValidator().Validate(new Dictionary<string, IExtensionIndex>());
        Assert.True(empty.HasErrors);
        Assert.Equal(ProblemIds.Tool_NoExtensionsFound, Assert.Single(empty.Errors).ProblemId);

        var invalid = Validate(new StubIndex([" "]));
        Assert.Equal(ProblemIds.CLI_InvalidCommandPath, Assert.Single(invalid.Errors).ProblemId);
        Assert.Contains("First", Assert.Single(invalid.Errors).Message);
    }

    private static ValidationReport Validate(IExtensionIndex index) =>
        new CommandTreeConventionValidator().Validate(new Dictionary<string, IExtensionIndex> { ["First"] = index });

    private sealed class StubIndex : IExtensionIndex
    {
        private readonly IReadOnlyDictionary<string, CommandMetadata> _commands;
        private readonly string[] _routedKeys;

        public StubIndex(string[] commands, string[]? routedKeys = null)
        {
            _routedKeys = routedKeys ?? [];
            _commands = commands.ToDictionary(command => command, Metadata, StringComparer.OrdinalIgnoreCase);
        }

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => GetCommandTable(clientType)[name];

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
            clientType == ClientType.CLI ? _commands : new Dictionary<string, CommandMetadata> { ["Get-SampleHelp"] = Metadata("Get-SampleHelp") };

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;

        public IEnumerable<string> GetCommandKeys(ClientType clientType) => clientType == ClientType.CLI ? _routedKeys : ["Get-SampleHelp"];

        private static CommandMetadata Metadata(string command) => new(
            command, command, [], command, "Stub", typeof(StubIndex),
            new Dictionary<string, ParameterMetadata>(), new Dictionary<string, string>());
    }
}
