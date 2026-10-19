using System.Formats.Tar;
using System.IO.Compression;
using Core.Abstractions;

namespace Azclips.Packaging;

public static class PackageLayout
{
    public static void VerifyArchive(string archivePath)
    {
        var names = new List<string>();
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            names.AddRange(zip.Entries.Select(entry => entry.FullName));
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            while (tar.GetNextEntry() is { } entry)
            {
                names.Add(entry.Name);
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported CLI archive format: {archivePath}");
        }

        var normalized = names.Select(name =>
        {
            var path = name.Replace('\\', '/');
            while (path.StartsWith("./", StringComparison.Ordinal))
            {
                path = path[2..];
            }
            return path;
        }).ToArray();
        if (!normalized.Contains("az.dll", StringComparer.Ordinal) ||
            !normalized.Any(name => name.StartsWith("Extensions/", StringComparison.Ordinal) &&
                                    name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Not a root-layout CLI package with bundled extensions: {archivePath}");
        }

        var shared = normalized.Where(name =>
            name.StartsWith("Extensions/", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            HostSharedAssemblyPolicy.IsShared(Path.GetFileNameWithoutExtension(name))).ToArray();
        if (shared.Length > 0)
        {
            throw new InvalidOperationException(
                $"CLI archive '{archivePath}' contains host-shared extension DLLs:\n{string.Join("\n", shared)}");
        }

        Console.WriteLine($"PASS: no host-shared extension DLLs in {archivePath}");
    }
}
