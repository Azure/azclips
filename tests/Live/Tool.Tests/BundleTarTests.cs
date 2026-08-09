using System.Formats.Tar;
using System.IO.Compression;
using Live.Tool;

namespace Live.Tool.Tests;

/// <summary>
/// Covers the parts of the bundle build that a Linux CI run would never exercise.
/// <para>
/// The port exists so the live test can be driven from Windows and macOS, and the way it
/// most plausibly breaks there is silently: a bundle built without executable bits looks
/// fine locally and only fails minutes later, inside the VM, as
/// <c>!! az not found/executable</c>. These tests pin that behaviour on every platform.
/// </para>
/// </summary>
public class BundleTarTests
{
    private const UnixFileMode Executable = (UnixFileMode)0b111_101_101; // 0755
    private const UnixFileMode ReadWrite = (UnixFileMode)0b110_100_100;  // 0644

    [Theory]
    // setup-vm-mi.sh checks `[[ ! -x ]]` on both of these and exits if they are not
    // executable, so these two rows are the ones that keep the test runnable at all.
    [InlineData("Core.Cli/az")]
    // Regression: this parses as though it had a ".Tests" extension, and an earlier
    // version of the shape heuristic therefore shipped it as 0644.
    [InlineData("Live.Tests/Live.Tests")]
    [InlineData("setup-vm-mi.sh")]
    [InlineData("Core.Cli/createdump")]
    [InlineData("Core.Cli/libSystem.Native.so")]
    public void ModeFor_OnWindows_MarksBundleExecutablesExecutable(string relativePath)
    {
        WithBundleLayout(bundle =>
            Assert.Equal(Executable, Bundle.ModeFor(Path.Combine(bundle, relativePath), windows: true)));
    }

    [Theory]
    [InlineData("Core.Cli/Core.Abstractions.dll")]
    [InlineData("Core.Cli/az.runtimeconfig.json")]
    [InlineData("Core.Cli/az.deps.json")]
    public void ModeFor_OnWindows_LeavesDataFilesNonExecutable(string relativePath)
    {
        WithBundleLayout(bundle =>
            Assert.Equal(ReadWrite, Bundle.ModeFor(Path.Combine(bundle, relativePath), windows: true)));
    }

    [Fact]
    public void CreateTarGz_PreservesExecutableBits_ForAWindowsStyleBuild()
    {
        WithBundleLayout(bundle =>
        {
            var tarball = Path.Combine(Path.GetDirectoryName(bundle)!, "bundle.tar.gz");
            Bundle.CreateTarGz(bundle, tarball, windows: true);

            var modes = ReadModes(tarball);

            Assert.Equal(Executable, modes["Release-linux-x64/Core.Cli/az"]);
            Assert.Equal(Executable, modes["Release-linux-x64/Live.Tests/Live.Tests"]);
            Assert.Equal(Executable, modes["Release-linux-x64/setup-vm-mi.sh"]);
            Assert.Equal(Executable, modes["Release-linux-x64/Core.Cli/createdump"]);
            Assert.Equal(ReadWrite, modes["Release-linux-x64/Core.Cli/Core.Abstractions.dll"]);
            Assert.Equal(ReadWrite, modes["Release-linux-x64/Core.Cli/az.runtimeconfig.json"]);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void CreateTarGz_UsesForwardSlashes_SoTheArchiveUnpacksOnLinux(string trailingSeparator)
    {
        WithBundleLayout(bundle =>
        {
            var tarball = Path.Combine(Path.GetDirectoryName(bundle)!, "bundle.tar.gz");
            bundle += trailingSeparator;
            Bundle.CreateTarGz(bundle, tarball, windows: true);
            var names = ReadModes(tarball).Keys;

            // A backslash here would produce one file literally named "Core.Cli\az".
            // Only reachable on Windows, where build.yml runs this suite.
            Assert.All(names, name => Assert.DoesNotContain('\\', name));
            Assert.Contains("Release-linux-x64/Core.Cli/az", names);
        });
    }

    /// <summary>
    /// Build a miniature of the real publish output. The layout matters: apphosts are
    /// recognised by the managed assembly sitting next to them.
    /// </summary>
    private static void WithBundleLayout(Action<string> body)
    {
        var root = Directory.CreateTempSubdirectory("livetool");
        try
        {
            var bundle = Path.Combine(root.FullName, "Release-linux-x64");
            Directory.CreateDirectory(Path.Combine(bundle, "Core.Cli"));
            Directory.CreateDirectory(Path.Combine(bundle, "Live.Tests"));

            foreach (var relative in new[]
                     {
                         "Core.Cli/az",
                         "Core.Cli/az.dll",
                         "Core.Cli/az.runtimeconfig.json",
                         "Core.Cli/az.deps.json",
                         "Core.Cli/Core.Abstractions.dll",
                         "Core.Cli/createdump",
                         "Core.Cli/libSystem.Native.so",
                         "Live.Tests/Live.Tests",
                         "Live.Tests/Live.Tests.dll",
                         "setup-vm-mi.sh",
                     })
            {
                File.WriteAllText(Path.Combine(bundle, relative.Replace('/', Path.DirectorySeparatorChar)), "x");
            }

            body(bundle);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static Dictionary<string, UnixFileMode> ReadModes(string tarball)
    {
        var modes = new Dictionary<string, UnixFileMode>(StringComparer.Ordinal);
        using var file = File.OpenRead(tarball);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is TarEntryType.RegularFile)
            {
                modes[entry.Name] = entry.Mode;
            }
        }

        return modes;
    }
}
