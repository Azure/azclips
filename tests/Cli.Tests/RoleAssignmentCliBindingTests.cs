// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

public class RoleAssignmentCliBindingTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string AssigneeObjectId = "11111111-2222-3333-4444-555555555555";
    private const string RoleDefinitionId = "b24988ac-6180-42a0-ab88-20f7382dd24c";
    private const string AssignmentName = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_Create_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment create", ClientType.CLI);
        var processor = (NewRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        // --role is a required parameter on create (unlike list), so it must be supplied for the
        // binder's required-parameter check to pass; this test otherwise isolates the -g/--resource-group
        // alias -> ResourceGroupName binding.
        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [argumentName] = Value("test-rg"),
                ["role"] = Value(RoleDefinitionId),
                ["assignee-object-id"] = Value(AssigneeObjectId),
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_Delete_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment delete", ClientType.CLI);
        var processor = (DeleteRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [argumentName] = Value("test-rg"),
                ["name"] = Value(AssignmentName),
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("n")]
    public void CliBinding_Delete_BindsNameAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment delete", ClientType.CLI);
        var processor = (DeleteRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [argumentName] = Value(AssignmentName) },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal(AssignmentName, processor.Name);
    }

    [Fact]
    public void CliBinding_List_BindsListParameters()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["subscription-id"] = Value(TestSubscriptionId),
                ["resource-group"] = Value("test-rg"),
                ["assignee-object-id"] = Value(AssigneeObjectId),
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal(TestSubscriptionId, processor.SubscriptionId);
        Assert.Equal("test-rg", processor.ResourceGroupName);
        Assert.Equal(AssigneeObjectId, processor.AssigneeObjectId);
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("g")]
    public void CliBinding_List_BindsResourceGroupAliases(string argumentName)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [argumentName] = Value("test-rg") },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Fact]
    public void CliBinding_List_BindsScopeBreadthFlags()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = (ListRoleAssignmentProcessor_Orchestrator)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["include-inherited"] = new List<List<string>> { new() },
                ["all"] = new List<List<string>> { new() },
                ["include-groups"] = new List<List<string>> { new() },
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.True(processor.IncludeInherited);
        Assert.True(processor.All);
        Assert.True(processor.IncludeGroups);
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];
}
