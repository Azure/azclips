// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;

namespace Cli.Tests;

public class NetworkParameterSetTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static readonly NetworkIndex s_index = new();

    [Theory]
    // #1345: a missing --resource-group was sent as `resourceGroups//`.
    [InlineData("network nic create", "name=nic location=eastus subnet=default vnet-name=vnet")]
    // #1322: a missing --name returned an all-null resource with exit code 0.
    [InlineData("network vnet show", "resource-group=rg")]
    [InlineData("network vnet subnet show", "resource-group=rg name=default")]
    [InlineData("network public-ip delete", "name=pip")]
    public void CliBinding_MissingRequiredArgument_ThrowsUsageError(string command, string arguments)
    {
        Assert.Throws<UsageErrorException>(() => Bind(command, arguments));
    }

    [Fact]
    public void CliBinding_NicCreateWithOptionalFlagsAndProfile_Binds()
    {
        var processor = (CreateOrUpdateNetworkInterfacesProcessor_Customization)Bind(
            "network nic create", "g=rg n=nic l=eastus subnet=default vnet-name=vnet profile=work");

        Assert.Equal("rg", processor.ResourceGroupName);
        Assert.Equal("vnet", processor.VnetName);
        Assert.Equal("work", processor.Profile);
        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
    }

    [Fact]
    public void CliBinding_ListWithoutResourceGroup_Binds()
    {
        var processor = (ListAllVirtualNetworksProcessor_Orchestrator)Bind("network vnet list", string.Empty);

        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
    }

    private static IProcessor Bind(string command, string arguments)
    {
        CommandMetadata metadata = s_index.GetCommandMetadata(command, ClientType.CLI);
        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));

        return binder.BindArgumentsToProcessor(
            metadata.GetInstance(),
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => pair[0], pair => (object)new List<List<string>> { new() { pair[1] } }),
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });
    }
}
