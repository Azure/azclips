// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Cli.Tests;

public class IdentityCliBindingTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string MissingIdentityAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    [Theory]
    [InlineData("identity show")]
    [InlineData("identity update")]
    [InlineData("identity delete")]
    public async Task CliProcess_AddressedIdentityCommandWithoutAddress_ThrowsInvalidInput(string commandName)
    {
        IProcessor processor = BindIdentityCommand(commandName, new());

        InvalidInputException error = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        Assert.Equal(MissingIdentityAddressMessage, error.Message);
    }

    [Theory]
    [InlineData("identity show")]
    [InlineData("identity update")]
    [InlineData("identity delete")]
    public async Task CliProcess_AddressedIdentityCommandWithNameOnly_ThrowsInvalidInput(string commandName)
    {
        IProcessor processor = BindIdentityCommand(commandName, new() { ["name"] = "identity-name" });

        InvalidInputException error = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        Assert.Equal(MissingIdentityAddressMessage, error.Message);
    }

    [Theory]
    [InlineData("identity show")]
    [InlineData("identity update")]
    [InlineData("identity delete")]
    public async Task CliProcess_AddressedIdentityCommandWithResourceGroupOnly_ThrowsInvalidInput(string commandName)
    {
        IProcessor processor = BindIdentityCommand(commandName, new() { ["resource-group"] = "test-rg" });

        InvalidInputException error = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        Assert.Equal(MissingIdentityAddressMessage, error.Message);
    }

    [Fact]
    public void CliBinding_BindsDocumentedAliases_ToRefactoredProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("identity create", ClientType.CLI);
        var processor = (CreateIdentityProcessor_Orchestrator)metadata.GetInstance();
        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));

        binder.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["g"] = new List<List<string>> { new() { "rg1" } },
                ["resource-name"] = new List<List<string>> { new() { "id1" } },
                ["location"] = new List<List<string>> { new() { "eastus" } },
                ["tags"] = new List<List<string>> { new() { "env=test" } },
                ["resource-restriction"] = new List<List<string>> { new() { """{"providers":["Microsoft.Compute"]}""" } },
            },
            metadata,
            new Profile(){SubscriptionId = "sub-1"});

        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus", processor.Location);
        Assert.NotNull(processor.TagsInput);
        Assert.Equal(["env=test"], processor.TagsInput);
        Assert.NotNull(processor.ResourceRestriction);
        Assert.Equal(["""{"providers":["Microsoft.Compute"]}"""], processor.ResourceRestriction);
    }

    private static IProcessor BindIdentityCommand(string commandName, Dictionary<string, string> arguments)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata(commandName, ClientType.CLI);
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
