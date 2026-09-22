extern alias CmdletsResources;

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ResourcesCmdlets = CmdletsResources::Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class StopManagementGroupDeploymentCmdletTests : IDisposable
{
    private const string DeploymentId =
        "/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Resources/deployments/test-deployment";

    private readonly Mock<IEngine> _engine = new();
    private readonly Mock<ISession> _session = new();
    private readonly List<CommandInvocation> _invocations = [];

    public StopManagementGroupDeploymentCmdletTests()
    {
        _engine.Setup(engine => engine.OpenExecutionSession(
                It.IsAny<CommandInvocation>(),
                It.IsAny<OutputStreams>(),
                It.IsAny<CancellationToken>()))
            .Callback<CommandInvocation, OutputStreams, CancellationToken>(
                (invocation, _, _) => _invocations.Add(invocation))
            .Returns(_session.Object);

        Mock<ITelemetryService> telemetry = new();
        Mock<IConfigurationService> configuration = new();
        configuration.Setup(service => service.GetConfiguration<CoreOptions>(
                It.IsAny<string?>(),
                It.IsAny<CoreOptions?>()))
            .Returns(new CoreOptions());
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(_engine.Object);
            services.AddSingleton(telemetry.Object);
            services.AddSingleton(configuration.Object);
        });
        AzclipsServiceProvider.Initialize(ClientType.PowerShell);
    }

    [Fact]
    public void NameParameterSet_DispatchesWithoutOutput()
    {
        Collection<PSObject> output = Invoke(
            ("ManagementGroupId", "test-mg"),
            ("Name", "test-deployment"),
            ("Confirm", false));

        Assert.Empty(output);
        CommandInvocation invocation = Assert.Single(_invocations);
        Assert.Equal("Stop-AzManagementGroupDeployment", invocation.CommandName);
        Assert.Equal("test-mg", invocation.Parameters["ManagementGroupId"]);
        Assert.Equal("test-deployment", invocation.Parameters["Name"]);
        _session.Verify(session => session.Run(), Times.Once);
    }

    [Fact]
    public void IdAliasParameterSet_DispatchesAndPassThruOutputsTrue()
    {
        Collection<PSObject> output = Invoke(
            ("DeploymentId", DeploymentId),
            ("PassThru", true),
            ("Confirm", false));

        Assert.True(Assert.IsType<bool>(Assert.Single(output).BaseObject));
        CommandInvocation invocation = Assert.Single(_invocations);
        Assert.Equal("test-mg", invocation.Parameters["ManagementGroupId"]);
        Assert.Equal("test-deployment", invocation.Parameters["Name"]);
        _session.Verify(session => session.Run(), Times.Once);
    }

    [Fact]
    public void WhatIf_SkipsDispatchAndProducesNoOutput()
    {
        Collection<PSObject> output = Invoke(
            ("ManagementGroupId", "test-mg"),
            ("Name", "test-deployment"),
            ("WhatIf", true));

        Assert.Empty(output);
        Assert.Empty(_invocations);
        _engine.Verify(engine => engine.OpenExecutionSession(
            It.IsAny<CommandInvocation>(),
            It.IsAny<OutputStreams>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => AzclipsServiceProvider.Dispose();

    private static Collection<PSObject> Invoke(params (string Name, object Value)[] parameters)
    {
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry(
            "Stop-AzManagementGroupDeployment",
            typeof(ResourcesCmdlets.StopManagementGroupDeploymentCmdlet),
            null));

        using Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using PowerShell shell = PowerShell.Create();
        shell.Runspace = runspace;
        shell.AddCommand("Stop-AzManagementGroupDeployment");
        foreach ((string name, object value) in parameters)
        {
            shell.AddParameter(name, value);
        }

        Collection<PSObject> output = shell.Invoke();
        Assert.Empty(shell.Streams.Error);
        return output;
    }
}
