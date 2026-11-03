// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

/// <summary>
/// PowerShell-surface coverage for managed identity cmdlets: parameter-set selection and
/// metadata-to-processor binding for create/list/get/update/delete command routes.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ManagedServiceIdentityCmdletTests
{
    private static ArgumentParsingService PSBinder() =>
        new(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));

    [Theory]
    [InlineData("Target", "List")]
    [InlineData("Target -ResourceGroupName rg1", "List")]
    [InlineData("Target -ResourceGroupName rg1 -Name id1", "Get")]
    public void GetCmdlet_SelectsExpectedParameterSet(string invocation, string expectedSet)
    {
        var (parameterSet, _) = BindWithSet(typeof(GetAzUserAssignedIdentityCmdlet), invocation);
        Assert.Equal(expectedSet, parameterSet);
    }

    [Fact]
    public void GetCmdlet_DefaultParameterSet_IsList()
    {
        var cmdlet = typeof(GetAzUserAssignedIdentityCmdlet).GetCustomAttributes(typeof(CmdletAttribute), inherit: true)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.Equal("List", cmdlet.DefaultParameterSetName);
    }

    [Fact]
    public void PSBinding_GetRoute_MapsToShowProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzUserAssignedIdentity_Get", ClientType.PowerShell);
        var processor = new ShowIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
            },
            metadata,
            new Profile());

        Assert.Equal(typeof(ShowIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
    }

    [Fact]
    public void PSBinding_ListRoute_MapsToListProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzUserAssignedIdentity_List", ClientType.PowerShell);
        var processor = new ListIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
            },
            metadata,
            new Profile());

        Assert.Equal(typeof(ListIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
    }

    [Fact]
    public void PSBinding_NewRoute_MapsTagAndRestrictionToCreateProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new CreateIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
                ["Location"] = "eastus",
                ["Tags"] = new Dictionary<string, string> { ["env"] = "test" },
                ["ResourceRestriction"] = new[] { "Microsoft.Compute" },
                ["IsolationScope"] = "Regional",
            },
            metadata,
            new Profile());

        Assert.Equal(typeof(CreateIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus", processor.Location);
        Assert.Equal("test", processor.Tags!["env"]);
        Assert.Equal(["Microsoft.Compute"], processor.ResourceRestrictionProviders);
        Assert.Equal("Regional", processor.IsolationScope);
    }

    [Fact]
    public void PSBinding_UpdateRoute_MapsTagAndRestrictionToUpdateProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Update-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new UpdateIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
                ["Location"] = "eastus2",
                ["Tags"] = new Dictionary<string, string> { ["env"] = "prod" },
                ["ResourceRestriction"] = new[] { "Microsoft.Storage/accounts" },
                ["IsolationScope"] = "None",
            },
            metadata,
            new Profile());

        Assert.Equal(typeof(UpdateIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus2", processor.Location);
        Assert.Equal("prod", processor.Tags!["env"]);
        Assert.Equal(["Microsoft.Storage/accounts"], processor.ResourceRestrictionProviders);
        Assert.Equal("None", processor.IsolationScope);
    }

    [Fact]
    public void PSBinding_RemoveRoute_MapsToDeleteProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Remove-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new DeleteIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
            },
            metadata,
            new Profile());

        Assert.Equal(typeof(DeleteIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
    }

    [Fact]
    public void CommandExecution_GetWithoutName_RoutesToListCommand()
    {
        var invocation = InvokeAndCaptureInvocation(
            typeof(GetAzUserAssignedIdentityCmdlet),
            "Get-AzUserAssignedIdentity",
            ("SubscriptionId", "sub-1"),
            ("ResourceGroupName", "rg1"));

        Assert.Equal("ManagedServiceIdentity", invocation.ExtensionName);
        Assert.Equal("Get-AzUserAssignedIdentity_List", invocation.CommandName);
        Assert.Equal("sub-1", invocation.Parameters["SubscriptionId"]);
        Assert.Equal("rg1", invocation.Parameters["ResourceGroupName"]);
    }

    [Fact]
    public void CommandExecution_GetWithName_RoutesToGetCommand()
    {
        var invocation = InvokeAndCaptureInvocation(
            typeof(GetAzUserAssignedIdentityCmdlet),
            "Get-AzUserAssignedIdentity",
            ("SubscriptionId", "sub-1"),
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"));

        Assert.Equal("ManagedServiceIdentity", invocation.ExtensionName);
        Assert.Equal("Get-AzUserAssignedIdentity_Get", invocation.CommandName);
        Assert.Equal("id1", invocation.Parameters["Name"]);
    }

    [Fact]
    public void CommandExecution_New_ConvertsTagAndRoutesToCreateCommand()
    {
        var invocation = InvokeAndCaptureInvocation(
            typeof(NewAzUserAssignedIdentityCmdlet),
            "New-AzUserAssignedIdentity",
            ("SubscriptionId", "sub-1"),
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("Location", "eastus"),
            ("Tag", new Hashtable { ["env"] = "test" }),
            ("IsolationScope", "Regional"),
            ("ResourceRestriction", new[] { "Microsoft.Compute" }));

        Assert.Equal("ManagedServiceIdentity", invocation.ExtensionName);
        Assert.Equal("New-AzUserAssignedIdentity", invocation.CommandName);
        var tags = Assert.IsType<Dictionary<string, string>>(invocation.Parameters["Tags"]);
        Assert.Equal("test", tags["env"]);
        Assert.Equal(new[] { "Microsoft.Compute" }, invocation.Parameters["ResourceRestriction"]);
        Assert.Equal("Regional", invocation.Parameters["IsolationScope"]);
    }

    [Fact]
    public void CommandExecution_Update_RoutesToUpdateCommand()
    {
        var invocation = InvokeAndCaptureInvocation(
            typeof(UpdateAzUserAssignedIdentityCmdlet),
            "Update-AzUserAssignedIdentity",
            ("SubscriptionId", "sub-1"),
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("IsolationScope", "None"),
            ("Tag", new Hashtable { ["env"] = "prod" }),
            ("ResourceRestriction", new[] { "Microsoft.Storage/accounts" }));

        Assert.Equal("ManagedServiceIdentity", invocation.ExtensionName);
        Assert.Equal("Update-AzUserAssignedIdentity", invocation.CommandName);
        Assert.Equal("sub-1", invocation.Parameters["SubscriptionId"]);
        Assert.Equal("rg1", invocation.Parameters["ResourceGroupName"]);
        Assert.Equal("id1", invocation.Parameters["Name"]);
        var tags = Assert.IsType<Dictionary<string, string>>(invocation.Parameters["Tags"]);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("None", invocation.Parameters["IsolationScope"]);
        Assert.Equal(new[] { "Microsoft.Storage/accounts" }, invocation.Parameters["ResourceRestriction"]);
    }

    [Fact]
    public void CommandExecution_Remove_RoutesToDeleteCommand()
    {
        var invocation = InvokeAndCaptureInvocation(
            typeof(RemoveAzUserAssignedIdentityCmdlet),
            "Remove-AzUserAssignedIdentity",
            ("SubscriptionId", "sub-1"),
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("Confirm", false));

        Assert.Equal("ManagedServiceIdentity", invocation.ExtensionName);
        Assert.Equal("Remove-AzUserAssignedIdentity", invocation.CommandName);
        Assert.Equal("sub-1", invocation.Parameters["SubscriptionId"]);
        Assert.Equal("rg1", invocation.Parameters["ResourceGroupName"]);
        Assert.Equal("id1", invocation.Parameters["Name"]);
    }

    [Fact]
    public void CommandExecution_Remove_WithWhatIf_DoesNotInvokeEngine()
    {
        InvokeAndAssertNoExecution(
            typeof(RemoveAzUserAssignedIdentityCmdlet),
            "Remove-AzUserAssignedIdentity",
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("WhatIf", true));
    }

    [Fact]
    public void CommandExecution_New_WithWhatIf_DoesNotInvokeEngine()
    {
        InvokeAndAssertNoExecution(
            typeof(NewAzUserAssignedIdentityCmdlet),
            "New-AzUserAssignedIdentity",
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("Location", "eastus"),
            ("WhatIf", true));
    }

    [Fact]
    public void CommandExecution_Update_WithWhatIf_DoesNotInvokeEngine()
    {
        InvokeAndAssertNoExecution(
            typeof(UpdateAzUserAssignedIdentityCmdlet),
            "Update-AzUserAssignedIdentity",
            ("ResourceGroupName", "rg1"),
            ("Name", "id1"),
            ("IsolationScope", "None"),
            ("WhatIf", true));
    }

    private static (string ParameterSet, Hashtable Bound) BindWithSet(Type cmdlet, string invocation)
    {
        string paramBlock = ProxyCommand.GetCmdletBindingAttribute(new System.Management.Automation.CommandMetadata(cmdlet))
            + "param(" + ProxyCommand.GetParamBlock(new System.Management.Automation.CommandMetadata(cmdlet)) + ")";
        var iss = InitialSessionState.Create();
        iss.LanguageMode = PSLanguageMode.FullLanguage;
        using var ps = PowerShell.Create(iss);
        ps.AddScript($"function Target {{ {paramBlock} process {{ $PSCmdlet.ParameterSetName; $PSBoundParameters }} }}; {invocation}");
        var result = ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        Assert.Equal(2, result.Count);

        var bound = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in (IDictionary)result[1].BaseObject)
        {
            bound[entry.Key] = entry.Value is PSObject pso ? pso.BaseObject : entry.Value;
        }

        return ((string)result[0].BaseObject, bound);
    }

    private static CommandInvocation InvokeAndCaptureInvocation(
        Type cmdletType,
        string commandName,
        params (string Name, object Value)[] parameters)
    {
        AzclipsServiceProvider.Dispose();
        var session = new CapturingSession();
        CommandInvocation? captured = null;
        var engine = new Mock<IEngine>(MockBehavior.Strict);
        engine.Setup(e => e.OpenExecutionSession(It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
            .Callback<CommandInvocation, OutputStreams, CancellationToken>((invocation, _, _) => captured = invocation)
            .Returns(session);

        try
        {
            AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(engine.Object));
            new ModuleInitializer().OnImport();

            var iss = InitialSessionState.CreateDefault2();
            iss.Commands.Add(new SessionStateCmdletEntry(commandName, cmdletType, null));
            using var ps = PowerShell.Create(iss);
            ps.AddCommand(commandName);
            foreach (var (name, value) in parameters)
            {
                ps.AddParameter(name, value);
            }

            _ = ps.Invoke();
            Assert.Empty(ps.Streams.Error);
            Assert.NotNull(captured);
            Assert.True(session.RunCalled);
            return captured!;
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private static void InvokeAndAssertNoExecution(
        Type cmdletType,
        string commandName,
        params (string Name, object Value)[] parameters)
    {
        AzclipsServiceProvider.Dispose();
        var engine = new Mock<IEngine>(MockBehavior.Strict);

        try
        {
            AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(engine.Object));
            new ModuleInitializer().OnImport();

            var iss = InitialSessionState.CreateDefault2();
            iss.Commands.Add(new SessionStateCmdletEntry(commandName, cmdletType, null));
            using var ps = PowerShell.Create(iss);
            ps.AddCommand(commandName);
            foreach (var (name, value) in parameters)
            {
                ps.AddParameter(name, value);
            }

            _ = ps.Invoke();
            Assert.Empty(ps.Streams.Error);
            engine.Verify(e => e.OpenExecutionSession(It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private sealed class CapturingSession : ISession
    {
        public string Id => "test-session";
        public IEventBus EventBus => throw new NotSupportedException();
        public bool RunCalled { get; private set; }
        public void Run() => RunCalled = true;
        public Task RunAsync()
        {
            RunCalled = true;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
