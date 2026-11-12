// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsCompute;

using System.Collections;
using System.Management.Automation;
using CmdletsCompute::Microsoft.Azure.Azclips.Cmdlets.Compute;

namespace Cmdlets.Tests;

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
    public void OSDiskDeleteOption_PassesValueThrough()
    {
        var bound = Bind($"{BaseInvocation} -OSDiskDeleteOption FutureOption");
        Assert.Equal("FutureOption", Assert.IsType<string>(bound[nameof(NewVMCmdlet.OSDiskDeleteOption)]));
    }

    private const string BaseInvocation = "Target -Name vm -Location eastus -ResourceGroupName rg -VmSize size -AdminUsername user -AdminPassword pass";

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
