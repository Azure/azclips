// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryPropertyResolverTests
{
    [Theory]
    [InlineData("\"Ubuntu\"", "ubuntu", "Ubuntu", "ubuntu")]
    [InlineData("'Azure Linux'", "'azurelinux'", "Azure Linux", "azurelinux")]
    [InlineData("  Debian GNU/Linux  ", "  debian  ", "Debian GNU/Linux", "debian")]
    public void ResolveLinuxDistribution_ParsesOsReleaseValues(
        string nameValue,
        string idValue,
        string expectedName,
        string expectedId)
    {
        var result = TelemetryPropertyResolver.ResolveLinuxDistribution(
            true,
            () => [$"NAME={nameValue}", $"ID={idValue}"]);

        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expectedId, result.Id);
    }

    [Fact]
    public void ResolveLinuxDistribution_ReturnsAvailableFields()
    {
        var result = TelemetryPropertyResolver.ResolveLinuxDistribution(
            true,
            () => ["NAME=\"Ubuntu\"", "VERSION_ID=\"24.04\""]);

        Assert.Equal("Ubuntu", result.Name);
        Assert.Null(result.Id);
    }

    [Fact]
    public void ResolveLinuxDistribution_ReturnsDefault_WhenValuesAreEmpty()
    {
        var result = TelemetryPropertyResolver.ResolveLinuxDistribution(
            true,
            () => ["NAME=\"\"", "ID='  '"]);

        Assert.Null(result.Name);
        Assert.Null(result.Id);
    }

    [Fact]
    public void ResolveLinuxDistribution_DoesNotReadFile_WhenNotLinux()
    {
        var read = false;

        var result = TelemetryPropertyResolver.ResolveLinuxDistribution(
            false,
            () =>
            {
                read = true;
                return ["NAME=Ubuntu", "ID=ubuntu"];
            });

        Assert.False(read);
        Assert.Null(result.Name);
        Assert.Null(result.Id);
    }

    [Fact]
    public void ResolveLinuxDistribution_ReturnsDefault_WhenFileReadFails()
    {
        var result = TelemetryPropertyResolver.ResolveLinuxDistribution(
            true,
            () => throw new IOException("Unavailable test file."));

        Assert.Null(result.Name);
        Assert.Null(result.Id);
    }
}
