// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Xml.Linq;

using Core.Abstractions;
using Core.Extensions;

namespace Cli.Tests;

/// <summary>
/// Contract tests for <see cref="ExtensionAddPlanner"/>. Drives the planner against real fixture
/// DLLs read from disk via <see cref="LeafReader"/>, exercising the production branches that
/// decide whether two extensions sharing a top-level verb can co-exist (disjoint
/// <c>(verb, path)</c> tuples) or collide (same tuple, different package id).
/// </summary>
/// <remarks>
/// Skips <c>ExtensionInstaller</c> / NuGet extraction (no nupkg packaging, no <c>~/.azclips</c>
/// involvement) and stages compiled fixture DLLs into temp directories the test owns. The
/// planner is the same code path
/// <see cref="Microsoft.Azure.Azclips.Extensions.Extension.Processors.AddExtensionProcessor.Process()"/>
/// runs after extraction, so a passing test proves the contract end-to-end from "leaves on disk"
/// to "registry decision".
/// </remarks>
public sealed class ExtensionAddPlannerTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Fact]
    public void GeneratedSidecars_ShouldMatchEveryExtensionIndexContract()
    {
        var repoRoot = global::Tests.Common.Utilities.RepoLocator.GetRepoRoot();
        var configuration = InferConfigurationFromBaseDirectory()
            ?? throw new InvalidOperationException("Cannot infer the test build configuration.");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", configuration);
        var extensionProjects = Directory.GetFiles(
                Path.Combine(repoRoot, "src", "Extensions"), "*.csproj", SearchOption.AllDirectories)
            .Append(Path.Combine(repoRoot, "Agent", "Commands", "Agent.Commands.csproj"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in extensionProjects)
        {
            var project = XDocument.Load(projectPath);
            var packageId = project.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "PackageId")
                ?.Value.Trim();
            Assert.False(string.IsNullOrWhiteSpace(packageId), $"No PackageId found in {projectPath}");

            var extensionDirectory = Path.Combine(artifactsRoot, packageId!);
            Assert.True(
                File.Exists(Path.Combine(extensionDirectory, $"{packageId}.dll")),
                $"Missing built extension assembly for {packageId}: {extensionDirectory}");
            var sidecarPath = Path.Combine(extensionDirectory, $"{packageId}.commands.json");
            Assert.True(File.Exists(sidecarPath), $"Missing generated sidecar: {sidecarPath}");

            var sidecar = JsonDocument.Parse(File.ReadAllText(sidecarPath));
            using (sidecar)
            {
                Assert.Equal(1, sidecar.RootElement.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(packageId, sidecar.RootElement.GetProperty("packageId").GetString());
                var sidecarVerbs = sidecar.RootElement.GetProperty("verbs")
                    .EnumerateArray().Select(value => value.GetString()!).ToArray();
                var sidecarLeaves = sidecar.RootElement.GetProperty("leaves")
                    .EnumerateArray().Select(value => value.GetString()!).ToArray();
                var probeDirectories = new[]
                {
                    Path.Combine(artifactsRoot, "Core"),
                    Path.Combine(artifactsRoot, "Microsoft.Azure.Azclips.ExtensionRuntime"),
                };
                var (expectedVerbs, expectedLeaves) = LeafReader.ReadContract(
                    extensionDirectory, probeDirectories);

                Assert.Equal(
                    expectedVerbs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
                    sidecarVerbs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                Assert.Equal(
                    expectedLeaves.OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
                    sidecarLeaves.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            }
        }
    }

    public void Dispose()
    {
        foreach (var dir in _tempRoots)
        {
            try { if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); } }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    [Fact]
    public void ReadContract_ShouldReturnDeclaredVerbsAndLeaves_ForRealCompiledExtension()
    {
        // Exercises the exact method the build-time sidecar generator calls, against a real
        // compiled assembly rather than an empty-directory edge case — catches regressions in
        // the shared verb/leaf extraction logic regardless of caller (extension add vs sidecar).
        var dir = StageFixtureDll("SharedAlpha");

        var (verbs, leaves) = LeafReader.ReadContract(dir);

        Assert.Equal(new[] { "sharedverb" }, verbs);
        Assert.Equal(new[] { "sharedverb alpha" }, leaves);
    }

    [Fact]
    public void ReadContract_ShouldThrow_WhenVerbAttributeHasNoExtensionIndexImplementer()
    {
        // A type carrying [ExtensionVerb] without implementing IExtensionIndex is a malformed
        // extension, not "no CLI contract" - must throw rather than fall back to the same empty
        // tuple ExtensionRuntime (genuinely command-less) returns.
        var dir = StageFixtureDll("Malformed");

        var ex = Assert.Throws<InvalidOperationException>(() => LeafReader.ReadContract(dir));
        Assert.Contains("ExtensionIndex", ex.Message);
        Assert.Contains("IExtensionIndex", ex.Message);
    }

    [Fact]
    public void ReadContract_ShouldOnlyUseTheConventionalIndexType()
    {
        // The runtime loads only <assembly name>.ExtensionIndex (CommandLoadingExtension). A decorated
        // secondary IExtensionIndex (declared first) or non-index type must not leak into the sidecar.
        var dir = StageFixtureDll("ExtraVerb");

        var (verbs, leaves) = LeafReader.ReadContract(dir);

        Assert.Equal(new[] { "primary" }, verbs);
        Assert.Equal(new[] { "primary run" }, leaves);
    }

    [Fact]
    public void PlanContributions_SharedVerb_DisjointLeaves_BothSucceed()
    {
        // Alpha contributes `sharedverb alpha`, Beta contributes `sharedverb beta`. Same root
        // verb, different leaves — the per-leaf-ownership invariant. Plan Alpha into an empty
        // registry, commit it, then plan Beta against the registry-with-Alpha. Both should
        // produce contributions without throwing.
        var alphaDir = StageFixtureDll("SharedAlpha");
        var betaDir = StageFixtureDll("SharedBeta");

        var alphaLeaves = LeafReader.ReadCliLeafKeys(alphaDir);
        var betaLeaves = LeafReader.ReadCliLeafKeys(betaDir);

        Assert.Equal(new[] { "sharedverb alpha" }, alphaLeaves);
        Assert.Equal(new[] { "sharedverb beta" }, betaLeaves);

        var registry = new ExtensionRegistry();

        var alphaContribs = ExtensionAddPlanner.PlanContributions(
            "Azclips.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, alphaLeaves, registry);
        Assert.Single(alphaContribs);
        Assert.Equal("sharedverb", alphaContribs[0].Verb);
        Assert.Equal("alpha", alphaContribs[0].Path);

        // Commit Alpha into the registry the same shape `Add.cs` does.
        registry.Extensions["Azclips.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>(alphaContribs),
        };

        // Beta plans cleanly against a registry that already owns "sharedverb alpha".
        var betaContribs = ExtensionAddPlanner.PlanContributions(
            "Azclips.Tests.Fixtures.SharedBeta", new[] { "sharedverb" }, betaLeaves, registry);
        Assert.Single(betaContribs);
        Assert.Equal("sharedverb", betaContribs[0].Verb);
        Assert.Equal("beta", betaContribs[0].Path);
    }

    [Fact]
    public void PlanContributions_SharedVerb_CollidingLeaf_SecondFailsLeafConflict()
    {
        // Stage the SAME fixture DLL into two different temp dirs and plan it under two
        // different package ids. Both contribute `sharedverb alpha`, so the second plan must
        // throw LeafConflict (exit 5) and the registry remains owning the first id only.
        var firstDir = StageFixtureDll("SharedAlpha");
        var secondDir = StageFixtureDll("SharedAlpha");

        var firstLeaves = LeafReader.ReadCliLeafKeys(firstDir);
        var secondLeaves = LeafReader.ReadCliLeafKeys(secondDir);
        Assert.Equal(firstLeaves, secondLeaves);

        var registry = new ExtensionRegistry();
        var firstContribs = ExtensionAddPlanner.PlanContributions(
            "Azclips.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, firstLeaves, registry);
        registry.Extensions["Azclips.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>(firstContribs),
        };

        var ex = Assert.Throws<AzclipsErrorException>(() =>
            ExtensionAddPlanner.PlanContributions(
                "Azclips.Tests.Fixtures.SharedAlpha.Impostor",
                new[] { "sharedverb" },
                secondLeaves,
                registry));

        Assert.Equal("LeafConflict", ex.Code);
        Assert.Equal(3, ex.ExitCode);
        Assert.Contains("sharedverb alpha", ex.Message);
        Assert.Contains("Azclips.Tests.Fixtures.SharedAlpha", ex.Message);

        // Details payload carries the structured conflict array, not just a single-row
        // dictionary. Even with one conflict the shape is the same.
        Assert.NotNull(ex.Details);
        Assert.Equal(1, Assert.IsType<int>(ex.Details!["conflictCount"]));
        var conflicts = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(ex.Details["conflicts"]);
        var only = Assert.Single(conflicts);
        Assert.Equal("sharedverb alpha", only["leafKey"]);
        Assert.Equal("sharedverb", only["verb"]);
        Assert.Equal("alpha", only["path"]);
        Assert.Equal("Azclips.Tests.Fixtures.SharedAlpha", only["existingPackageId"]);

        // Registry was not mutated by the planner (planner is pure; production caller commits).
        Assert.Single(registry.Extensions);
        Assert.True(registry.Extensions.ContainsKey("Azclips.Tests.Fixtures.SharedAlpha"));
    }

    [Fact]
    public void PlanContributions_MultipleCollisions_AreEnumeratedInOneThrow()
    {
        // Multi-conflict enumeration: a candidate that would collide on N leaves must
        // surface all N rows in a single throw, not abort on the first. Hand-roll the
        // candidate's leaf set (so the test isn't bound to a fixture's specific contributions)
        // and pre-populate the registry with two different already-installed packages owning
        // the colliding tuples.
        var registry = new ExtensionRegistry();
        registry.Extensions["Existing.A"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>
            {
                new() { Verb = "sharedverb", Path = "alpha" },
                new() { Verb = "sharedverb", Path = "gamma" },
            },
        };
        registry.Extensions["Existing.B"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>
            {
                new() { Verb = "sharedverb", Path = "beta" },
            },
        };

        // Candidate claims four leaves: three collide (alpha → A, beta → B, gamma → A);
        // `delta` is fresh so we know `contributions` would have included it had the planner
        // not bailed. The order in the throw must follow leaf-key iteration order.
        var candidateLeaves = new[]
        {
            "sharedverb alpha",
            "sharedverb beta",
            "sharedverb delta",
            "sharedverb gamma",
        };

        var ex = Assert.Throws<AzclipsErrorException>(() =>
            ExtensionAddPlanner.PlanContributions(
                "Candidate.Pkg",
                new[] { "sharedverb" },
                candidateLeaves,
                registry));

        Assert.Equal("LeafConflict", ex.Code);
        Assert.NotNull(ex.Details);
        Assert.Equal(3, Assert.IsType<int>(ex.Details!["conflictCount"]));
        Assert.Contains("3 leaf conflicts", ex.Message);

        var conflicts = Assert
            .IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(ex.Details["conflicts"])
            .ToArray();
        Assert.Equal(3, conflicts.Length);

        // Order matches candidate leaf iteration: alpha, beta, gamma (delta is non-conflicting
        // and never appears in the array).
        Assert.Equal("sharedverb alpha", conflicts[0]["leafKey"]);
        Assert.Equal("Existing.A", conflicts[0]["existingPackageId"]);
        Assert.Equal("sharedverb beta", conflicts[1]["leafKey"]);
        Assert.Equal("Existing.B", conflicts[1]["existingPackageId"]);
        Assert.Equal("sharedverb gamma", conflicts[2]["leafKey"]);
        Assert.Equal("Existing.A", conflicts[2]["existingPackageId"]);

        // Human-readable summary mentions every colliding leaf.
        Assert.Contains("sharedverb alpha", ex.Message);
        Assert.Contains("sharedverb beta", ex.Message);
        Assert.Contains("sharedverb gamma", ex.Message);
    }

    [Fact]
    public void PlanContributions_SamePackageId_AllowsSelfUpsert()
    {
        // Self-upsert is the in-place upgrade path: planning the same package id whose
        // contributions are already in the registry must not throw. Without this branch the
        // `add --force` upgrade flow would always trip its own LeafConflict.
        var dir = StageFixtureDll("SharedAlpha");
        var leaves = LeafReader.ReadCliLeafKeys(dir);

        var registry = new ExtensionRegistry();
        registry.Extensions["Azclips.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>
            {
                new() { Verb = "sharedverb", Path = "alpha" },
            },
        };

        var contribs = ExtensionAddPlanner.PlanContributions(
            "Azclips.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, leaves, registry);
        Assert.Single(contribs);
        Assert.Equal("alpha", contribs[0].Path);
    }

    [Fact]
    public void PlanContributions_LeafKeyWithUndeclaredVerb_Throws()
    {
        // Every leaf-key's first token must match a declared [ExtensionVerb]. The build-time
        // hint generator catches this at PR time; the planner enforces it again at install
        // time so a hand-rolled DLL can't slip through.
        var ex = Assert.Throws<AzclipsErrorException>(() =>
            ExtensionAddPlanner.PlanContributions(
                "Azclips.Tests.Fixtures.SharedAlpha",
                new[] { "sharedverb" },
                new[] { "otherverb undeclared" },
                new ExtensionRegistry()));

        Assert.Equal("LeafConflict", ex.Code);
        Assert.Contains("otherverb", ex.Message);
        Assert.Contains("declared via [ExtensionVerb]", ex.Message);
    }

    /// <summary>
    /// Copy a built fixture's DLL set into a fresh temp dir so <see cref="LeafReader"/> can
    /// load it through a throwaway ALC. Carries every <c>*.dll</c> from the fixture's bin
    /// folder so transitive Core.Abstractions resolves.
    /// </summary>
    private string StageFixtureDll(string fixtureName)
    {
        var fixtureBin = LocateFixtureBin(fixtureName)
            ?? throw new InvalidOperationException(
                $"Could not locate built fixture '{fixtureName}'. Run `dotnet build " +
                $"tests/Fixtures/{fixtureName}/Azclips.Tests.Fixtures.{fixtureName}.csproj`.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"azclips-extadd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempRoots.Add(tempDir);

        foreach (var dll in Directory.EnumerateFiles(fixtureBin, "*.dll", SearchOption.TopDirectoryOnly))
        {
            File.Copy(dll, Path.Combine(tempDir, Path.GetFileName(dll)), overwrite: true);
        }

        return tempDir;
    }

    private static string? LocateFixtureBin(string fixtureName)
    {
        // Walk up from the test's bin dir to the repo root, then descend into
        // tests/Fixtures/<name>/bin/<Configuration>/net10.0/. The fixture is built as a normal
        // class-library output (default SDK layout, AppendTargetFrameworkToOutputPath=true).
        // Configuration is mirrored from the test assembly's own bin path so Debug/Release
        // matrix builds (see .github/workflows/build.yml) both resolve correctly.
        var configuration = InferConfigurationFromBaseDirectory() ?? "Debug";
        var probe = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(probe); i++)
        {
            var candidate = Path.Combine(
                probe, "tests", "Fixtures", fixtureName, "bin", configuration, "net10.0");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, $"Azclips.Tests.Fixtures.{fixtureName}.dll")))
            {
                return candidate;
            }
            probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar));
        }
        return null;
    }

    private static string? InferConfigurationFromBaseDirectory()
    {
        // AppContext.BaseDirectory for Cli.Tests is .../tests/Cli.Tests/bin/<Configuration>/net10.0/.
        // Pull the configuration token by walking two parents up from the TFM folder.
        var tfmDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var configDir = Path.GetDirectoryName(tfmDir);
        if (string.IsNullOrEmpty(configDir)) return null;
        var binDir = Path.GetDirectoryName(configDir);
        if (string.IsNullOrEmpty(binDir) || !string.Equals(Path.GetFileName(binDir), "bin", StringComparison.Ordinal))
            return null;
        return Path.GetFileName(configDir);
    }
}
