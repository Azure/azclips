using System.Reflection;
using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.KeyVault;

namespace Extensions.KeyVault.Tests;

public class CompatibilityTests
{
    [Fact]
    public void FoundationDeclaresTheCurrentHostCompatibilityContract()
    {
        var compatibility = typeof(ExtensionIndex).Assembly.GetCustomAttribute<AzclipsCompatibilityAttribute>();

        Assert.NotNull(compatibility);
        Assert.Equal("1.0.0", compatibility.MinCoreVersion);
        Assert.Null(compatibility.MaxCoreVersion);
    }

    [Theory]
    [InlineData("0.1.3", false)]
    [InlineData("0.9.0", false)]
    [InlineData("1.0.0-preview.1", false)]
    [InlineData("1.0.0", true)]
    [InlineData("1.0.0+build.1", true)]
    [InlineData("1.1.0", true)]
    public void ExtensionMetadataEnforcesAnInclusiveMinimum(string currentCoreVersion, bool expectedCompatible)
    {
        var directory = Directory.CreateTempSubdirectory("azclips-keyvault-compatibility-");
        try
        {
            var assemblyPath = typeof(ExtensionIndex).Assembly.Location;
            File.Copy(assemblyPath, Path.Combine(directory.FullName, Path.GetFileName(assemblyPath)));

            var result = CompatibilityGuard.CheckAssembly(directory.FullName, currentCoreVersion);

            Assert.Equal(expectedCompatible, result.IsCompatible);
            if (expectedCompatible)
                Assert.Null(result.Reason);
            else
                Assert.Contains("requires core >= 1.0.0", result.Reason);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}