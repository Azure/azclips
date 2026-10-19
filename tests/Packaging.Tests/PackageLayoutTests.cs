using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Azclips.Packaging;

namespace Packaging.Tests;

public sealed class PackageLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"azclips-archive-{Guid.NewGuid():N}");

    public PackageLayoutTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(".zip")]
    [InlineData(".tar.gz")]
    public void VerifyArchive_AllowsPrivateDependenciesWithoutChangingArchive(string format)
    {
        var archive = CreateArchive(format, "Extensions/Accounts/Microsoft.Azure.Azclips.ExtensionRuntime.dll");
        var original = File.ReadAllBytes(archive);

        PackageLayout.VerifyArchive(archive);

        Assert.Equal(original, File.ReadAllBytes(archive));
    }

    [Theory]
    [InlineData(".zip", "Extensions/Accounts/Core.Authentication.dll")]
    [InlineData(".tar.gz", "./Extensions/Accounts/Core.Authentication.dll")]
    [InlineData(".zip", "Extensions\\Accounts\\Core.Authentication.dll")]
    [InlineData(".tar.gz", "Extensions/Agent/deps/Core.Runtime/1.0.0/Core.Runtime.dll")]
    [InlineData(".zip", "Extensions/Agent/deps/Microsoft.Extensions.DependencyInjection.dll")]
    public void VerifyArchive_RejectsReintroducedSharedCopies(string format, string dependency)
    {
        var archive = CreateArchive(format, dependency);
        var original = File.ReadAllBytes(archive);

        var error = Assert.Throws<InvalidOperationException>(() => PackageLayout.VerifyArchive(archive));

        Assert.Contains("host-shared", error.Message);
        Assert.Equal(original, File.ReadAllBytes(archive));
    }

    [Theory]
    [InlineData(".zip")]
    [InlineData(".tar.gz")]
    public void VerifyArchive_RejectsUnrecognizedPackageLayout(string format)
    {
        var archive = CreateArchive(format, "Other.dll", includeCli: false);

        Assert.Throws<InvalidOperationException>(() => PackageLayout.VerifyArchive(archive));
    }

    [Fact]
    public void VerifyArchive_RejectsMissingAndCorruptArchives()
    {
        Assert.Throws<FileNotFoundException>(() => PackageLayout.VerifyArchive(Path.Combine(_root, "missing.zip")));
        var corrupt = Path.Combine(_root, "corrupt.zip");
        File.WriteAllText(corrupt, "invalid archive");
        Assert.Throws<InvalidDataException>(() => PackageLayout.VerifyArchive(corrupt));
    }

    private string CreateArchive(string format, string dependency, bool includeCli = true)
    {
        var path = Path.Combine(_root, $"package{format}");
        string[] entries = includeCli
            ? ["az.dll", "Core.Authentication.dll", "Extensions/Accounts/Accounts.dll", dependency]
            : [dependency];
        if (format == ".zip")
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var name in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("fixture");
            }
        }
        else
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionMode.Compress);
            using var tar = new TarWriter(gzip);
            foreach (var name in entries)
            {
                using var data = new MemoryStream(Encoding.UTF8.GetBytes("fixture"));
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
            }
        }
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
