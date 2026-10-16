// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

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
    public void ActiveUploader_PreventsASecondUploaderFromReclaimingItsOldFiles()
    {
        using var temp = new TempDirectory();
        var stale = Path.Combine(temp.Path, "traces-ccc.uploading");
        File.WriteAllText(stale, "payload");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-10));
        var store = new TraceFileStore(temp.Path);
        using (var active = store.TryStartUpload())
        {
            Assert.NotNull(active);
            new TraceUploader(store, new NoExportExpected()).Run();
            Assert.True(File.Exists(stale));
        }
        using var next = store.TryStartUpload();
        Assert.NotNull(next);
        Assert.Single(store.ClaimPending());
    }

    [Fact]
    public async Task ClaimPending_CannotOutrunAConcurrentReservation()
    {
        using var temp = new TempDirectory();
        const string traceId = "11111111111111111111111111111111";
        var invocationId = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(temp.Path,
            $"traces-hold-22222222222222222222222222222222-{Guid.NewGuid():N}.json"), "{}");
        var reservations = new TraceUploadReservationStore(temp.Path);
        var store = new TraceFileStore(temp.Path);
        using var barrier = new ReservationScanBarrier();
        var originalError = Console.Error;
        Console.SetError(barrier);
        var claiming = Task.Run(() => store.ClaimPending());
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var reserved = false;
            try
            {
                reservations.Begin(traceId, invocationId);
                WritePendingFile(temp.Path, traceId, "command");
                reserved = true;
            }
            catch (IOException)
            {
                Assert.True(File.Exists(Path.Combine(temp.Path, ".upload.lock")));
            }
            finally
            {
                barrier.Release.Set();
            }

            Assert.Empty(await claiming.WaitAsync(TimeSpan.FromSeconds(10)));
            if (!reserved)
            {
                reservations.Begin(traceId, invocationId);
                WritePendingFile(temp.Path, traceId, "command");
            }
            Assert.Empty(store.ClaimPending());
            Assert.True(File.Exists(Path.Combine(temp.Path, TraceFileStore.FileNameFor(traceId))));
        }
        finally
        {
            barrier.Release.Set();
            await claiming.WaitAsync(TimeSpan.FromSeconds(10));
            Console.SetError(originalError);
        }
    }

    private const string ReservedTraceId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ClaimPending_DeletesAnOldReadyReservationWhoseTraceFileNeverAppeared()
    {
        using var temp = new TempDirectory();
        var reservation = WriteReadyReservation(temp.Path, DateTime.UtcNow.AddMinutes(-10));

        Assert.Empty(new TraceFileStore(temp.Path).ClaimPending());

        Assert.False(File.Exists(reservation));
    }

    [Fact]
    public void ClaimPending_KeepsARecentReadyReservationWhoseTraceFileIsMissing()
    {
        using var temp = new TempDirectory();
        var reservation = WriteReadyReservation(temp.Path, DateTime.UtcNow.AddMinutes(-1));

        Assert.Empty(new TraceFileStore(temp.Path).ClaimPending());

        Assert.True(File.Exists(reservation));
    }

    [Fact]
    public void ClaimPending_KeepsAnOldReservationWhileItsTraceFileIsBeingUploaded()
    {
        using var temp = new TempDirectory();
        var reservation = WriteReadyReservation(temp.Path, DateTime.UtcNow.AddMinutes(-10));
        // A live uploader's claim: fresh, so the stale-claim sweep leaves it alone.
        var uploading = Path.Combine(temp.Path, $"traces-{ReservedTraceId}.uploading");
        File.WriteAllText(uploading, "payload");

        Assert.Empty(new TraceFileStore(temp.Path).ClaimPending());

        Assert.True(File.Exists(reservation));
        Assert.True(File.Exists(uploading));
    }

    [Fact]
    public void ClaimPending_ReleasesTheSurvivingFileOfAnExpiredReservation()
    {
        using var temp = new TempDirectory();
        var invocationId = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(temp.Path, $"traces-agent-{invocationId}.log"), "payload");
        var reservation = WriteReadyReservation(temp.Path, DateTime.UtcNow.AddMinutes(-10), invocationId);
        var store = new TraceFileStore(temp.Path);

        Assert.Empty(store.ClaimPending());
        Assert.False(File.Exists(reservation));

        var claimed = Assert.Single(store.ClaimPending());
        Assert.Equal($"traces-agent-{invocationId}.uploading", Path.GetFileName(claimed));
    }

    /// <summary>
    /// Writes a ready reservation for <see cref="ReservedTraceId"/> with no trace file beside it,
    /// last written at <paramref name="lastWritten"/>.
    /// </summary>
    private static string WriteReadyReservation(string directory, DateTime lastWritten, string? invocationId = null)
    {
        invocationId ??= Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(directory);
        reservations.Begin(ReservedTraceId, invocationId);
        reservations.MarkReady(invocationId);
        var path = Assert.Single(Directory.GetFiles(directory, $"traces-hold-*-{invocationId}.json"));
        File.SetLastWriteTimeUtc(path, lastWritten);
        return path;
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

    private sealed class ReservationScanBarrier : TextWriter
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            if (value != "Telemetry upload reservation is invalid; related files remain held.")
                return;
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The reservation scan barrier was not released.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Release.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class NoExportExpected : ITraceExportSink
    {
        public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries,
            string? dataBoundary = null, TraceReplayProfile profile = TraceReplayProfile.Legacy,
            TraceReplayContext? replayContext = null) =>
            throw new InvalidOperationException("A second uploader must not export an active claim.");
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
