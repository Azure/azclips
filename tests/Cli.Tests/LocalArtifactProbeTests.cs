using Microsoft.Azure.Cirrus.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// <see cref="LocalArtifactProbe"/> walks up looking for <c>cirrus.slnx</c> and
/// surfaces a freshly built <c>.nupkg</c> in <c>artifacts/Debug/</c> as an <c>--path</c> hint when
/// the feed lookup misses. These tests pin the four observable behaviors: (1) silent null when
/// no slnx is found, (2) silent null when slnx exists but no artifact matches, (3) returns the
/// matching nupkg, (4) most-recently-written wins when multiple versions are on disk.
/// </summary>
public class LocalArtifactProbeTests : IDisposable
{
    private readonly string _tempRoot;

    public LocalArtifactProbeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "cirrus-probe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private string MakeFakeRepo(bool withSlnx)
    {
        var repo = Path.Combine(_tempRoot, "repo");
        Directory.CreateDirectory(repo);
        if (withSlnx)
        {
            File.WriteAllText(Path.Combine(repo, "cirrus.slnx"), "<Solution />");
        }
        return repo;
    }

    private static string CreateNupkg(string repo, string packageId, string version, DateTime writeTimeUtc)
    {
        var packageDir = Path.Combine(repo, "artifacts", "Debug", packageId);
        Directory.CreateDirectory(packageDir);
        var nupkg = Path.Combine(packageDir, $"{packageId}.{version}.nupkg");
        File.WriteAllText(nupkg, "fake");
        File.SetLastWriteTimeUtc(nupkg, writeTimeUtc);
        return nupkg;
    }

    [Fact]
    public void Find_ShouldReturnNull_WhenNoSlnxOnPathUp()
    {
        // Walk-up anchor missing => not running from a Cirrus repo => silent no-op.
        var notARepo = Path.Combine(_tempRoot, "not-a-repo");
        Directory.CreateDirectory(notARepo);

        var result = LocalArtifactProbe.Find("Microsoft.Azure.Cirrus.Extensions.Compute", notARepo);

        Assert.Null(result);
    }

    [Fact]
    public void Find_ShouldReturnNull_WhenSlnxFoundButNoArtifact()
    {
        var repo = MakeFakeRepo(withSlnx: true);

        var result = LocalArtifactProbe.Find("Microsoft.Azure.Cirrus.Extensions.Compute", repo);

        Assert.Null(result);
    }

    [Fact]
    public void Find_ShouldReturnNupkgPath_WhenSingleArtifactExists()
    {
        var repo = MakeFakeRepo(withSlnx: true);
        var nupkg = CreateNupkg(repo, "Microsoft.Azure.Cirrus.Extensions.Compute", "1.0.0-alpha", DateTime.UtcNow);

        var result = LocalArtifactProbe.Find("Microsoft.Azure.Cirrus.Extensions.Compute", repo);

        Assert.Equal(nupkg, result);
    }

    [Fact]
    public void Find_ShouldReturnMostRecent_WhenMultipleVersionsExist()
    {
        var repo = MakeFakeRepo(withSlnx: true);
        var older = CreateNupkg(repo, "Microsoft.Azure.Cirrus.Extensions.Compute", "1.0.0-alpha", DateTime.UtcNow.AddDays(-2));
        var newer = CreateNupkg(repo, "Microsoft.Azure.Cirrus.Extensions.Compute", "1.0.1-alpha", DateTime.UtcNow);

        var result = LocalArtifactProbe.Find("Microsoft.Azure.Cirrus.Extensions.Compute", repo);

        Assert.Equal(newer, result);
        Assert.NotEqual(older, result);
    }

    [Fact]
    public void Find_ShouldWalkUp_FromNestedSubdirectory()
    {
        // Anchor walk-up: starting deep in a dev tree (e.g. running `az` from src/Foo) still
        // finds the root slnx and probes artifacts/ relative to it.
        var repo = MakeFakeRepo(withSlnx: true);
        var nupkg = CreateNupkg(repo, "Microsoft.Azure.Cirrus.Extensions.Compute", "1.0.0", DateTime.UtcNow);
        var nested = Path.Combine(repo, "src", "Extensions", "Extension");
        Directory.CreateDirectory(nested);

        var result = LocalArtifactProbe.Find("Microsoft.Azure.Cirrus.Extensions.Compute", nested);

        Assert.Equal(nupkg, result);
    }

    [Fact]
    public void Find_ShouldReturnNull_OnEmptyPackageId()
    {
        var repo = MakeFakeRepo(withSlnx: true);

        var result = LocalArtifactProbe.Find("", repo);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("../../../Windows/System32")]
    [InlineData("..\\..\\..\\Windows\\System32")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows")]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    [InlineData("foo bar")]
    [InlineData("foo:bar")]
    [InlineData("Microsoft.Extensions.Cómpute")] // accented letter (Latin-1)
    [InlineData("Package.Ⅳ")]                    // Roman numeral 4 (Unicode digit)
    public void Find_ShouldReturnNull_OnPackageIdsWithInvalidChars(string packageId)
    {
        // Defense-in-depth: packageId is user-influenced (dotted tokens pass through
        // Add.ResolvePackageId). A path-traversal or rooted token must not be combined
        // with repoRoot and probed via Directory.Exists.
        var repo = MakeFakeRepo(withSlnx: true);

        var result = LocalArtifactProbe.Find(packageId, repo);

        Assert.Null(result);
    }
}
