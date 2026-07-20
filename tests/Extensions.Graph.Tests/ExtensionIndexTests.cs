// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Graph;
using Microsoft.Azure.Azclips.Extensions.Graph.Processors;

namespace Extensions.Graph.Tests;

public class ExtensionIndexTests
{
    private static readonly ExtensionIndex s_index = new();

    [Fact]
    public void GetVerbs_ShouldContainMsGraph()
    {
        Assert.Equal(["msgraph"], s_index.GetVerbs());
    }

    [Fact]
    public void CliTable_ShouldExposeMsGraphUserShow()
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey("msgraph user show"));
        Assert.Equal(typeof(GetUserProcessor), table["msgraph user show"].ProcessorType);
    }

    [Fact]
    public void CliTable_ShouldExposeMsGraphUserList()
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey("msgraph user list"));
        Assert.Equal(typeof(ListUserProcessor), table["msgraph user list"].ProcessorType);
    }

    [Theory]
    [InlineData("Get-AzMsGraphUser_List", typeof(ListUserProcessor))]
    [InlineData("Get-AzMsGraphUser_Mail", typeof(ListUserProcessor))]
    [InlineData("Get-AzMsGraphUser_DisplayName", typeof(ListUserProcessor))]
    [InlineData("Get-AzMsGraphUser_StartsWith", typeof(ListUserProcessor))]
    [InlineData("Get-AzMsGraphUser_Get", typeof(GetUserProcessor))]
    [InlineData("Get-AzMsGraphUser_SignedIn", typeof(GetSignedInUserProcessor))]
    public void PsTable_ShouldRouteEveryParameterSetToTheRightProcessor(string key, Type expectedProcessor)
    {
        var table = s_index.GetCommandTable(ClientType.PowerShell);

        Assert.True(table.ContainsKey(key), $"missing PS dispatch key '{key}'");
        Assert.Equal(expectedProcessor, table[key].ProcessorType);
    }

    [Fact]
    public void CliTable_ShowIdParameter_ShouldBeRequiredKebabCase()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user show", ClientType.CLI);

        Assert.True(metadata.Parameters.ContainsKey("id"));
        ParameterMetadata id = metadata.Parameters["id"];
        Assert.Equal("Id", id.Name);
        Assert.True(id.IsRequired);
        Assert.Equal(typeof(string), id.ParameterType);
    }

    [Fact]
    public void CliTable_ListParameters_ShouldMatchAzAdUserList()
    {
        // Locked to exactly the three params `az ad user list` ships. Guards against
        // silently widening the CLI surface.
        var metadata = s_index.GetCommandMetadata("msgraph user list", ClientType.CLI);

        Assert.Equal(3, metadata.Parameters.Count);
        foreach (var expected in new[] { "display-name", "filter", "upn" })
        {
            Assert.True(metadata.Parameters.ContainsKey(expected), $"missing '{expected}' parameter");
            Assert.False(metadata.Parameters[expected].IsRequired, $"'{expected}' should be optional");
            Assert.Equal(typeof(string), metadata.Parameters[expected].ParameterType);
        }
    }

    [Theory]
    [InlineData("Get-AzMsGraphUser_Get", "Id")]
    [InlineData("Get-AzMsGraphUser_Mail", "Mail")]
    [InlineData("Get-AzMsGraphUser_DisplayName", "DisplayName")]
    [InlineData("Get-AzMsGraphUser_StartsWith", "StartsWith")]
    public void PsTable_ConvenienceSets_ShouldRequireTheirDiscriminator(string psKey, string requiredParameter)
    {
        // Each convenience set forwards its shortcut param directly to the processor, which
        // translates it into an OData $filter clause. The required parameter is the shortcut
        // itself, not the raw Filter.
        var metadata = s_index.GetCommandMetadata(psKey, ClientType.PowerShell);

        Assert.True(metadata.Parameters.ContainsKey(requiredParameter), $"'{psKey}' missing '{requiredParameter}'");
        Assert.True(metadata.Parameters[requiredParameter].IsRequired, $"'{psKey}': '{requiredParameter}' should be required");
    }

    [Theory]
    [InlineData("Get-AzMsGraphUser_List")]
    [InlineData("Get-AzMsGraphUser_Mail")]
    [InlineData("Get-AzMsGraphUser_DisplayName")]
    [InlineData("Get-AzMsGraphUser_StartsWith")]
    [InlineData("Get-AzMsGraphUser_Get")]
    [InlineData("Get-AzMsGraphUser_SignedIn")]
    public void PsTable_EverySet_ShouldExposeAllProjectionParams(string psKey)
    {
        // Select/Expand/AppendSelected are Get-AzADUser's (All) params — must appear on every set.
        var metadata = s_index.GetCommandMetadata(psKey, ClientType.PowerShell);

        Assert.True(metadata.Parameters.ContainsKey("Select"), $"'{psKey}' missing Select");
        Assert.Equal(typeof(string[]), metadata.Parameters["Select"].ParameterType);

        Assert.True(metadata.Parameters.ContainsKey("Expand"), $"'{psKey}' missing Expand");
        Assert.Equal(typeof(string[]), metadata.Parameters["Expand"].ParameterType);

        Assert.True(metadata.Parameters.ContainsKey("AppendSelected"), $"'{psKey}' missing AppendSelected");
        Assert.Equal(typeof(bool), metadata.Parameters["AppendSelected"].ParameterType);
    }

    [Fact]
    public void PsTable_ListSet_ShouldExposeAdvancedODataParams()
    {
        // Orderby/Search/ConsistencyLevel are List-only per Get-AzADUser.
        var metadata = s_index.GetCommandMetadata("Get-AzMsGraphUser_List", ClientType.PowerShell);

        Assert.True(metadata.Parameters.ContainsKey("Orderby"));
        Assert.Equal(typeof(string[]), metadata.Parameters["Orderby"].ParameterType);
        Assert.True(metadata.Parameters.ContainsKey("Search"));
        Assert.Equal(typeof(string), metadata.Parameters["Search"].ParameterType);
        Assert.True(metadata.Parameters.ContainsKey("ConsistencyLevel"));
        Assert.Equal(typeof(string), metadata.Parameters["ConsistencyLevel"].ParameterType);
    }

    [Fact]
    public void GetCommandMetadata_UnknownCommand_ShouldThrow()
    {
        Assert.Throws<KeyNotFoundException>(() => s_index.GetCommandMetadata("msgraph not-a-real-command", ClientType.CLI));
    }
}
