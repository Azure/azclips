// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsConfig;

using System.Management.Automation;
using System.Reflection;
using GetAzConfigCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigCmdlet;
using GetAzConfigInfoCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigInfoCmdlet;
using GetAzConfigListCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigListCmdlet;

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
}
