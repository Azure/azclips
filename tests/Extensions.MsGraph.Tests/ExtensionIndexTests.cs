// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Extensions.MsGraph.Tests;

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
        Assert.Equal(typeof(GetUserProcessor_Orchestrator), table["msgraph user show"].ProcessorType);
    }

    [Fact]
    public void CliTable_ShouldExposeMsGraphUserList()
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey("msgraph user list"));
        Assert.Equal(typeof(ListUserProcessor_Orchestrator), table["msgraph user list"].ProcessorType);
    }

    [Fact]
    public void CliTable_ShouldExposeMsGraphUserSignedIn()
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey("msgraph user signed-in"));
        Assert.Equal(typeof(GetSignedInUserProcessor_Orchestrator), table["msgraph user signed-in"].ProcessorType);
    }

    [Theory]
    [InlineData("Get-AzMsGraphUser_List", typeof(ListUserProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphUser_Mail", typeof(ListUserProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphUser_DisplayName", typeof(ListUserProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphUser_StartsWith", typeof(ListUserProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphUser_Get", typeof(GetUserProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphUser_SignedIn", typeof(GetSignedInUserProcessor_Orchestrator))]
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
    public void CliTable_ListParameters_ShouldExposeUserParitySurface()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user list", ClientType.CLI);

        Assert.Equal(9, metadata.Parameters.Count);
        foreach (var expected in new[] { "display-name", "filter", "upn", "search", "consistency-level" })
        {
            Assert.True(metadata.Parameters.ContainsKey(expected), $"missing '{expected}' parameter");
            Assert.False(metadata.Parameters[expected].IsRequired, $"'{expected}' should be optional");
            Assert.Equal(typeof(string), metadata.Parameters[expected].ParameterType);
        }
        foreach (var expected in new[] { "select", "expand", "orderby" })
        {
            Assert.True(metadata.Parameters.ContainsKey(expected), $"missing '{expected}' parameter");
            Assert.False(metadata.Parameters[expected].IsRequired, $"'{expected}' should be optional");
            Assert.Equal(typeof(string[]), metadata.Parameters[expected].ParameterType);
        }
        Assert.True(metadata.Parameters.ContainsKey("append-selected"));
        Assert.False(metadata.Parameters["append-selected"].IsRequired);
        Assert.Equal(typeof(bool), metadata.Parameters["append-selected"].ParameterType);
    }

    [Fact]
    public void CliTable_ListDisplayName_ShouldMapToPrefixSemantics()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user list", ClientType.CLI);

        Assert.Equal("DisplayNamePrefix", metadata.Parameters["display-name"].Name);
    }

    [Fact]
    public void CliTable_ListConsistencyLevel_ShouldMapToProcessorConsistencyLevel()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user list", ClientType.CLI);

        Assert.Equal("ConsistencyLevel", metadata.Parameters["consistency-level"].Name);
    }

    [Fact]
    public void CliTable_SignedInParameters_ShouldExposeProjectionSurface()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user signed-in", ClientType.CLI);

        Assert.Equal(3, metadata.Parameters.Count);
        Assert.Equal(typeof(string[]), metadata.Parameters["select"].ParameterType);
        Assert.Equal(typeof(string[]), metadata.Parameters["expand"].ParameterType);
        Assert.Equal(typeof(bool), metadata.Parameters["append-selected"].ParameterType);
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
        // Select/Expand/AppendSelected are the (All) projection params — must appear on every set.
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
        // Orderby/Search/ConsistencyLevel are List-only.
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

    // ── Group commands ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("msgraph group show", typeof(GetGroupProcessor_Orchestrator))]
    [InlineData("msgraph group list", typeof(ListGroupProcessor_Orchestrator))]
    [InlineData("msgraph group member list", typeof(ListGroupMemberProcessor_Orchestrator))]
    [InlineData("msgraph group member check", typeof(CheckGroupMemberProcessor_Orchestrator))]
    public void CliTable_ShouldExposeMsGraphGroupCommands(string key, Type expectedProcessor)
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey(key), $"missing CLI dispatch key '{key}'");
        Assert.Equal(expectedProcessor, table[key].ProcessorType);
    }

    [Theory]
    [InlineData("Get-AzMsGraphGroup_Get", typeof(GetGroupProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphGroup_List", typeof(ListGroupProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphGroup_DisplayName", typeof(ListGroupProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphGroup_DisplayNameStartsWith", typeof(ListGroupProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphGroupMember_ObjectId", typeof(ListGroupMemberProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphGroupMember_DisplayName", typeof(ListGroupMemberProcessor_Orchestrator))]
    public void PsTable_ShouldRouteGroupCommands(string key, Type expectedProcessor)
    {
        var table = s_index.GetCommandTable(ClientType.PowerShell);

        Assert.True(table.ContainsKey(key), $"missing PS dispatch key '{key}'");
        Assert.Equal(expectedProcessor, table[key].ProcessorType);
    }

    [Fact]
    public void CliTable_GroupShowGroupParameter_ShouldBeRequiredWithGShortAlias()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group show", ClientType.CLI);

        Assert.True(metadata.Parameters.ContainsKey("group"));
        ParameterMetadata group = metadata.Parameters["group"];
        Assert.Equal("Group", group.Name);
        Assert.True(group.IsRequired);
        Assert.Equal(typeof(string), group.ParameterType);
        Assert.Contains("g", group.Aliases);
    }

    [Fact]
    public void CliTable_GroupListParameters_ShouldExposeGroupParitySurface()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group list", ClientType.CLI);

        Assert.Equal(8, metadata.Parameters.Count);
        foreach (var expected in new[] { "display-name", "filter", "search", "consistency-level" })
        {
            Assert.True(metadata.Parameters.ContainsKey(expected), $"missing '{expected}' parameter");
            Assert.False(metadata.Parameters[expected].IsRequired, $"'{expected}' should be optional");
            Assert.Equal(typeof(string), metadata.Parameters[expected].ParameterType);
        }
        foreach (var expected in new[] { "select", "expand", "orderby" })
        {
            Assert.True(metadata.Parameters.ContainsKey(expected), $"missing '{expected}' parameter");
            Assert.False(metadata.Parameters[expected].IsRequired, $"'{expected}' should be optional");
            Assert.Equal(typeof(string[]), metadata.Parameters[expected].ParameterType);
        }
        Assert.True(metadata.Parameters.ContainsKey("append-selected"));
        Assert.False(metadata.Parameters["append-selected"].IsRequired);
        Assert.Equal(typeof(bool), metadata.Parameters["append-selected"].ParameterType);
    }

    [Fact]
    public void CliTable_GroupListDisplayName_ShouldMapToPrefixSemantics()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group list", ClientType.CLI);

        Assert.Equal("DisplayNamePrefix", metadata.Parameters["display-name"].Name);
    }

    [Fact]
    public void CliTable_GroupListConsistencyLevel_ShouldMapToProcessorConsistencyLevel()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group list", ClientType.CLI);

        Assert.Equal("ConsistencyLevel", metadata.Parameters["consistency-level"].Name);
    }

    [Fact]
    public void CliTable_GroupMemberList_ShouldExposeParityParametersAndAlternateGroupSelectors()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group member list", ClientType.CLI);

        Assert.True(metadata.Parameters.ContainsKey("group"));
        Assert.True(metadata.Parameters.ContainsKey("display-name"));
        Assert.True(metadata.Parameters.ContainsKey("filter"));
        Assert.True(metadata.Parameters.ContainsKey("select"));
        Assert.True(metadata.Parameters.ContainsKey("expand"));
        Assert.True(metadata.Parameters.ContainsKey("orderby"));
        Assert.True(metadata.Parameters.ContainsKey("search"));
        Assert.True(metadata.Parameters.ContainsKey("consistency-level"));

        ParameterMetadata group = metadata.Parameters["group"];
        Assert.Equal("Group", group.Name);
        Assert.False(group.IsRequired);
        Assert.Contains("g", group.Aliases);
        Assert.Equal(typeof(string), metadata.Parameters["display-name"].ParameterType);
        Assert.Equal("GroupDisplayName", metadata.Parameters["display-name"].Name);
        Assert.NotNull(metadata.ParameterSets);
        Assert.True(metadata.ParameterSets!.ContainsKey("ByGroup"));
        Assert.True(metadata.ParameterSets.ContainsKey("ByDisplayName"));
    }

    [Fact]
    public void CliTable_GroupMemberCheck_ShouldRequireBothIdsWithGAlias()
    {
        var metadata = s_index.GetCommandMetadata("msgraph group member check", ClientType.CLI);

        Assert.Equal(2, metadata.Parameters.Count);
        Assert.True(metadata.Parameters["group"].IsRequired);
        Assert.Contains("g", metadata.Parameters["group"].Aliases);
        Assert.True(metadata.Parameters["member-id"].IsRequired);
    }

    [Theory]
    [InlineData("Get-AzMsGraphGroup_Get", "ObjectId")]
    [InlineData("Get-AzMsGraphGroup_DisplayName", "DisplayName")]
    [InlineData("Get-AzMsGraphGroup_DisplayNameStartsWith", "DisplayNameStartsWith")]
    public void PsTable_GroupConvenienceSets_ShouldRequireTheirDiscriminator(string psKey, string requiredParameter)
    {
        var metadata = s_index.GetCommandMetadata(psKey, ClientType.PowerShell);

        Assert.True(metadata.Parameters.ContainsKey(requiredParameter), $"'{psKey}' missing '{requiredParameter}'");
        Assert.True(metadata.Parameters[requiredParameter].IsRequired, $"'{psKey}': '{requiredParameter}' should be required");
    }

    [Fact]
    public void PsTable_GetAzMsGraphGroup_ObjectId_HasIdAlias()
    {
        var metadata = s_index.GetCommandMetadata("Get-AzMsGraphGroup_Get", ClientType.PowerShell);

        Assert.Contains("Id", metadata.Parameters["ObjectId"].Aliases);
    }

    [Fact]
    public void PsTable_GetAzMsGraphGroup_StartsWith_HasSearchStringAlias()
    {
        var metadata = s_index.GetCommandMetadata("Get-AzMsGraphGroup_DisplayNameStartsWith", ClientType.PowerShell);

        Assert.Contains("SearchString", metadata.Parameters["DisplayNameStartsWith"].Aliases);
    }

    [Fact]
    public void PsTable_GetAzMsGraphGroupMember_ObjectId_HasIdAndObjectIdAliases()
    {
        var metadata = s_index.GetCommandMetadata("Get-AzMsGraphGroupMember_ObjectId", ClientType.PowerShell);

        var aliases = metadata.Parameters["GroupObjectId"].Aliases;
        Assert.Contains("Id", aliases);
        Assert.Contains("ObjectId", aliases);
    }

    // ── Application commands ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("msgraph app create", typeof(NewApplicationProcessor))]
    [InlineData("msgraph app show", typeof(GetApplicationProcessor))]
    [InlineData("msgraph app delete", typeof(DeleteApplicationProcessor))]
    [InlineData("msgraph app list", typeof(ListApplicationProcessor_Orchestrator))]
    public void CliTable_ShouldExposeMsGraphApplicationCommands(string key, Type expectedProcessor)
    {
        var table = s_index.GetCommandTable(ClientType.CLI);

        Assert.True(table.ContainsKey(key), $"missing CLI dispatch key '{key}'");
        Assert.Equal(expectedProcessor, table[key].ProcessorType);
    }

    [Theory]
    [InlineData("New-AzMsGraphApplication", typeof(NewApplicationProcessor))]
    [InlineData("Get-AzMsGraphApplication_Get", typeof(GetApplicationProcessor))]
    [InlineData("Get-AzMsGraphApplication_List", typeof(ListApplicationProcessor_Orchestrator))]
    [InlineData("Get-AzMsGraphApplication_DisplayName", typeof(ListApplicationProcessor_Orchestrator))]
    [InlineData("Remove-AzMsGraphApplication", typeof(DeleteApplicationProcessor))]
    public void PsTable_ShouldRouteApplicationCommands(string key, Type expectedProcessor)
    {
        var table = s_index.GetCommandTable(ClientType.PowerShell);

        Assert.True(table.ContainsKey(key), $"missing PS dispatch key '{key}'");
        Assert.Equal(expectedProcessor, table[key].ProcessorType);
    }

    [Fact]
    public void CliTable_AppListDisplayName_ShouldMapToExactSemantics()
    {
        var metadata = s_index.GetCommandMetadata("msgraph app list", ClientType.CLI);

        Assert.Equal("DisplayName", metadata.Parameters["display-name"].Name);
    }
}
