// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Cli.Tests;

public class StorageAccountParameterSetTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static readonly ExtensionIndex s_index = new();

    [Theory]
    [InlineData("storage account create")]
    [InlineData("storage account show")]
    [InlineData("storage account delete")]
    public void CliBinding_MissingResourceGroup_ThrowsUsageError(string command)
    {
        Assert.Throws<UsageErrorException>(() => Bind(command, new() { ["name"] = "azclipsaccount" }));
    }

    [Theory]
    [InlineData("storage account create")]
    [InlineData("storage account show")]
    [InlineData("storage account delete")]
    public void CliBinding_MissingName_ThrowsUsageError(string command)
    {
        Assert.Throws<UsageErrorException>(() => Bind(command, new() { ["resource-group"] = "test-rg" }));
    }

    [Fact]
    public void CliBinding_CreateWithOptionalFlagsAndProfile_Binds()
    {
        var processor = (NewStorageAccountProcessor_Orchestrator)Bind("storage account create", new()
        {
            ["g"] = "test-rg",
            ["n"] = "azclipsaccount",
            ["location"] = "westus2",
            ["sku"] = "Standard_LRS",
            ["min-tls-version"] = "TLS1_2",
            ["profile"] = "work",
        });

        Assert.Equal("test-rg", processor.ResourceGroupName);
        Assert.Equal("azclipsaccount", processor.AccountName);
        Assert.Equal("TLS1_2", processor.MinimumTlsVersion);
        Assert.Equal("work", processor.Profile);
        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
    }

    [Fact]
    public void CliBinding_ListWithoutResourceGroup_ResolvesSubscriptionSet()
    {
        var processor = (ListStorageAccountProcessor_Orchestrator)Bind("storage account list", new());

        Assert.Equal(string.Empty, processor.ResourceGroupName);
        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
    }

    [Fact]
    public void CliBinding_ListWithResourceGroup_ResolvesResourceGroupSet()
    {
        var processor = (ListStorageAccountProcessor_Orchestrator)Bind("storage account list", new() { ["g"] = "test-rg" });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Fact]
    public void CliTable_ParameterSetsAndParameters_Match()
    {
        // Parameter set resolution rejects arguments outside the resolved set, so a flag added to
        // Parameters but not to a set would be unusable; a set member that is not a parameter
        // (e.g. a typo) could never be supplied, making a required member unsatisfiable.
        foreach (CommandMetadata metadata in s_index.GetAllCommands(ClientType.CLI))
        {
            Assert.NotNull(metadata.ParameterSets);
            var parameterNames = metadata.Parameters.Values.Select(parameter => parameter.Name).ToHashSet();
            var members = metadata.ParameterSets!.Values.SelectMany(set => set.Parameters!.Keys).ToHashSet();
            foreach (ParameterMetadata parameter in metadata.Parameters.Values)
            {
                Assert.True(members.Contains(parameter.Name), $"'{metadata.InvocationName}' parameter '{parameter.InvocationName}' is in no parameter set");
            }
            foreach (string member in members)
            {
                Assert.True(parameterNames.Contains(member), $"'{metadata.InvocationName}' parameter set member '{member}' is not a parameter");
            }
            foreach (ParameterSetMetadata set in metadata.ParameterSets.Values)
            {
                Assert.True(set.Parameters!.ContainsKey("Profile"), $"'{metadata.InvocationName}' parameter set '{set.Name}' is missing the injected Profile parameter");
            }
        }
    }

    private static IProcessor Bind(string command, Dictionary<string, string> arguments)
    {
        CommandMetadata metadata = s_index.GetCommandMetadata(command, ClientType.CLI);
        IProcessor processor = metadata.GetInstance();
        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));

        return binder.BindArgumentsToProcessor(
            processor,
            arguments.ToDictionary(kvp => kvp.Key, kvp => (object)new List<List<string>> { new() { kvp.Value } }),
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });
    }
}
