namespace Ace.Generator.Tests;

/// <summary>
/// A <see cref="GeneratedFileHandler"/> that buffers generated files in memory.
///
/// Optional convenience for callers that <em>deliberately</em> want to collect
/// output — tests asserting on results, or hosts generating small specs. Large
/// runs should stream straight to disk instead of buffering. Pass
/// <see cref="WriteAsync"/> as the handler, then read <see cref="Files"/> afterwards.
/// </summary>
public sealed class GeneratedFileCollector
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Collected files keyed by path relative to the output root.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    /// <summary>
    /// The <see cref="GeneratedFileHandler"/> to hand to a generator; records each
    /// file as it is produced and completes synchronously. (Duplicate paths are
    /// already filtered upstream by the generator, so a straight assignment is safe.)
    /// </summary>
    public ValueTask WriteAsync(string relativePath, string contents, CancellationToken cancellationToken = default)
    {
        _files[relativePath] = contents;
        return ValueTask.CompletedTask;
    }
}
