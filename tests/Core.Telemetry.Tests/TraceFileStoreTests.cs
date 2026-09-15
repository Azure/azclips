// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceFileStoreTests
{
    [Fact]
    public void FileNameFor_UsesTracesPrefixAndLogExtension()
    {
        Assert.Equal("traces-abc123.log", TraceFileStore.FileNameFor("abc123"));
    }

    [Fact]
    public void PendingFilePattern_MatchesWhatFileNameForProduces()
    {
        // Writers name files through FileNameFor, readers enumerate with PendingFilePattern;
        // this pins the two together.
        using var temp = new TempDirectory();
        var written = Path.Combine(temp.Path, TraceFileStore.FileNameFor("abc123"));
        File.WriteAllText(written, "payload");

        // Decoys isolating each half: a pattern missing the prefix matches the first, one missing
        // the extension matches the second.
        File.WriteAllText(Path.Combine(temp.Path, "other.log"), "data");
        File.WriteAllText(Path.Combine(temp.Path, "traces-abc123.uploading"), "claimed");

        var matched = Directory.GetFiles(temp.Path, TraceFileStore.PendingFilePattern);

        Assert.Equal(new[] { written }, matched);
    }

    [Fact]
    public void ClaimPending_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "azclips-missing-" + Guid.NewGuid().ToString("N"));

        var store = new TraceFileStore(missing);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_ReturnsEmpty_WhenDirectoryHasNoTraceFiles()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "unrelated.txt"), "data");

        var store = new TraceFileStore(temp.Path);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_RenamesPendingFilesToUploading_AndReturnsThem()
    {
        using var temp = new TempDirectory();
        var pending = WritePendingFile(temp.Path, "aaa", "payload");
        var store = new TraceFileStore(temp.Path);

        var claimed = store.ClaimPending();

        var claimedPath = Assert.Single(claimed);
        Assert.Equal(".uploading", Path.GetExtension(claimedPath));
        Assert.True(File.Exists(claimedPath));
        Assert.False(File.Exists(pending));
    }

    [Fact]
    public void ClaimPending_IgnoresFilesAlreadyClaimedByAnotherUploader()
    {
        using var temp = new TempDirectory();
        // A fresh .uploading file belongs to another uploader.
        File.WriteAllText(Path.Combine(temp.Path, "traces-bbb.uploading"), "payload");
        var store = new TraceFileStore(temp.Path);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_ReclaimsClaimsAbandonedByACrashedUploader()
    {
        using var temp = new TempDirectory();
        var stale = Path.Combine(temp.Path, "traces-ccc.uploading");
        File.WriteAllText(stale, "payload");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-10));
        var store = new TraceFileStore(temp.Path);

        var claimed = store.ClaimPending();

        // The sweep renames it back to .log, then this caller claims it.
        var claimedPath = Assert.Single(claimed);
        Assert.Equal("traces-ccc.uploading", Path.GetFileName(claimedPath));
        Assert.True(File.Exists(claimedPath));
    }

    [Fact]
    public void TryRead_ReturnsContentOfClaimedFile()
    {
        using var temp = new TempDirectory();
        WritePendingFile(temp.Path, "ddd", "payload");
        var store = new TraceFileStore(temp.Path);
        var claimedPath = Assert.Single(store.ClaimPending());

        Assert.True(store.TryRead(claimedPath, out var content));
        Assert.Equal("payload", content);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenFileIsMissing()
    {
        using var temp = new TempDirectory();
        var store = new TraceFileStore(temp.Path);

        Assert.False(store.TryRead(Path.Combine(temp.Path, "traces-eee.uploading"), out var content));
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void Delete_RemovesTheClaimedFile()
    {
        using var temp = new TempDirectory();
        WritePendingFile(temp.Path, "fff", "payload");
        var store = new TraceFileStore(temp.Path);
        var claimedPath = Assert.Single(store.ClaimPending());

        store.Delete(claimedPath);

        Assert.False(File.Exists(claimedPath));
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenFileIsAlreadyGone()
    {
        using var temp = new TempDirectory();
        var store = new TraceFileStore(temp.Path);

        Assert.Null(Record.Exception(
            () => store.Delete(Path.Combine(temp.Path, "traces-ggg.uploading"))));
    }

    private static string WritePendingFile(string directory, string traceId, string content)
    {
        var path = Path.Combine(directory, TraceFileStore.FileNameFor(traceId));
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A throwaway directory removed when the test finishes.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-trace-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup only.
            }
        }
    }
}
