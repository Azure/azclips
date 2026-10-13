using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class ExtensionIndexTests
{
    [Theory]
    [InlineData(ClientType.CLI, 6)]
    [InlineData(ClientType.PowerShell, 7)]
    public void FrontendsExposeRequestedCustomizedOperations(ClientType clientType, int expectedCount)
    {
        var index = new ExtensionIndex();

        Assert.Equal(expectedCount, index.GetCommandTable(clientType).Count);
        Assert.All(index.GetAllCommands(clientType), metadata =>
        {
            Assert.Contains(".Customization", metadata.ProcessorType.Namespace!);
            foreach (var parameter in metadata.Parameters.Values)
                Assert.NotNull(metadata.ProcessorType.GetProperty(parameter.Name));
        });
        Assert.Equal(expectedCount, index.GetCommandKeys(clientType).Count());
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("not-implemented", clientType));
    }

    [Fact]
    public void RecoveryIsOnlyExposedThroughTheRequestedPowerShellCommand()
    {
        var index = new ExtensionIndex();

        var command = index.GetCommandMetadata("Undo-AzKeyVaultSecretRemoval", ClientType.PowerShell);

        Assert.Equal(typeof(Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.RecoverSecretCommand), command.ProcessorType);
        Assert.True(command.Parameters["Name"].IsRequired);
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("keyvault secret recover", ClientType.CLI));
    }
}