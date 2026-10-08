using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class ExtensionIndexTests
{
    [Theory]
    [InlineData(ClientType.CLI, 8)]
    [InlineData(ClientType.PowerShell, 9)]
    public void FrontendsExposeRequestedSecretAndCertificateOperations(ClientType clientType, int certificateCount)
    {
        var index = new ExtensionIndex();

        Assert.Equal(6 + certificateCount, index.GetCommandTable(clientType).Count);
        Assert.Equal(6, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Secret")));
        Assert.Equal(certificateCount, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Certificate")));
        Assert.All(index.GetAllCommands(clientType), metadata =>
        {
            Assert.Contains(".Customization", metadata.ProcessorType.Namespace!);
            foreach (var parameter in metadata.Parameters.Values)
                Assert.NotNull(metadata.ProcessorType.GetProperty(parameter.Name));
        });
        Assert.Equal(6 + certificateCount, index.GetCommandKeys(clientType).Count());
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("not-implemented", clientType));
    }

    [Fact]
    public void CertificateRecoveryIsOnlyExposedThroughPowerShell()
    {
        var index = new ExtensionIndex();
        var command = index.GetCommandMetadata("Undo-AzKeyVaultCertificateRemoval", ClientType.PowerShell);

        Assert.Equal(typeof(Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.RecoverCertificateCommand), command.ProcessorType);
        Assert.True(command.Parameters["Name"].IsRequired);
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("keyvault certificate recover", ClientType.CLI));
    }
}