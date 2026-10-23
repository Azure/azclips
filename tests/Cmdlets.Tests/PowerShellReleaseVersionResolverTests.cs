// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Cmdlets.Accounts;

namespace Cmdlets.Tests;

public sealed class PowerShellReleaseVersionResolverTests : IDisposable
{
    private const string RollupGuid = "7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5";
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"azclips-release-{Guid.NewGuid():N}");
    private string AssemblyDirectory => Path.Combine(_root, "build", "Debug");
    private string ModuleRoot => Path.Combine(_root, "modules");

    private static void WriteManifest(string path, string guid, string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"@{{\nGUID = '{guid}'\nModuleVersion = '{version}'\n}}");
    }

    [Fact]
    public void MissingRollup_UsesStandaloneVersion()
    {
        Assert.Equal("0.0.0", PowerShellReleaseVersionResolver.GetReleaseVersion(AssemblyDirectory, ModuleRoot));
    }

    [Fact]
    public void InstalledRollup_UsesLatestVersionAndIgnoresUnrelatedAz()
    {
        WriteManifest(Path.Combine(ModuleRoot, "Az", "0.9.0", "Az.psd1"), Guid.NewGuid().ToString(), "0.9.0");
        WriteManifest(Path.Combine(ModuleRoot, "Az", "0.1.5", "Az.psd1"), RollupGuid, "0.1.5");
        WriteManifest(Path.Combine(ModuleRoot, "Az", "0.1.6", "Az.psd1"), RollupGuid, "0.1.6");

        Assert.Equal("0.1.6", PowerShellReleaseVersionResolver.GetReleaseVersion(AssemblyDirectory, ModuleRoot));
    }

    [Fact]
    public void LocalRollup_TakesPrecedenceOverInstalledRollup()
    {
        WriteManifest(Path.Combine(_root, "src", "PS", "Az", "Az.psd1"), RollupGuid, "0.1.5");
        WriteManifest(Path.Combine(ModuleRoot, "Az", "0.9.0", "Az.psd1"), RollupGuid, "0.9.0");

        Assert.Equal("0.1.5", PowerShellReleaseVersionResolver.GetReleaseVersion(AssemblyDirectory, ModuleRoot));
    }

    [Fact]
    public void InvalidRollupVersion_IsReported()
    {
        var path = Path.Combine(ModuleRoot, "Az", "Az.psd1");
        WriteManifest(path, RollupGuid, "not-a-version");

        Assert.Contains(path, Assert.Throws<InvalidDataException>(() =>
            PowerShellReleaseVersionResolver.GetReleaseVersion(AssemblyDirectory, ModuleRoot)).Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
