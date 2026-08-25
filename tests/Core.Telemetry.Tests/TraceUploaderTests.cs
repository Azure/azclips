// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceUploaderTests
{
    [Fact]
    public void Run_DeletesFiles_WhenExportSucceeds()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", "CLI", "op-1");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void Run_LeavesFilesOnDisk_WhenExportFails()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", "CLI", "op-1");
        var sink = new FakeSink(exportSucceeds: false);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Single(Directory.GetFiles(temp.Path, "traces-*.uploading"));
    }

    [Fact]
    public void Run_CallsSinkOncePerServiceName()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", "CLI", "op-1");
        WriteTraceFile(temp.Path, "bbb", "PowerShell", "op-2");
        WriteTraceFile(temp.Path, "ccc", "CLI", "op-3");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal(
            new[] { "CLI", "PowerShell" },
            sink.Calls.Select(c => c.ServiceName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Run_FlattensEntriesAcrossFilesInTheSameGroup()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", "CLI", "op-1");
        WriteTraceFile(temp.Path, "bbb", "CLI", "op-2", "op-3");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        var call = Assert.Single(sink.Calls);
        Assert.Equal(
            new[] { "op-1", "op-2", "op-3" },
            call.Entries.Select(e => e.OperationName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Run_UsesFallbackServiceName_WhenResourceHasNone()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", null, "op-1");

        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal("AzclipsTelemetryUploader", Assert.Single(sink.Calls).ServiceName);
    }

    [Fact]
    public void Run_DeletesFile_WhenItParsesToZeroEntries()
    {
        // A file whose content has no JSON batch at all parses to zero entries.
        // The uploader should still claim it, group it under the fallback service name,
        // and delete it when export succeeds — so corrupt/empty files don't accumulate.
        using var temp = new TempDirectory();
        var filePath = Path.Combine(temp.Path, TraceFileStore.FileNameFor("empty"));
        File.WriteAllText(filePath, "not a json batch");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void Run_DoesNothing_WhenDirectoryIsEmpty()
    {
        using var temp = new TempDirectory();
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(sink.Calls);
    }

    private static void WriteTraceFile(
        string directory,
        string traceId,
        string? serviceName,
        params string[] operationNames)
    {
        var resource = serviceName == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["service.name"] = serviceName };

        var traces = operationNames
            .Select(name => (object)new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = "1111111111111111",
                ParentSpanId = "0000000000000000",
                OperationName = name,
                DisplayName = name,
                StartTime = "2024-01-01T00:00:00.0000000Z",
                Duration = 1.0,
                Status = "Unset",
                Kind = "Internal",
                Tags = Array.Empty<object>(),
                Events = Array.Empty<object>(),
                Resource = resource,
            })
            .ToList();

        File.WriteAllText(
            Path.Combine(directory, TraceFileStore.FileNameFor(traceId)),
            TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
    }

    /// <summary>Records what the uploader tried to send and returns a fixed outcome.</summary>
    private sealed class FakeSink : ITraceExportSink
    {
        private readonly bool _exportSucceeds;

        public FakeSink(bool exportSucceeds)
        {
            _exportSucceeds = exportSucceeds;
        }

        public List<(string ServiceName, IReadOnlyList<TraceEntry> Entries)> Calls { get; } = new();

        public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
        {
            Calls.Add((serviceName, entries));
            return _exportSucceeds;
        }
    }

    /// <summary>A throwaway directory removed when the test finishes.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-trace-uploader-" + Guid.NewGuid().ToString("N"));
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
