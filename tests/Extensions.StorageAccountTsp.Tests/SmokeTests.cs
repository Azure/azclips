// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Smoke tests for the StorageAccountTsp extension — verifies the full
// `az storage account` CRUD surface (+ wait) is registered for both the
// CLI and PowerShell client tables.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp;
using Xunit;

namespace Extensions.StorageAccountTsp.Tests;

public class GeneratedSmokeTests
{
    private readonly IExtensionIndex _index = new ExtensionIndex();

    [Fact]
    public void ExtensionIndex_Instantiates() => Assert.NotNull(_index);

    [Fact]
    public void ExtensionIndex_HasPsCommands()
    {
        var keys = _index.GetCommandKeys(ClientType.PowerShell);
        Assert.NotEmpty(keys);
    }

    [Fact]
    public void ExtensionIndex_HasCliCommands()
    {
        var keys = _index.GetCommandKeys(ClientType.CLI);
        Assert.NotEmpty(keys);
    }

    [Theory]
    [InlineData("storage account create")]
    [InlineData("storage account show")]
    [InlineData("storage account update")]
    [InlineData("storage account delete")]
    [InlineData("storage account list")]
    [InlineData("storage account list-by-subscription")]
    [InlineData("storage account wait")]
    public void Registers_Cli_Command(string invocationName)
    {
        var keys = _index.GetCommandKeys(ClientType.CLI);
        Assert.Contains(invocationName, keys);
    }

    [Theory]
    [InlineData("New-AzStorageAccount")]
    [InlineData("Get-AzStorageAccount_Get")]
    [InlineData("Update-AzStorageAccount")]
    [InlineData("Remove-AzStorageAccount")]
    [InlineData("Get-AzStorageAccount_List")]
    [InlineData("Get-AzStorageAccount_ListBySubscription")]
    [InlineData("Wait-AzStorageAccount")]
    public void Registers_Ps_Command(string invocationName)
    {
        var keys = _index.GetCommandKeys(ClientType.PowerShell);
        Assert.Contains(invocationName, keys);
    }

    [Fact]
    public void Cli_CommandCount_MatchesCrudScope()
    {
        var keys = _index.GetCommandKeys(ClientType.CLI);
        Assert.Equal(7, keys.Count());
    }

    [Fact]
    public void Ps_CommandCount_MatchesCrudScope()
    {
        var keys = _index.GetCommandKeys(ClientType.PowerShell);
        Assert.Equal(7, keys.Count());
    }

    [Fact]
    public void Create_RequiresCoreArmFields()
    {
        var metadata = _index.GetCommandMetadata("storage account create", ClientType.CLI);
        Assert.True(metadata.Parameters.ContainsKey("resource-group"));
        Assert.True(metadata.Parameters.ContainsKey("name"));
        Assert.True(metadata.Parameters.ContainsKey("sku-name"));
        Assert.True(metadata.Parameters.ContainsKey("kind"));
        Assert.True(metadata.Parameters.ContainsKey("location"));
        Assert.True(metadata.Parameters["resource-group"].IsRequired);
        Assert.True(metadata.Parameters["name"].IsRequired);
    }
}
