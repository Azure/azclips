using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class ExtensionIndexTests
{
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void FoundationExposesNoCommands(ClientType clientType)
    {
        var index = new ExtensionIndex();

        Assert.Empty(index.GetCommandTable(clientType));
        Assert.Empty(index.GetAllCommands(clientType));
        Assert.Empty(index.GetCommandKeys(clientType));
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("not-implemented", clientType));
    }
}