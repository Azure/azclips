// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Cirrus.Extensions.Graph;
using Microsoft.Azure.Cirrus.Extensions.Graph.Processors;

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
    public void PsTable_ShouldExposeGetAzMsGraphUser()
    {
        var table = s_index.GetCommandTable(ClientType.PowerShell);

        Assert.True(table.ContainsKey("Get-AzMsGraphUser"));
        Assert.Equal(typeof(GetUserProcessor), table["Get-AzMsGraphUser"].ProcessorType);
    }

    [Fact]
    public void CliTable_IdParameter_ShouldBeRequiredKebabCase()
    {
        var metadata = s_index.GetCommandMetadata("msgraph user show", ClientType.CLI);

        Assert.True(metadata.Parameters.ContainsKey("id"));
        ParameterMetadata id = metadata.Parameters["id"];
        Assert.Equal("Id", id.Name);
        Assert.True(id.IsRequired);
        Assert.Equal(typeof(string), id.ParameterType);
    }

    [Fact]
    public void GetCommandMetadata_UnknownCommand_ShouldThrow()
    {
        Assert.Throws<KeyNotFoundException>(() => s_index.GetCommandMetadata("msgraph not-a-real-command", ClientType.CLI));
    }
}
