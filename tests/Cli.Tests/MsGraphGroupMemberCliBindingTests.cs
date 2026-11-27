// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.MsGraph;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Cli.Tests;

public class MsGraphGroupMemberCliBindingTests
{
    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
    }

    private static List<List<string>> Value(params string[] values) => [[.. values]];

    [Fact]
    public void CliBinding_GroupMemberList_ByGroup_AllowsSelectorPlusSelect()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("msgraph group member list", ClientType.CLI);
        var processor = (ListGroupMemberProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["group"] = Value("Sales"),
                ["select"] = Value("id", "displayName"),
            },
            metadata,
            new Profile());

        Assert.Equal("Sales", processor.Group);
        Assert.NotNull(processor.Select);
        Assert.Equal(["id", "displayName"], processor.Select);
    }

    [Fact]
    public void CliBinding_GroupMemberList_ByDisplayName_AllowsSelectorPlusFilter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("msgraph group member list", ClientType.CLI);
        var processor = (ListGroupMemberProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["display-name"] = Value("Sales Team"),
                ["filter"] = Value("startsWith(displayName,'A')"),
            },
            metadata,
            new Profile());

        Assert.Equal("Sales Team", processor.GroupDisplayName);
        Assert.Equal("startsWith(displayName,'A')", processor.Filter);
    }
}
