// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;

namespace Cli.Tests;

public class CliDefaultParameterSetTests
{
    private static readonly Dictionary<string, IExtensionIndex> s_indexes =
        ExtensionIndexContractTests.Indexes().ToDictionary(entry => entry.Index, entry => entry.Instance);

    private static readonly HashSet<string> s_existingSets =
    [
        "storage account create", "storage account show", "storage account list", "storage account delete",
        "msgraph group member list", "msgraph group member check",
        "agent tool docs show",
    ];

    public static IEnumerable<object[]> Commands() =>
        s_indexes.SelectMany(entry => entry.Value.GetAllCommands(ClientType.CLI)
            .Select(metadata => new object[] { entry.Key, metadata.InvocationName }));

    public static IEnumerable<object[]> MissingRequiredParameters() =>
        Commands().Where(command => !s_existingSets.Contains((string)command[1]))
            .SelectMany(command => Metadata((string)command[0], (string)command[1]).Parameters.Values
                .Where(parameter => parameter.IsRequired)
                .Select(parameter => new object[] { command[0], command[1], parameter.InvocationName }));

    [Fact]
    public void Discovery_CoversEveryProductExtensionIndex()
    {
        var root = global::Tests.Common.Utilities.RepoLocator.GetRepoRoot();
        var count = new[] { Path.Combine(root, "src", "Extensions"), Path.Combine(root, "Agent") }
            .Sum(directory => Directory.EnumerateFiles(directory, "ExtensionIndex.cs", SearchOption.AllDirectories).Count());
        Assert.Equal(count, s_indexes.Count);
        Assert.NotEmpty(s_indexes);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCliCommand_HasSetsAndAllParametersAreMembers(string index, string command)
    {
        var metadata = Metadata(index, command);
        var sets = Assert.IsAssignableFrom<IDictionary<string, ParameterSetMetadata>>(metadata.ParameterSets);
        Assert.NotEmpty(sets);
        Assert.Single(sets.Values, set => set.IsDefault);
        var names = metadata.Parameters.Values.Select(parameter => parameter.Name).ToHashSet();
        var members = sets.Values.SelectMany(set => set.Parameters!.Keys).ToHashSet();
        Assert.True(names.SetEquals(members), $"{command}: parameter-set members do not match canonical parameter names");

        if (!s_existingSets.Contains(command))
        {
            var set = Assert.Single(sets);
            Assert.Equal("Default", set.Key);
            Assert.Equal("Default", set.Value.Name);
            foreach (var parameter in metadata.Parameters.Values)
            {
                var reference = set.Value.Parameters![parameter.Name];
                Assert.Equal(parameter.Name, reference.Name);
                Assert.Equal(parameter.IsRequired, reference.IsRequired);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EmptyInput_RejectsRequiredSetsAndAcceptsOptionalOnlySets(string index, string command)
    {
        var metadata = Metadata(index, command);
        var acceptsEmpty = metadata.ParameterSets!.Values.Any(set => set.Parameters!.Values.All(parameter => !parameter.IsRequired));
        if (acceptsEmpty)
        {
            Assert.NotNull(Bind(metadata, new()));
        }
        else
        {
            var error = Assert.Throws<UsageErrorException>(() => Bind(metadata, new()));
            Assert.StartsWith("Invalid parameter set:", error.Message);
        }
    }

    [Theory]
    [MemberData(nameof(MissingRequiredParameters))]
    public void OmittingEachRequiredParameter_RejectsAtSetValidation(string index, string command, string missing)
    {
        var metadata = Metadata(index, command);
        var arguments = RequiredArguments(metadata, metadata.ParameterSets!["Default"]);
        arguments.Remove(missing);

        var error = Assert.Throws<UsageErrorException>(() => Bind(metadata, arguments));

        Assert.StartsWith("Invalid parameter set:", error.Message);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void RequiredMembersAndOptionalProfile_BindForEverySet(string index, string command)
    {
        var metadata = Metadata(index, command);
        foreach (var set in metadata.ParameterSets!.Values)
        {
            var arguments = RequiredArguments(metadata, set);
            var profileParameter = metadata.Parameters.Values.SingleOrDefault(parameter => parameter.Name == "Profile");
            // rest's injected Profile is a pre-existing field rather than a bindable property.
            if (profileParameter is not null && metadata.ProcessorType.GetProperty("Profile") is not null)
            {
                arguments[profileParameter.InvocationName] = Value("validation");
            }
            Assert.NotNull(Bind(metadata, arguments));
        }
    }

    [Fact]
    public void PowerShellMetadata_HasNoAddedParameterSets()
    {
        foreach (var index in s_indexes.Values)
        {
            foreach (var metadata in index.GetAllCommands(ClientType.PowerShell))
            {
                Assert.Null(metadata.ParameterSets);
            }
        }
    }

    [Theory]
    [InlineData("storage account create", "Default")]
    [InlineData("storage account show", "Default")]
    [InlineData("storage account delete", "Default")]
    [InlineData("storage account list", "List", "ListByResourceGroup")]
    [InlineData("msgraph group member list", "ByGroup", "ByDisplayName")]
    [InlineData("msgraph group member check", "Default")]
    [InlineData("agent tool docs show", "(All)")]
    public void ExistingSets_ArePreserved(string command, params string[] expected)
    {
        var metadata = s_indexes.Values.SelectMany(index => index.GetAllCommands(ClientType.CLI))
            .Single(metadata => metadata.InvocationName == command);
        Assert.Equal(expected.Order(), metadata.ParameterSets!.Keys.Order());
        var table = new Dictionary<string, CommandMetadata> { [command] = metadata };

        table.AddDefaultParameterSets();

        Assert.Same(metadata, table[command]);
    }

    [Fact]
    public void AddingSets_DoesNotMutateSharedMetadataAndIsIdempotent()
    {
        var original = Metadata("Microsoft.Azure.Azclips.Extensions.Config.ExtensionIndex", "config get") with { ParameterSets = null };
        var table = new Dictionary<string, CommandMetadata> { ["config get"] = original };
        table.AddDefaultParameterSets();
        var updated = table["config get"];
        table.AddDefaultParameterSets();

        Assert.Null(original.ParameterSets);
        Assert.NotSame(original, updated);
        Assert.Same(updated, table["config get"]);
    }

    private static CommandMetadata Metadata(string index, string command) =>
        s_indexes[index].GetCommandMetadata(command, ClientType.CLI);

    private static List<List<string>> Value(string value) => [[value]];

    private static Dictionary<string, object> RequiredArguments(CommandMetadata metadata, ParameterSetMetadata set) =>
        set.Parameters!.Values.Where(reference => reference.IsRequired)
            .Select(reference => metadata.Parameters.Values.Single(parameter => parameter.Name == reference.Name))
            .ToDictionary(parameter => parameter.InvocationName,
                parameter => (object)Value(parameter.AllowedValues.FirstOrDefault()
                    ?? (parameter.ParameterType == typeof(bool) ? "true" : "sample")));

    private static IProcessor Bind(CommandMetadata metadata, Dictionary<string, object> arguments)
    {
        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        return binder.BindArgumentsToProcessor(metadata.GetInstance(), arguments, metadata,
            new Profile { SubscriptionId = "00000000-0000-0000-0000-000000000001" });
    }
}
