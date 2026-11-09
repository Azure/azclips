// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Parser;

namespace Cli.Tests.Extensions;

public sealed class CommandTreeConventionValidatorTests
{
    [Fact]
    public void BuiltExtensions_FollowCommandTreeConventions()
    {
        var indexes = ExtensionIndexContractTests.Indexes()
            .ToDictionary(entry => entry.Index, entry => entry.Instance, StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(indexes);

        var errors = new CommandTreeConventionValidator().Validate(indexes);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(error => error.Message)));
    }

    [Theory]
    [InlineData("sample item", "sample item show")]
    [InlineData("sample", "sample show")]
    [InlineData(" SAMPLE   ITEM ", "sample item\tshow")]
    public void ExecutableParent_ReportsBothPathsAndOwner(string parent, string child)
    {
        var report = Validate(new StubIndex([parent, child]));

        var error = Assert.Single(report);
        Assert.Equal(CommandTreeConventionErrorKind.CommandGroupCollision, error.Kind);
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

        var error = Assert.Single(report);
        Assert.Equal(CommandTreeConventionErrorKind.CommandGroupCollision, error.Kind);
        Assert.Contains("'az sample item'", error.Message);
        Assert.Contains("'az sample item show'", error.Message);
        Assert.Contains("extension 'First'", error.Message);
        Assert.Contains("extension 'Second'", error.Message);
    }

    [Fact]
    public void EveryExecutableAncestor_IsReported()
    {
        var report = Validate(new StubIndex(["sample", "sample item", "sample item show"]));

        Assert.Equal(3, report.Count);
        Assert.All(report, error => Assert.Equal(CommandTreeConventionErrorKind.CommandGroupCollision, error.Kind));
    }

    [Fact]
    public void SiblingsTokenPrefixLookalikesAndStandaloneRoots_Pass()
    {
        var report = Validate(new StubIndex(["sample bar", "sample barcode", "sample other show", "login", "rest"]));

        Assert.Empty(report);
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

        var error = Assert.Single(report);
        Assert.Equal(CommandTreeConventionErrorKind.ReservedGlobalArgument, error.Kind);
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

        Assert.Equal(CommandTreeConventionErrorKind.ReservedGlobalArgument, Assert.Single(report).Kind);
    }

    [Fact]
    public void InternalHyphensArePreserved_AndSimilarNamesPass()
    {
        Assert.Equal(CommandTreeConventionErrorKind.ReservedGlobalArgument,
            Assert.Single(Validate(new StubIndex(["sample no-cache show"]))).Kind);
        Assert.Empty(Validate(new StubIndex(["sample nocache show", "sample help-docs", "sample output-format"])));
    }

    [Fact]
    public void AdditionalRoutedAliasKeys_AreValidated()
    {
        var index = new StubIndex(["sample item show"], ["sample item", "sample help"]);

        var report = Validate(index);

        Assert.Single(report, error => error.Kind == CommandTreeConventionErrorKind.CommandGroupCollision);
        Assert.Single(report, error => error.Kind == CommandTreeConventionErrorKind.ReservedGlobalArgument);
    }

    [Fact]
    public void DuplicateDiscoveryKeys_DoNotDuplicateDiagnostics()
    {
        var report = Validate(new StubIndex(["sample help"], ["sample help", "SAMPLE HELP"]));

        Assert.Single(report);
    }

    [Fact]
    public void MetadataAliasesAndPowerShellCommands_AreNotCliPaths()
    {
        var index = new StubIndex(["sample show"]);
        index.GetCommandMetadata("sample show", ClientType.CLI).Aliases.Add("sample help");
        Assert.NotEmpty(index.GetCommandTable(ClientType.PowerShell));

        Assert.Empty(Validate(index));
    }

    [Fact]
    public void RegisteredCommands_AreCheckedEvenWhenDiscoveryKeysAreEmpty()
    {
        var report = Validate(new StubIndex(["sample help"]));

        Assert.Equal(CommandTreeConventionErrorKind.ReservedGlobalArgument, Assert.Single(report).Kind);
    }

    [Fact]
    public void EmptyCommandPaths_FailExplicitly()
    {
        var invalid = Validate(new StubIndex([" "]));
        Assert.Equal(CommandTreeConventionErrorKind.InvalidCommandPath, Assert.Single(invalid).Kind);
        Assert.Contains("First", Assert.Single(invalid).Message);
    }

    private static IReadOnlyList<CommandTreeConventionError> Validate(IExtensionIndex index) =>
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
