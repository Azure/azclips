using System.Runtime.CompilerServices;

namespace Ace.Generator.Tests;

/// <summary>
/// Test-only <c>GeneratedFileHandler</c> that persists generated files to
/// <c>test/Ace.Generator.Tests/artifacts/&lt;scenario&gt;/</c> so a run's output can
/// be opened and eyeballed, while also mirroring them into an in-memory
/// <see cref="GeneratedFileCollector"/> for assertions.
///
/// The <paramref name="scenario"/> (defaulting to the calling test method) names a
/// per-test subfolder, which is cleared at construction so stale files from a
/// previous run never linger.
/// </summary>
public sealed class ArtifactWriter
{
    private readonly string _baseDir;
    private readonly GeneratedFileCollector _collector = new();

    public ArtifactWriter([CallerMemberName] string scenario = "default")
    {
        _baseDir = Path.Combine(ArtifactsRoot(), scenario);
        if (Directory.Exists(_baseDir))
        {
            Directory.Delete(_baseDir, recursive: true);
        }

        Directory.CreateDirectory(_baseDir);
    }

    /// <summary>Absolute path to this scenario's output directory.</summary>
    public string BaseDirectory => _baseDir;

    /// <summary>The files written so far, keyed by relative path (for assertions).</summary>
    public IReadOnlyDictionary<string, string> Files => _collector.Files;

    /// <summary>
    /// <c>GeneratedFileHandler</c> to hand to <c>GenerateAsync</c>: writes each file
    /// under <see cref="BaseDirectory"/> (creating subdirectories) and records it
    /// in the in-memory mirror.
    /// </summary>
    public async ValueTask WriteAsync(string relativePath, string contents, CancellationToken cancellationToken = default)
    {
        string full = Path.Combine(_baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, contents, cancellationToken);
        await _collector.WriteAsync(relativePath, contents, cancellationToken);
    }

    /// <summary>
    /// <c>test/Ace.Generator.Tests/artifacts</c>, resolved from this source file's
    /// location so it is independent of the working directory and the build output
    /// layout.
    /// </summary>
    private static string ArtifactsRoot([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "artifacts");
}
