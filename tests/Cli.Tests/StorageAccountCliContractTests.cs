// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Cli.Tests;

public class StorageAccountCliContractTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public void CreateMetadata_ExposesCompatibleDefaultsAliasesAndLargeFileFlag()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("storage account create", ClientType.CLI);

        Assert.False(metadata.Parameters["location"].IsRequired);
        Assert.False(metadata.Parameters["sku"].IsRequired);
        Assert.False(metadata.Parameters["kind"].IsRequired);
        Assert.Equal(["g"], metadata.Parameters["resource-group"].Aliases);
        Assert.Equal(["n"], metadata.Parameters["name"].Aliases);
        Assert.Equal(["l"], metadata.Parameters["location"].Aliases);
        Assert.Equal("resource-group", metadata.ParameterAliases["g"]);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
        Assert.Equal("location", metadata.ParameterAliases["l"]);
        Assert.Equal(typeof(bool), metadata.Parameters["enable-large-file-share"].ParameterType);
    }

    [Theory]
    [InlineData("storage account show", "resource-group", "g")]
    [InlineData("storage account show", "name", "n")]
    [InlineData("storage account list", "resource-group", "g")]
    [InlineData("storage account delete", "resource-group", "g")]
    [InlineData("storage account delete", "name", "n")]
    public void CrudMetadata_ExposesCompatibleAliases(string command, string parameter, string alias)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.Contains(alias, metadata.Parameters[parameter].Aliases);
        Assert.Equal(parameter, metadata.ParameterAliases[alias]);
    }

    [Fact]
    public void CreateBinding_UsesAliasesDefaultsAndValuelessLargeFileFlag()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("storage account create", ClientType.CLI);
        var processor = (NewStorageAccountProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["g"] = Value("test-rg"),
                ["n"] = Value("teststorageaccount"),
                ["enable-large-file-share"] = new List<List<string>>(),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
        Assert.Equal("test-rg", processor.ResourceGroupName);
        Assert.Equal("teststorageaccount", processor.AccountName);
        Assert.Equal("StorageV2", processor.Kind);
        Assert.Equal("Standard_RAGRS", processor.SkuName);
        Assert.Empty(processor.Location);
        Assert.True(processor.EnableLargeFileShare);
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];
}
