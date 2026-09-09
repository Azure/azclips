// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.Abstractions;
using Core.Extensions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Covers <see cref="CompatibilityGuard.Check"/> and the
/// <see cref="CompatibilityGuard.CheckAssembly"/> reader path.
/// </summary>
public class CompatibilityGuardTests
{
    // --- Check (pure) -----------------------------------------------------

    [Fact]
    public void Check_NoBounds_IsCompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: null, maxCoreVersion: null, currentCoreVersion: "1.0.0");
        Assert.True(r.IsCompatible);
        Assert.Null(r.Reason);
    }

    [Fact]
    public void Check_CurrentInsideRange_IsCompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: "1.0.0", maxCoreVersion: "2.0.0", currentCoreVersion: "1.5.0");
        Assert.True(r.IsCompatible);
    }

    [Fact]
    public void Check_CurrentBelowMin_IsIncompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: "2.0.0", maxCoreVersion: null, currentCoreVersion: "1.5.0");
        Assert.False(r.IsCompatible);
        Assert.Contains("requires core >= 2.0.0", r.Reason);
    }

    [Fact]
    public void Check_CurrentAboveMax_IsIncompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: null, maxCoreVersion: "1.0.0", currentCoreVersion: "2.0.0");
        Assert.False(r.IsCompatible);
        Assert.Contains("requires core <= 1.0.0", r.Reason);
    }

    [Fact]
    public void Check_CurrentEqualsMin_IsCompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: "1.0.0", maxCoreVersion: null, currentCoreVersion: "1.0.0");
        Assert.True(r.IsCompatible);
    }

    [Fact]
    public void Check_CurrentEqualsMax_IsCompatible()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: null, maxCoreVersion: "1.0.0", currentCoreVersion: "1.0.0");
        Assert.True(r.IsCompatible);
    }

    [Fact]
    public void Check_PrereleaseBelowRelease_IsIncompatible()
    {
        // SemVer: 1.0.0-alpha < 1.0.0
        var r = CompatibilityGuard.Check(minCoreVersion: "1.0.0", maxCoreVersion: null, currentCoreVersion: "1.0.0-alpha");
        Assert.False(r.IsCompatible);
    }

    [Fact]
    public void Check_UnparseableMin_IsCompatibleWithAdvisory()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: "not-a-version", maxCoreVersion: null, currentCoreVersion: "1.0.0");
        Assert.True(r.IsCompatible);
        Assert.Contains("MinCoreVersion", r.Reason);
        Assert.Contains("ignored", r.Reason);
    }

    [Fact]
    public void Check_UnparseableMax_IsCompatibleWithAdvisory()
    {
        var r = CompatibilityGuard.Check(minCoreVersion: null, maxCoreVersion: "garbage", currentCoreVersion: "1.0.0");
        Assert.True(r.IsCompatible);
        Assert.Contains("MaxCoreVersion", r.Reason);
    }

    [Fact]
    public void Check_UnparseableCurrent_IsCompatibleWithAdvisory()
    {
        // Host bug: don't fail-closed and brick every install.
        var r = CompatibilityGuard.Check(minCoreVersion: "1.0.0", maxCoreVersion: "2.0.0", currentCoreVersion: "lol");
        Assert.True(r.IsCompatible);
        Assert.Contains("not a valid SemVer", r.Reason);
    }

    [Fact]
    public void Check_NullCurrent_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CompatibilityGuard.Check(minCoreVersion: "1.0.0", maxCoreVersion: null, currentCoreVersion: ""));
    }

    // --- CheckAssembly (reader) -------------------------------------------

    [Fact]
    public void CheckAssembly_EmptyDirectory_IsCompatible()
    {
        using var dir = new TempDir();
        var r = CompatibilityGuard.CheckAssembly(dir.Path, "1.0.0");
        Assert.True(r.IsCompatible);
    }

    [Fact]
    public void CheckAssembly_MissingDirectory_Throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"azclips-missing-{Guid.NewGuid():N}");
        Assert.Throws<DirectoryNotFoundException>(() =>
            CompatibilityGuard.CheckAssembly(missing, "1.0.0"));
    }

    [Fact]
    public void CheckAssembly_StampedFirstPartyExtension_IsCompatibleAt1_0_0()
    {
        // Accounts is stamped via Compatibility.cs. Use its artifacts directory directly so MLC
        // can resolve the assembly's full reference graph (Core.Abstractions + sibling Core.* DLLs).
        var dir = LocateExtensionDir();
        Assert.NotNull(dir);

        var r = CompatibilityGuard.CheckAssembly(dir!, "1.0.0");
        Assert.True(r.IsCompatible, r.Reason);
    }

    [Fact]
    public void CheckAssembly_StampedFirstPartyExtension_IsIncompatibleAt0_9_0()
    {
        var dir = LocateExtensionDir();
        Assert.NotNull(dir);

        var r = CompatibilityGuard.CheckAssembly(dir!, "0.9.0");
        Assert.False(r.IsCompatible, $"Expected incompatible. Reason: {r.Reason}");
        Assert.Contains("requires core >= 1.0.0", r.Reason);
    }

    private static string? LocateExtensionDir()
    {
        // Walk up from test bin and find the published Accounts extension folder. It contains the
        // Accounts DLL plus all sibling Core.* DLLs the metadata graph needs to fully resolve.
        const string DllName = "Microsoft.Azure.Azclips.Extensions.Accounts.dll";
        var probe = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(probe); i++)
        {
            try
            {
                foreach (var found in Directory.EnumerateFiles(probe, DllName, SearchOption.AllDirectories))
                {
                    var dir = Path.GetDirectoryName(found);
                    // Prefer the artifacts/Debug/<package>/ layout (has full sibling tree),
                    // skip stray copies in obj/ or test bin.
                    if (dir is not null && dir.Contains("artifacts") && !dir.Contains("obj"))
                    {
                        return dir;
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar));
        }
        return null;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"azclips-compat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}

