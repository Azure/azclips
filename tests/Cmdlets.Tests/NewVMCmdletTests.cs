// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsCompute;

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.DependencyInjection;
using CmdletsCompute::Microsoft.Azure.Azclips.Cmdlets.Compute;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AccountsModuleInitializer = Microsoft.Azure.Azclips.Cmdlets.Accounts.ModuleInitializer;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class NewVMCmdletTests
{
    [Fact]
    public void NetworkInterfaceId_IsRequiredOnlyForExistingNicParameterSet()
    {
        var attributes = typeof(NewVMCmdlet)
            .GetProperty(nameof(NewVMCmdlet.NetworkInterfaceId))!
            .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
            .Cast<ParameterAttribute>()
            .ToList();

        var parameter = Assert.Single(attributes);
        Assert.Equal("ExistingNetworkInterface", parameter.ParameterSetName);
        Assert.True(parameter.Mandatory);
        Assert.Equal(typeof(string[]), typeof(NewVMCmdlet).GetProperty(nameof(NewVMCmdlet.NetworkInterfaceId))!.PropertyType);
    }

    [Fact]
    public void NetworkCreationParameters_AreExcludedFromExistingNicParameterSet()
    {
        foreach (string propertyName in new[]
        {
            nameof(NewVMCmdlet.NetworkInterfaceName),
            nameof(NewVMCmdlet.VirtualNetworkName),
            nameof(NewVMCmdlet.SubnetName),
            nameof(NewVMCmdlet.SubnetNames),
            nameof(NewVMCmdlet.SubnetAddressPrefixes),
            nameof(NewVMCmdlet.AddressPrefixes),
            nameof(NewVMCmdlet.SecurityGroupName),
            nameof(NewVMCmdlet.PublicIpAddressName),
            nameof(NewVMCmdlet.AcceleratedNetworking),
        })
        {
            var attributes = typeof(NewVMCmdlet)
                .GetProperty(propertyName)!
                .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
                .Cast<ParameterAttribute>();

            Assert.All(attributes, attribute => Assert.Equal("NewNetworkInterface", attribute.ParameterSetName));
        }
    }

    [Fact]
    public void NetworkInterfaceDeleteOption_AcceptsOnlyDeleteAndDetach()
    {
        var validateSet = Assert.Single(
            typeof(NewVMCmdlet)
                .GetProperty(nameof(NewVMCmdlet.NetworkInterfaceDeleteOption))!
                .GetCustomAttributes(typeof(ValidateSetAttribute), inherit: true)
                .Cast<ValidateSetAttribute>());

        Assert.True(validateSet.IgnoreCase);
        Assert.Equal(["Delete", "Detach"], validateSet.ValidValues);
    }

    [Fact]
    public void UefiParameters_BindOmittedNullTrueAndFalseDistinctly()
    {
        Assert.Equal(typeof(bool?), typeof(NewVMCmdlet).GetProperty(nameof(NewVMCmdlet.EnableSecureBoot))!.PropertyType);
        Assert.Equal(typeof(bool?), typeof(NewVMCmdlet).GetProperty(nameof(NewVMCmdlet.EnableVtpm))!.PropertyType);

        var omitted = Bind(BaseInvocation);
        Assert.False(omitted.ContainsKey(nameof(NewVMCmdlet.EnableSecureBoot)));
        Assert.False(omitted.ContainsKey(nameof(NewVMCmdlet.EnableVtpm)));

        var explicitNulls = Bind($"{BaseInvocation} -EnableSecureBoot $null -EnableVtpm $null");
        Assert.True(explicitNulls.ContainsKey(nameof(NewVMCmdlet.EnableSecureBoot)));
        Assert.True(explicitNulls.ContainsKey(nameof(NewVMCmdlet.EnableVtpm)));
        Assert.Null(explicitNulls[nameof(NewVMCmdlet.EnableSecureBoot)]);
        Assert.Null(explicitNulls[nameof(NewVMCmdlet.EnableVtpm)]);

        var explicitValues = Bind($"{BaseInvocation} -EnableSecureBoot $false -EnableVtpm $true");
        Assert.False(Assert.IsType<bool>(explicitValues[nameof(NewVMCmdlet.EnableSecureBoot)]));
        Assert.True(Assert.IsType<bool>(explicitValues[nameof(NewVMCmdlet.EnableVtpm)]));
    }

    [Fact]
    public void Zone_BindsAsSingleValue()
    {
        var bound = Bind($"{BaseInvocation} -Zone 1");
        Assert.True(bound.ContainsKey(nameof(NewVMCmdlet.Zone)));
        Assert.Equal("1", Assert.IsType<string>(bound[nameof(NewVMCmdlet.Zone)]));
    }

    [Fact]
    public void StorageSku_BindsAsSingleValue()
    {
        var bound = Bind($"{BaseInvocation} -StorageSku Premium_LRS");
        Assert.Equal("Premium_LRS", Assert.IsType<string>(bound[nameof(NewVMCmdlet.StorageSku)]));
    }

    [Fact]
    public void SshKeyValues_BindWithoutAdminPassword()
    {
        var adminPasswordParameter = Assert.Single(
            typeof(NewVMCmdlet)
                .GetProperty(nameof(NewVMCmdlet.AdminPassword))!
                .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
                .Cast<ParameterAttribute>());
        Assert.False(adminPasswordParameter.Mandatory);

        var bound = Bind($"{BaseInvocationWithoutAuthentication} -SshKeyValues key-one,key-two");

        Assert.False(bound.ContainsKey(nameof(NewVMCmdlet.AdminPassword)));
        Assert.Equal(["key-one", "key-two"], Assert.IsType<string[]>(bound[nameof(NewVMCmdlet.SshKeyValues)]));
    }

    [Fact]
    public void SshKeyValues_AreForwardedWithoutAdminPassword()
    {
        AzclipsServiceProvider.Dispose();
        var session = new Mock<ISession>();
        var engine = new Mock<IEngine>();
        CommandInvocation? captured = null;
        engine.Setup(engine => engine.OpenExecutionSession(
                It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
            .Callback<CommandInvocation, OutputStreams, CancellationToken>((invocation, _, _) => captured = invocation)
            .Returns(session.Object);

        try
        {
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(Mock.Of<ITelemetryService>());
            });
            new AccountsModuleInitializer().OnImport();
            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("New-AzVM", typeof(NewVMCmdlet), null));
            using var runspace = RunspaceFactory.CreateRunspace(state);
            runspace.Open();
            using var shell = PowerShell.Create();
            shell.Runspace = runspace;
            shell.AddCommand("New-AzVM")
                .AddParameter("Name", "vm")
                .AddParameter("Location", "eastus")
                .AddParameter("ResourceGroupName", "rg")
                .AddParameter("VmSize", "Standard_D2s_v3")
                .AddParameter("AdminUsername", "azadmin")
                .AddParameter("SshKeyValues", new[] { "key-one", "key-two" })
                .AddParameter("NetworkInterfaceName", "nic");

            Assert.Empty(shell.Invoke());

            Assert.Empty(shell.Streams.Error);
            Assert.NotNull(captured);
            Assert.False(captured.Parameters.ContainsKey("AdminPassword"));
            Assert.Equal(["key-one", "key-two"], Assert.IsType<List<string>>(captured.Parameters["SshKeyValues"]));
            session.Verify(session => session.Run(), Times.Once);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    [Fact]
    public void OSDiskDeleteOption_PassesValueThrough()
    {
        var bound = Bind($"{BaseInvocation} -OSDiskDeleteOption FutureOption");
        Assert.Equal("FutureOption", Assert.IsType<string>(bound[nameof(NewVMCmdlet.OSDiskDeleteOption)]));
    }

    private const string BaseInvocation = "Target -Name vm -Location eastus -ResourceGroupName rg -VmSize size -AdminUsername user -AdminPassword pass -NetworkInterfaceName nic";
    private const string BaseInvocationWithoutAuthentication = "Target -Name vm -Location eastus -ResourceGroupName rg -VmSize size -AdminUsername user -NetworkInterfaceName nic";

    private static Hashtable Bind(string invocation)
    {
        var metadata = new System.Management.Automation.CommandMetadata(typeof(NewVMCmdlet));
        string paramBlock = ProxyCommand.GetCmdletBindingAttribute(metadata) + "param(" + ProxyCommand.GetParamBlock(metadata) + ")";
        var state = System.Management.Automation.Runspaces.InitialSessionState.Create();
        state.LanguageMode = PSLanguageMode.FullLanguage;
        using var powerShell = PowerShell.Create(state);
        powerShell.AddScript($"function Target {{ {paramBlock} process {{ $PSBoundParameters }} }}; {invocation}");

        var result = powerShell.Invoke();

        Assert.Empty(powerShell.Streams.Error);
        var values = Assert.IsAssignableFrom<IDictionary>(Assert.Single(result).BaseObject);
        var bound = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in values)
        {
            bound[entry.Key] = entry.Value is PSObject value ? value.BaseObject : entry.Value;
        }
        return bound;
    }
}
