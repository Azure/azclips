// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;
using Microsoft.Azure.Cirrus.Extensions.Extension;

namespace Cli.Tests;

/// <summary>
/// Phase 10 #38: Shape B (§11.7) contract test. Drives <see cref="ExtensionAddPlanner"/> against
/// real fixture DLLs read from disk via <see cref="LeafReader"/>, exercising the exact production
/// branches that decide whether two extensions sharing a verb can co-exist (disjoint
/// <c>(verb, path)</c> tuples) or collide (same tuple, different package id).
/// </summary>
/// <remarks>
/// This is the "B1" fidelity per the Phase-10 plan: we skip <c>ExtensionInstaller</c> / NuGet
/// extraction (no nupkg packaging, no <c>~/.cirrus</c> involvement) and stage compiled fixture
/// DLLs into temp directories the test owns. The planner is the same code path
/// <see cref="Microsoft.Azure.Cirrus.Extensions.Extension.Processors.AddExtensionProcessor.Process()"/>
/// runs after extraction, so a passing test proves the contract end-to-end from "leaves on disk"
/// to "registry decision".
/// </remarks>
public sealed class ShapeBContractTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

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
    public void PlanContributions_SharedVerb_DisjointLeaves_BothSucceed()
    {
        // Alpha contributes `sharedverb alpha`, Beta contributes `sharedverb beta`. Same root
        // verb, different leaves — Shape B's whole point. Plan Alpha into an empty registry,
        // commit it, then plan Beta against the registry-with-Alpha. Both should produce
        // contributions without throwing.
        var alphaDir = StageFixtureDll("SharedAlpha");
        var betaDir = StageFixtureDll("SharedBeta");

        var alphaLeaves = LeafReader.ReadCliLeafKeys(alphaDir);
        var betaLeaves = LeafReader.ReadCliLeafKeys(betaDir);

        Assert.Equal(new[] { "sharedverb alpha" }, alphaLeaves);
        Assert.Equal(new[] { "sharedverb beta" }, betaLeaves);

        var registry = new ExtensionRegistry();

        var alphaContribs = ExtensionAddPlanner.PlanContributions(
            "Cirrus.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, alphaLeaves, registry);
        Assert.Single(alphaContribs);
        Assert.Equal("sharedverb", alphaContribs[0].Verb);
        Assert.Equal("alpha", alphaContribs[0].Path);

        // Commit Alpha into the registry the same shape `Add.cs` does.
        registry.Extensions["Cirrus.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>(alphaContribs),
        };

        // Beta plans cleanly against a registry that already owns "sharedverb alpha".
        var betaContribs = ExtensionAddPlanner.PlanContributions(
            "Cirrus.Tests.Fixtures.SharedBeta", new[] { "sharedverb" }, betaLeaves, registry);
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
            "Cirrus.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, firstLeaves, registry);
        registry.Extensions["Cirrus.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>(firstContribs),
        };

        var ex = Assert.Throws<CirrusErrorException>(() =>
            ExtensionAddPlanner.PlanContributions(
                "Cirrus.Tests.Fixtures.SharedAlpha.Impostor",
                new[] { "sharedverb" },
                secondLeaves,
                registry));

        Assert.Equal("LeafConflict", ex.Code);
        Assert.Equal(3, ex.ExitCode);
        Assert.Contains("sharedverb alpha", ex.Message);
        Assert.Contains("Cirrus.Tests.Fixtures.SharedAlpha", ex.Message);
        // Registry was not mutated by the planner (planner is pure; production caller commits).
        Assert.Single(registry.Extensions);
        Assert.True(registry.Extensions.ContainsKey("Cirrus.Tests.Fixtures.SharedAlpha"));
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
        registry.Extensions["Cirrus.Tests.Fixtures.SharedAlpha"] = new ExtensionRegistryEntry
        {
            Verb = "sharedverb",
            Version = "1.0.0",
            Contributions = new List<Contribution>
            {
                new() { Verb = "sharedverb", Path = "alpha" },
            },
        };

        var contribs = ExtensionAddPlanner.PlanContributions(
            "Cirrus.Tests.Fixtures.SharedAlpha", new[] { "sharedverb" }, leaves, registry);
        Assert.Single(contribs);
        Assert.Equal("alpha", contribs[0].Path);
    }

    [Fact]
    public void PlanContributions_LeafKeyWithUndeclaredVerb_Throws()
    {
        // §11.2 Q10: every leaf-key's first token must match a declared [ExtensionVerb]. The
        // build-time hint generator catches this at PR time; the planner enforces it again at
        // install time so a hand-rolled DLL can't slip through.
        var ex = Assert.Throws<CirrusErrorException>(() =>
            ExtensionAddPlanner.PlanContributions(
                "Cirrus.Tests.Fixtures.SharedAlpha",
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
                $"tests/Fixtures/{fixtureName}/Cirrus.Tests.Fixtures.{fixtureName}.csproj`.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"cirrus-shapeb-{Guid.NewGuid():N}");
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
        // tests/Fixtures/<name>/bin/Debug/net10.0/. The fixture is built as a normal
        // class-library output (default SDK layout, AppendTargetFrameworkToOutputPath=true).
        var probe = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(probe); i++)
        {
            var candidate = Path.Combine(
                probe, "tests", "Fixtures", fixtureName, "bin", "Debug", "net10.0");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, $"Cirrus.Tests.Fixtures.{fixtureName}.dll")))
            {
                return candidate;
            }
            probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar));
        }
        return null;
    }
}
