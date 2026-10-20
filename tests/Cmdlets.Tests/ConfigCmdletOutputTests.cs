// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsConfig;

using System.Management.Automation;
using System.Reflection;
using GetAzConfigCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigCmdlet;
using GetAzConfigInfoCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigInfoCmdlet;
using GetAzConfigListCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigListCmdlet;
using SetAzConfigCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.SetAzConfigCmdlet;

namespace Cmdlets.Tests;

public class ConfigCmdletOutputTests
{
    [Theory]
    [InlineData(typeof(GetAzConfigCmdlet))]
    [InlineData(typeof(GetAzConfigListCmdlet))]
    [InlineData(typeof(GetAzConfigInfoCmdlet))]
    public void ReadCmdlets_ShouldEnumerateStructuredOutput(Type cmdletType)
    {
        var cmdlet = Activator.CreateInstance(cmdletType);
        var enumerateOutput = cmdletType.GetProperty(
            "EnumerateOutput",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(enumerateOutput);
        Assert.True((bool)enumerateOutput.GetValue(cmdlet)!);
    }

    [Theory]
    [InlineData(typeof(GetAzConfigCmdlet))]
    [InlineData(typeof(GetAzConfigListCmdlet))]
    public void ReadCmdlets_ShouldNotExposeAsJson(Type cmdletType)
    {
        Assert.Null(cmdletType.GetProperty("AsJson"));
    }

    [Fact]
    public void GetConfigCmdlets_ShouldDeclareStructuredOutputTypes()
    {
        AssertOutputType<GetAzConfigCmdlet>("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry");
        AssertOutputType<GetAzConfigListCmdlet>("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry");
        AssertOutputType<GetAzConfigInfoCmdlet>(
            "Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigKeyInfo",
            typeof(string).FullName!);
    }

    private static void AssertOutputType<TCmdlet>(params string[] expectedTypeNames)
    {
        var outputType = typeof(TCmdlet).GetCustomAttribute<OutputTypeAttribute>();

        Assert.NotNull(outputType);
        Assert.Equal(expectedTypeNames, outputType.Type.Select(type => type.Name));
    }

    [Fact]
    public void SetConfig_ExposesMutuallyExclusiveValueAddRemoveParameterSets()
    {
        var metadata = new CommandMetadata(typeof(SetAzConfigCmdlet));
        Assert.Equal("Value", metadata.DefaultParameterSetName);
        foreach (var name in new[] { "Value", "Add", "Remove" })
        {
            var parameter = metadata.Parameters[name];
            var set = Assert.Single(parameter.ParameterSets);
            Assert.Equal(name, set.Key);
            Assert.True(set.Value.IsMandatory);
        }

        Assert.True(metadata.Parameters["Key"].ParameterSets[ParameterAttribute.AllParameterSets].IsMandatory);
        Assert.Contains(metadata.Parameters["Value"].Attributes, attribute => attribute is AllowEmptyStringAttribute);
    }

    [Theory]
    [InlineData("Add", "Entries to add to a list key (comma- or semicolon-separated).")]
    [InlineData("Remove", "Entries to remove from a list key (comma- or semicolon-separated).")]
    public void SetConfig_ListMutationParametersProvideHelpDescriptions(string name, string expectedDescription)
    {
        var metadata = new CommandMetadata(typeof(SetAzConfigCmdlet));
        var attribute = Assert.Single(metadata.Parameters[name].Attributes.OfType<ParameterAttribute>());

        Assert.Equal(expectedDescription, attribute.HelpMessage);
    }

    [Theory]
    [InlineData("Target -Key agent.excludedCommands -Value login", "Value", "Value", "login")]
    [InlineData("Target -Key agent.excludedCommands -Value ''", "Value", "Value", "")]
    [InlineData("Target -Key agent.excludedCommands -Add login", "Add", "Add", "login")]
    [InlineData("Target -Key agent.excludedCommands -Remove login", "Remove", "Remove", "login")]
    public void SetConfig_PowerShellBinderSelectsExpectedParameterSet(
        string invocation, string expectedSet, string parameter, string expectedValue)
    {
        var metadata = new CommandMetadata(typeof(SetAzConfigCmdlet));
        var paramBlock = ProxyCommand.GetCmdletBindingAttribute(metadata)
            + "param(" + ProxyCommand.GetParamBlock(metadata) + ")";
        var initialState = System.Management.Automation.Runspaces.InitialSessionState.Create();
        initialState.LanguageMode = PSLanguageMode.FullLanguage;
        using var ps = PowerShell.Create(initialState);
        ps.AddScript($"function Target {{ {paramBlock} process {{ $PSCmdlet.ParameterSetName; $PSBoundParameters['{parameter}'] }} }}; {invocation}");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        Assert.Equal(2, results.Count);
        Assert.Equal(expectedSet, results[0].BaseObject);
        Assert.Equal(expectedValue, results[1].BaseObject);
    }

    [Fact]
    public void SetConfig_PowerShellBinderRejectsMixedMutations()
    {
        var metadata = new CommandMetadata(typeof(SetAzConfigCmdlet));
        var paramBlock = ProxyCommand.GetCmdletBindingAttribute(metadata)
            + "param(" + ProxyCommand.GetParamBlock(metadata) + ")";
        var initialState = System.Management.Automation.Runspaces.InitialSessionState.Create();
        initialState.LanguageMode = PSLanguageMode.FullLanguage;
        using var ps = PowerShell.Create(initialState);
        ps.AddScript($"function Target {{ {paramBlock} process {{ 'unexpected' }} }}; Target -Key agent.excludedCommands -Value login -Add profile");
        Assert.Empty(ps.Invoke());
        Assert.NotEmpty(ps.Streams.Error);
    }
}
