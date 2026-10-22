using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class ExtensionIndexTests
{
    [Theory]
    [InlineData(ClientType.CLI, 6, 8)]
    [InlineData(ClientType.PowerShell, 7, 9)]
    public void FrontendsExposeRequestedSecretAndCertificateOperations(ClientType clientType, int secretCount, int certificateCount)
    {
        var index = new ExtensionIndex();

        Assert.Equal(secretCount + certificateCount, index.GetCommandTable(clientType).Count);
        Assert.Equal(secretCount, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Secret")));
        Assert.Equal(certificateCount, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Certificate")));
        Assert.All(index.GetAllCommands(clientType), metadata =>
        {
            Assert.Contains(".Customization", metadata.ProcessorType.Namespace!);
            foreach (var parameter in metadata.Parameters.Values)
                Assert.NotNull(metadata.ProcessorType.GetProperty(parameter.Name));
        });
        Assert.Equal(secretCount + certificateCount, index.GetCommandKeys(clientType).Count());
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

    [Fact]
    public void SecretRecoveryIsOnlyExposedThroughPowerShell()
    {
        var index = new ExtensionIndex();
        var command = index.GetCommandMetadata("Undo-AzKeyVaultSecretRemoval", ClientType.PowerShell);

        Assert.Equal(typeof(Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.RecoverSecretCommand), command.ProcessorType);
        Assert.True(command.Parameters["Name"].IsRequired);
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("keyvault secret recover", ClientType.CLI));
    }
}