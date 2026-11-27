namespace Extensions.KeyVault.Tests;

/// <summary>A temporary file path that is deleted on dispose.</summary>
internal sealed class TempFile : IDisposable
{
    private TempFile(string path) => Path = path;

    public string Path { get; }

    /// <summary>Creates an empty temporary file.</summary>
    public static TempFile CreateEmpty() => new(System.IO.Path.GetTempFileName());

    /// <summary>Reserves a unique temporary path without creating the file.</summary>
    public static TempFile ReservePath(string extension) => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + extension));

    public void Dispose() => File.Delete(Path);
}