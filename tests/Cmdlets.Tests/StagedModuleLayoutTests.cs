// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Tests.Common.Utilities;

namespace Cmdlets.Tests;

/// <summary>
/// The build stages every PowerShell module as a self-contained folder under
/// artifacts/&lt;Configuration&gt;/PS, and the source manifests address their payload by
/// module-relative paths so the same manifest works in any configuration.
/// </summary>
public class StagedModuleLayoutTests
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    // Every quoted assembly reference in a manifest (RootModule, NestedModules, RequiredAssemblies).
    private static readonly Regex AssemblyReference = new(@"'(?<path>[^']+\.dll)'", RegexOptions.Compiled);

    private static string RepoRoot => RepoLocator.GetRepoRoot();

    [Fact]
    public void SourceManifests_DoNotReferenceAConfigurationSpecificArtifactPath()
    {
        var manifests = Directory.GetFiles(Path.Combine(RepoRoot, "src", "PS"), "*.psd1", SearchOption.AllDirectories);
        Assert.NotEmpty(manifests);

        foreach (var manifest in manifests)
        {
            var content = File.ReadAllText(manifest).Replace('\\', '/');
            Assert.DoesNotContain("artifacts/Debug", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("artifacts/Release", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Only the modules this test project builds are staged by a `dotnet test` of it alone.
    [Theory]
    [InlineData("Accounts")]
    [InlineData("Config")]
    public void StagedModule_ContainsEveryAssemblyItsManifestReferences(string moduleName)
    {
        var stagedModuleDir = Path.Combine(RepoRoot, "artifacts", BuildConfiguration, "PS", moduleName);
        var stagedManifest = Path.Combine(stagedModuleDir, $"{moduleName}.psd1");
        Assert.True(File.Exists(stagedManifest), $"Module manifest was not staged: {stagedManifest}");

        var references = AssemblyReference.Matches(File.ReadAllText(stagedManifest))
            .Select(match => match.Groups["path"].Value)
            .ToArray();
        Assert.NotEmpty(references);

        foreach (var reference in references)
        {
            Assert.False(Path.IsPathRooted(reference), $"{moduleName} references an absolute path: {reference}");
            var resolved = Path.Combine(stagedModuleDir, reference);
            Assert.True(File.Exists(resolved), $"{moduleName} references a missing assembly: {reference}");
        }
    }
}
