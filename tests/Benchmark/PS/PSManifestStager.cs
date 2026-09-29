// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Benchmark.PS;

/// <summary>
/// Stages PowerShell module manifests for benchmark consumption.
/// </summary>
internal static class PSManifestStager
{
    // Matches a RequiredModules entry that references a sibling manifest by relative path,
    // e.g. ModuleName = '../Microsoft.Azure.Azclips.Extensions.Accounts/Accounts.psd1'.
    private static readonly Regex RequiredModuleReference = new(
        @"ModuleName\s*=\s*'[^']*/(?<name>[^'/]+)\.psd1'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches a RequiredModules entry that references a sibling module by bare name,
    // e.g. ModuleName = 'Login'.
    private static readonly Regex NamedModuleReference = new(
        @"ModuleName\s*=\s*'(?<name>[A-Za-z][\w.]*)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Stages a single module's complete artifact directory — plus the transitive
    /// <c>RequiredModules</c> closure it depends on — into <c>artifacts/Benchmark/PS</c>.
    ///
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="moduleName">The module to stage, e.g. <c>Resources</c>.</param>
    /// <returns>The absolute path to the staged <c>&lt;moduleName&gt;.psd1</c>.</returns>
    public static string StageModule(string repoRoot, string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("A module name is required.", nameof(moduleName));
        }

        var staging = new StagingContext(repoRoot);
        staging.StageClosure(moduleName);
        return staging.StagedManifestPath(moduleName);
    }

    // Holds the immutable roots for one staging run and tracks which modules have already been
    // staged, so the recursive closure walk only needs to pass a module name around.
    private sealed class StagingContext
    {
        private readonly string _sourceRoot;
        private readonly string _releaseArtifactsRoot;
        private readonly string _stagingRoot;
        private readonly HashSet<string> _staged = new(StringComparer.OrdinalIgnoreCase);

        public StagingContext(string repoRoot)
        {
            _sourceRoot = Path.Combine(repoRoot, "src", "PS");
            _releaseArtifactsRoot = Path.Combine(repoRoot, "artifacts", "Release");
            _stagingRoot = Path.Combine(repoRoot, "artifacts", "Benchmark", "PS");
        }

        // The absolute path to a module's staged manifest, whether or not it has been staged yet.
        public string StagedManifestPath(string moduleName) =>
            Path.Combine(_stagingRoot, ArtifactName(moduleName), $"{moduleName}.psd1");

        // Recursively stages a module and every sibling it lists in RequiredModules.
        public void StageClosure(string moduleName)
        {
            if (!_staged.Add(moduleName))
            {
                return; // already staged (guards against duplicate / cyclic references)
            }

            var sourceManifest = Path.Combine(_sourceRoot, moduleName, $"{moduleName}.psd1");
            if (!File.Exists(sourceManifest))
            {
                throw new FileNotFoundException(
                    $"PowerShell module manifest not found: '{sourceManifest}'. " +
                    $"Expected src/PS/{moduleName}/{moduleName}.psd1.",
                    sourceManifest);
            }

            StageArtifact(moduleName);
            var manifestContent = File.ReadAllText(sourceManifest);

            foreach (Match reference in RequiredModuleReference.Matches(manifestContent))
            {
                StageClosure(reference.Groups["name"].Value);
            }
            foreach (Match reference in NamedModuleReference.Matches(manifestContent))
            {
                StageClosure(reference.Groups["name"].Value);
            }
        }

        private void StageArtifact(string moduleName)
        {
            var artifactName = ArtifactName(moduleName);
            var sourceDir = Path.Combine(_releaseArtifactsRoot, artifactName);
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException(
                    $"PowerShell module artifact directory not found: '{sourceDir}'. Build Release artifacts first.");
            }

            var targetDir = Path.Combine(_stagingRoot, artifactName);
            Directory.CreateDirectory(targetDir);
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var targetFile = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }

        private static string ArtifactName(string moduleName) =>
            $"Microsoft.Azure.Azclips.Extensions.{moduleName}";
    }
}
