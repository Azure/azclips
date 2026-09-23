using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class ExtensionIndexTests
{
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void BothFrontendsExposeOnlySixSecretAndEightCertificateOperations(ClientType clientType)
    {
        var index = new ExtensionIndex();

        Assert.Equal(14, index.GetCommandTable(clientType).Count);
        Assert.Equal(6, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Secret")));
        Assert.Equal(8, index.GetAllCommands(clientType).Count(metadata => metadata.ProcessorType.Name.Contains("Certificate")));
        Assert.All(index.GetAllCommands(clientType), metadata =>
        {
            Assert.Contains(".Customization", metadata.ProcessorType.Namespace!);
            foreach (var parameter in metadata.Parameters.Values)
                Assert.NotNull(metadata.ProcessorType.GetProperty(parameter.Name));
        });
        Assert.Equal(14, index.GetCommandKeys(clientType).Count());
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("not-implemented", clientType));
    }
}