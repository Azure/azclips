// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Tests.Common.Utilities;

/// <summary>
/// Stages a locally built extension into an extensions root exactly as <c>extension add</c> would,
/// but offline (copies loose DLLs instead of extracting a feed package). Copies the payload into
/// <c>&lt;extensionsRoot&gt;/&lt;packageId&gt;/&lt;version&gt;/</c> and writes a faithful registry
/// entry whose verbs and (verb, path) contributions are derived from the staged DLLs via the same
/// readers <c>extension add</c> uses. Load-modify-save preserves any pre-existing registry entries.
/// </summary>
/// <remarks>
/// Test/benchmark-only helper: lives in Tests.Common (not production <c>src/</c>) because the only
/// consumers are the benchmark harness and its unit test.
/// </remarks>
public static class LocalExtensionStager
{
    public static void Stage(string packageId, string version, string sourceBuildDir, string extensionsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionsRoot);

        if (!Directory.Exists(sourceBuildDir))
        {
            throw new DirectoryNotFoundException($"Source build directory not found: {sourceBuildDir}");
        }

        var stagedDir = Path.Combine(extensionsRoot, packageId, version);
        if (Directory.Exists(stagedDir))
        {
            Directory.Delete(stagedDir, recursive: true);
        }
        Directory.CreateDirectory(stagedDir);

        foreach (var dll in Directory.EnumerateFiles(sourceBuildDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            File.Copy(dll, Path.Combine(stagedDir, Path.GetFileName(dll)), overwrite: true);
        }

        var verbs = ExtensionVerbReader.ReadVerbs(stagedDir);
        if (verbs.Count == 0)
        {
            throw new InvalidOperationException(
                $"Extension '{packageId}' declares no [ExtensionVerb]; cannot stage it.");
        }

        var leafKeys = LeafReader.ReadCliLeafKeys(stagedDir);
        ReleaseCollectibleLoadContextFileHandles();

        var store = new FileExtensionRegistryStore(Path.Combine(extensionsRoot, "registry.json"));
        var registry = store.Load();
        var contributions = ExtensionAddPlanner.PlanContributions(packageId, verbs, leafKeys, registry);

        registry.Extensions[packageId] = new ExtensionRegistryEntry
        {
            Verb = verbs[0],
            Version = version,
            Contributions = contributions.ToList(),
            InstalledAt = DateTimeOffset.UtcNow,
        };

        store.Save(registry);
    }

    private static void ReleaseCollectibleLoadContextFileHandles()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
