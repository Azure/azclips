// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using Xunit;

namespace Core.Extensions.Tests;

public class AssemblyVersionReaderTests
{
    [Fact]
    public void TryReadInformationalVersion_ShouldReturnStrippedVersion_ForRealAssembly()
    {
        // The test assembly itself carries AssemblyInformationalVersion via the SDK's default
        // GenerateAssemblyInfo path; reader must return the SemVer core (no +sha suffix).
        var asm = typeof(AssemblyVersionReaderTests).Assembly;
        var dir = Path.GetDirectoryName(asm.Location)!;
        var packageId = Path.GetFileNameWithoutExtension(asm.Location);

        var version = AssemblyVersionReader.TryReadInformationalVersion(dir, packageId);

        Assert.False(string.IsNullOrEmpty(version));
        Assert.DoesNotContain('+', version);
    }

    [Fact]
    public void TryReadInformationalVersion_ShouldReturnEmpty_WhenPackageDirectoryMissing()
    {
        var version = AssemblyVersionReader.TryReadInformationalVersion(
            Path.Combine(Path.GetTempPath(), "azclips-avr-missing-" + Guid.NewGuid()),
            "Foo");

        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void TryReadInformationalVersion_ShouldReturnEmpty_WhenDllMissing()
    {
        var version = AssemblyVersionReader.TryReadInformationalVersion(
            Path.GetTempPath(),
            "Nonexistent.Package.Id." + Guid.NewGuid());

        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void TryReadInformationalVersion_ShouldReturnEmpty_WhenDllUnreadable()
    {
        // Empty file at the expected DLL path — MetadataLoadContext throws BadImageFormat; the
        // reader swallows it and returns empty rather than propagating the failure to callers.
        var dir = Path.Combine(Path.GetTempPath(), "azclips-avr-empty-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var packageId = "Empty.Package";
        try
        {
            File.WriteAllText(Path.Combine(dir, packageId + ".dll"), string.Empty);

            var version = AssemblyVersionReader.TryReadInformationalVersion(dir, packageId);

            Assert.Equal(string.Empty, version);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
