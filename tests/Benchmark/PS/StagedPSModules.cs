// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Benchmark.PS;

/// <summary>
/// Resolves the PowerShell modules the build stages under
/// <c>artifacts/&lt;Configuration&gt;/PS/&lt;Module&gt;</c>. Each staged module is a self-contained,
/// importable folder, so benchmarks import it directly instead of rewriting manifests.
/// </summary>
internal static class StagedPSModules
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <summary>
    /// The absolute path to a staged module's manifest.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="moduleName">The module to resolve, e.g. <c>Resources</c>.</param>
    public static string ManifestPath(string repoRoot, string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("A module name is required.", nameof(moduleName));
        }

        var manifest = Path.Combine(
            repoRoot, "artifacts", BuildConfiguration, "PS", moduleName, $"{moduleName}.psd1");
        if (!File.Exists(manifest))
        {
            throw new FileNotFoundException(
                $"Staged PowerShell module manifest not found: '{manifest}'. " +
                $"Run `dotnet build azclips.slnx -c {BuildConfiguration}` first.",
                manifest);
        }

        return manifest;
    }
}
