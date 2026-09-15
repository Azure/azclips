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
    // Relative artifact references embedded in the source manifests. Both Debug and
    // Release source forms are accepted and rewritten to the Release staging target.
    private static readonly string[] SourceArtifactPrefixes =
    {
        "../../../artifacts/Debug/",
        "../../../artifacts/Release/",
    };

    // Matches a RequiredModules entry that references a sibling manifest by relative path,
    // e.g. ModuleName = '../Accounts/Accounts.psd1' — capturing the module (directory) name.
    private static readonly Regex RequiredModuleReference = new(
        @"ModuleName\s*=\s*'(?:\.\./)+(?<name>[^'/]+)/[^']*\.psd1'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches a RequiredModules entry that references a sibling module by bare name,
    // e.g. ModuleName = 'Login'.
    private static readonly Regex NamedModuleReference = new(
        @"ModuleName\s*=\s*'(?<name>[A-Za-z][\w.]*)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Stages a single module's manifest — plus the transitive <c>RequiredModules</c> closure
    /// it depends on — into <c>artifacts/Benchmark/PS</c>, with artifact references rewritten to
    /// absolute Release paths and the manifests otherwise left intact.
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
        private readonly string _stagingRoot;
        private readonly string _absReleaseArtifacts;
        private readonly HashSet<string> _staged = new(StringComparer.OrdinalIgnoreCase);

        public StagingContext(string repoRoot)
        {
            _sourceRoot = Path.Combine(repoRoot, "src", "PS");
            _stagingRoot = Path.Combine(repoRoot, "artifacts", "Benchmark", "PS");

            // Absolute path to artifacts/Release/ with forward slashes (psd1-friendly on all
            // platforms; PowerShell accepts forward slashes on Windows too).
            _absReleaseArtifacts = Path.Combine(repoRoot, "artifacts", "Release").Replace('\\', '/') + "/";
        }

        // The absolute path to a module's staged manifest, whether or not it has been staged yet.
        public string StagedManifestPath(string moduleName) =>
            Path.Combine(_stagingRoot, moduleName, $"{moduleName}.psd1");

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

            var manifestContent = StageManifest(moduleName, sourceManifest);

            foreach (Match reference in RequiredModuleReference.Matches(manifestContent))
            {
                StageClosure(reference.Groups["name"].Value);
            }
            foreach (Match reference in NamedModuleReference.Matches(manifestContent))
            {
                StageClosure(reference.Groups["name"].Value);
            }
        }

        // Copies a single manifest into the staging tree with artifact references rewritten to
        // absolute Release paths. Returns the manifest's (source) content so the caller can
        // inspect its RequiredModules.
        private string StageManifest(string moduleName, string sourceManifest)
        {
            var targetDir = Path.Combine(_stagingRoot, moduleName);
            Directory.CreateDirectory(targetDir);

            var content = File.ReadAllText(sourceManifest);
            var rewritten = content;
            foreach (var prefix in SourceArtifactPrefixes)
            {
                rewritten = rewritten.Replace(prefix, _absReleaseArtifacts);
            }

            // A bare-name requirement only resolves against loaded modules or PSModulePath;
            // the staged tree is neither, so point it at the staged sibling manifest, which
            // PowerShell auto-imports.
            rewritten = NamedModuleReference.Replace(
                rewritten, m => $"ModuleName = '../{m.Groups["name"].Value}/{m.Groups["name"].Value}.psd1'");

            File.WriteAllText(Path.Combine(targetDir, $"{moduleName}.psd1"), rewritten);
            return content;
        }
    }
}
