using System.Formats.Tar;
using System.IO.Compression;
using System.Xml.Linq;

namespace Live.Tool;

/// <summary>
/// Builds the self-contained bundle shipped to the VM. Port of <c>publish-vm-mi.sh</c>.
/// </summary>
internal sealed class Bundle(string repoRoot, string liveRoot, Timing timing)
{
    /// <summary>
    /// Built-in (always-on) extensions. Their verbs are seeded in-memory by the host and
    /// loaded from loose folders by BuiltinExtensionLoader — so ONLY these need loose-folder
    /// output, and they MUST NOT be packed/registered as nupkg.
    /// </summary>
    private static readonly string[] BuiltInIds =
    [
        "Microsoft.Azure.Azclips.Extensions.Extension",
        "Microsoft.Azure.Azclips.Extensions.Login",
        "Microsoft.Azure.Azclips.Extensions.Config",
    ];

    /// <summary>Build the bundle and return the path to the tarball.</summary>
    public string Build(string rid, string config, bool makeTar)
    {
        var outDir = Path.Combine(repoRoot, "publish", "vm", $"{config}-{rid}");

        Console.WriteLine($"==> Repo:   {repoRoot}");
        Console.WriteLine($"==> RID:    {rid}");
        Console.WriteLine($"==> Config: {config}");
        Console.WriteLine($"==> Out:    {outDir}");
        Console.WriteLine();

        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, recursive: true);
        }

        Directory.CreateDirectory(outDir);

        // 1) Self-contained CLI host -> <out>/Core.Cli. The apphost is named 'az' and is
        //    published self-contained so the VM needs no .NET runtime installed.
        timing.Timed("publish CLI (self-contained)", () =>
            Dotnet("publish", Path.Combine("src", "CLI", "Core.Cli.csproj"),
                "-c", config, "-r", rid, "--self-contained", "true",
                "-o", Path.Combine(outDir, "Core.Cli")));

        var extensionProjects = FindExtensionProjects();

        // 2) Built-in extensions only (framework-dependent) -> <out>/<ExtName>. The folder
        //    name MUST equal <ExtName> so BuiltinExtensionLoader finds it.
        timing.Timed("publish built-in extensions", () =>
        {
            foreach (var (csproj, extName) in extensionProjects)
            {
                if (!BuiltInIds.Contains(extName, StringComparer.Ordinal))
                {
                    Console.WriteLine($"    - skip {extName} (non-built-in; installed from nupkg)");
                    continue;
                }

                Console.WriteLine($"    - {extName}");
                Dotnet("publish", csproj, "-c", config, "-r", rid, "--no-self-contained",
                    "-o", Path.Combine(outDir, extName));
            }
        });

        // 3) Pack the NON-built-in extensions as .nupkg so they can be registered offline
        //    on the VM (az extension add --path).
        timing.Timed("pack extensions for offline install", () =>
        {
            Directory.CreateDirectory(Path.Combine(outDir, "packages"));
            foreach (var (csproj, extName) in extensionProjects)
            {
                if (BuiltInIds.Contains(extName, StringComparer.Ordinal))
                {
                    Console.WriteLine($"    - skip {extName} (built-in)");
                    continue;
                }

                Console.WriteLine($"    - pack {extName}");
                // Build first so the runtime config/deps files exist, then pack with
                // --no-build. A bare `dotnet pack` from a clean tree fails with NU5026
                // (missing *.runtimeconfig.json) because pack does not emit it on its own.
                Dotnet("build", csproj, "-c", config);
                Dotnet("pack", csproj, "-c", config, "--no-build", "-o", Path.Combine(outDir, "packages"));
            }
        });

        // 4) Live test suite -> <out>/Live.Tests. Self-contained because the VM has no .NET
        //    SDK and so cannot run `dotnet test`; xUnit v3 builds a standalone executable.
        timing.Timed("publish live tests (self-contained)", () =>
            Dotnet("publish", Path.Combine("tests", "Live", "Live.Tests", "Live.Tests.csproj"),
                "-c", config, "-r", rid, "--self-contained", "true",
                "-o", Path.Combine(outDir, "Live.Tests")));

        // 5) The VM setup helper travels with the bundle. timing.sh is still bash: it runs
        //    on the VM, where this tool does not.
        timing.Timed("add setup-vm-mi.sh to bundle", () =>
        {
            var scripts = Path.Combine(liveRoot, "Scripts");
            File.Copy(Path.Combine(scripts, "setup-vm-mi.sh"), Path.Combine(outDir, "setup-vm-mi.sh"), overwrite: true);
            File.Copy(Path.Combine(scripts, "lib", "timing.sh"), Path.Combine(outDir, "timing.sh"), overwrite: true);
        });

        Console.WriteLine();
        Console.WriteLine($"==> Bundle ready: {outDir}");

        if (!makeTar)
        {
            return string.Empty;
        }

        var tarball = Path.Combine(repoRoot, "publish", "vm", $"azclips-{config}-{rid}.tar.gz");
        timing.Timed($"create tarball ({Path.GetFileName(tarball)})", () => CreateTarGz(outDir, tarball));
        Console.WriteLine($"    {new FileInfo(tarball).Length / 1024 / 1024} MB -> {tarball}");
        return tarball;
    }

    private void Dotnet(params string[] args)
    {
        var r = Proc.Run("dotnet", args, workingDirectory: repoRoot);
        if (!r.Ok)
        {
            // dotnet reports build errors on stdout, not stderr.
            var detail = string.IsNullOrWhiteSpace(r.Stdout) ? r.Stderr : r.Stdout;
            throw new LiveToolException($"dotnet {string.Join(' ', args)} failed (exit {r.ExitCode}).\n{detail.Trim()}");
        }
    }

    /// <summary>
    /// Discover extension projects and their &lt;ExtName&gt;.
    /// <para>
    /// Parsed as XML rather than pattern-matched: the bash version used <c>grep -oP</c>,
    /// which does not exist on BSD/macOS grep, and its <c>|| true</c> turned that into an
    /// empty name — silently producing a bundle with no extensions at all.
    /// </para>
    /// </summary>
    private List<(string Csproj, string ExtName)> FindExtensionProjects()
    {
        var root = Path.Combine(repoRoot, "src", "Extensions");
        var found = new List<(string, string)>();

        foreach (var csproj in Directory.EnumerateFiles(root, "Extensions.*.csproj", SearchOption.AllDirectories))
        {
            if (csproj.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var extName = XDocument.Load(csproj).Descendants("ExtName").FirstOrDefault()?.Value?.Trim();
            if (string.IsNullOrEmpty(extName))
            {
                Console.WriteLine($"    !! Skipping {csproj} (no <ExtName>)");
                continue;
            }

            found.Add((csproj, extName));
        }

        return [.. found.OrderBy(x => x.Item2, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Write the bundle as a .tar.gz with correct Unix permissions.
    /// <para>
    /// The mode is set explicitly rather than copied from disk because Windows has no
    /// executable bit: a bundle built there would arrive on the VM with a non-executable
    /// <c>Core.Cli/az</c> and <c>Live.Tests/Live.Tests</c>, and setup-vm-mi.sh fails
    /// outright on both (<c>[[ ! -x ]]</c>). Getting this wrong is the single most likely
    /// way for a cross-platform build to break, and it breaks it remotely.
    /// </para>
    /// </summary>
    internal static void CreateTarGz(string sourceDir, string tarballPath, bool? windows = null)
    {
        var onWindows = windows ?? OperatingSystem.IsWindows();
        var prefix = Path.GetFileName(sourceDir.TrimEnd('/', '\\'));
        Directory.CreateDirectory(Path.GetDirectoryName(tarballPath)!);
        if (File.Exists(tarballPath))
        {
            File.Delete(tarballPath);
        }

        using var file = File.Create(tarballPath);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        using var tar = new TarWriter(gzip, TarEntryFormat.Pax);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var entry = new PaxTarEntry(TarEntryType.Directory, EntryName(sourceDir, dir, prefix) + "/")
            {
                Mode = (UnixFileMode)0b111_101_101, // 0755
            };
            tar.WriteEntry(entry);
        }

        foreach (var path in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, EntryName(sourceDir, path, prefix))
            {
                Mode = ModeFor(path, onWindows),
            };

            using var content = File.OpenRead(path);
            entry.DataStream = content;
            tar.WriteEntry(entry);
        }
    }

    private static string EntryName(string root, string path, string prefix) =>
        prefix + "/" + Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Decide the Unix mode for a bundled file.
    /// <para>
    /// On Unix the real mode is authoritative. On Windows there is none to read, so
    /// executables are identified by shape. Getting this wrong is not a build error: the
    /// bundle is produced happily and only fails minutes later inside the VM, where
    /// setup-vm-mi.sh tests <c>-x</c> on both `az` and the test runner and aborts.
    /// </para>
    /// </summary>
    internal static UnixFileMode ModeFor(string path, bool windows)
    {
        const UnixFileMode Executable = (UnixFileMode)0b111_101_101; // 0755
        const UnixFileMode ReadWrite = (UnixFileMode)0b110_100_100;  // 0644

        var looksExecutable = IsExecutable(path);

        // On Windows there is no mode to read, so infer one from the file's shape.
        if (windows || OperatingSystem.IsWindows())
        {
            return looksExecutable ? Executable : ReadWrite;
        }

        var actual = File.GetUnixFileMode(path);
        return looksExecutable ? actual | Executable : actual;
    }

    /// <summary>
    /// Identify the files that must arrive on the VM with the exec bit set.
    /// </summary>
    internal static bool IsExecutable(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path);

        if (ext.Equals(".sh", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".so", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase)
            || name.Equals("createdump", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A .NET apphost carries the assembly's name, so `Live.Tests` parses as though it
        // had a ".Tests" extension. Testing for a matching managed assembly alongside it
        // is what actually distinguishes an apphost from a data file.
        if (File.Exists(path + ".dll"))
        {
            return true;
        }

        return ext.Length == 0;
    }

}
