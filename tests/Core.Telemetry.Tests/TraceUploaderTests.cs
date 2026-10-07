// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

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

        Assert.Empty(Directory.GetFiles(temp.Path, "traces-*"));
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
    public void Run_CallsSinkOncePerServiceAndDataBoundary()
    {
        using var temp = new TempDirectory();
        WriteTraceFileWithBoundary(temp.Path, "aaa", "CLI", "EU", "op-1");
        WriteTraceFileWithBoundary(temp.Path, "bbb", "CLI", "Global", "op-2");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal(
            new[] { "EU", "Global" },
            sink.Calls.Select(c => c.DataBoundary).OrderBy(n => n, StringComparer.Ordinal));
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
    public void Run_DeletesMixedBoundaryFile_OnlyWhenAllGroupsExport()
    {
        using var temp = new TempDirectory();
        WriteMixedBoundaryTraceFile(temp.Path, "aaa", "CLI");
        var sink = new FakeSink(boundaryFailures: ["EU"]);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Single(Directory.GetFiles(temp.Path, "traces-*.uploading"));
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

        Assert.Empty(Directory.GetFiles(temp.Path, "traces-*"));
    }

    [Fact]
    public void Run_DoesNothing_WhenDirectoryIsEmpty()
    {
        using var temp = new TempDirectory();
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(sink.Calls);
    }

    [Fact]
    public void ManagedLayer_UploadsWithAgentResourceAndTypedProfile()
    {
        using var temp = new TempDirectory();
        WriteManagedFile(temp.Path);
        var sink = new FakeSink();

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        var call = Assert.Single(sink.Calls);
        Assert.Equal("Agent", call.ServiceName);
        Assert.Equal("EU", call.DataBoundary);
        Assert.Equal(TraceReplayProfile.ManagedLayer, call.Profile);
        Assert.Equal(2, call.Entries.Count);
        Assert.Empty(Directory.GetFiles(temp.Path, "traces-*"));
    }

    [Fact]
    public void ManagedLayer_RejectsMixedSourcesAndDoesNotRetryRejectedFile()
    {
        using var temp = new TempDirectory();
        WriteManagedFile(temp.Path, secondService: "CLI");
        var sink = new FakeSink();
        var uploader = new TraceUploader(new TraceFileStore(temp.Path), sink);

        uploader.Run();
        uploader.Run();

        Assert.Empty(sink.Calls);
        Assert.Single(Directory.GetFiles(temp.Path, "*.rejected"));
        Assert.Single(Directory.GetFiles(temp.Path, "*.diagnostic.json"));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.uploading"));
    }

    [Fact]
    public void ManagedLayer_DoesNotSalvageTruncatedBatchesAsComplete()
    {
        using var temp = new TempDirectory();
        var file = WriteManagedFile(temp.Path);
        File.AppendAllText(file, "\n--- Batch at 2024-01-01T00:00:00.0000000Z ---\n[{");
        var sink = new FakeSink();

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(sink.Calls);
        Assert.Single(Directory.GetFiles(temp.Path, "*.rejected"));
    }

    [Fact]
    public void ReplayProfiles_AreNotCombinedEvenForTheSameResource()
    {
        using var temp = new TempDirectory();
        WriteManagedFile(temp.Path, boundary: "Global");
        WriteTraceFile(temp.Path, "legacy", "Agent", "legacy");
        var sink = new FakeSink();

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal(2, sink.Calls.Count);
        Assert.Contains(sink.Calls, call => call.Profile == TraceReplayProfile.Legacy);
        Assert.Contains(sink.Calls, call => call.Profile == TraceReplayProfile.ManagedLayer);
    }

    [Fact]
    public void Reservation_HoldsBothFilesUntilAgentAndCliAreReady_ThenDeletesThemTogether()
    {
        using var temp = new TempDirectory();
        const string traceId = "0123456789abcdef0123456789abcdef";
        var invocationId = Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(temp.Path);
        reservations.Begin(traceId, invocationId);
        WriteTraceFile(temp.Path, traceId, "CLI", "command");
        WriteManagedAgentFile(temp.Path, traceId, invocationId);

        var sink = new FakeSink();
        var uploader = new TraceUploader(new TraceFileStore(temp.Path), sink);
        uploader.Run();
        Assert.Empty(sink.Calls);
        Assert.Equal(2, Directory.GetFiles(temp.Path, "traces-*.log").Length);

        reservations.MarkReady(invocationId);
        uploader.Run();

        Assert.Equal(2, sink.Calls.Count);
        Assert.Empty(Directory.GetFiles(temp.Path, "traces-*"));
    }

    [Fact]
    public void Reservation_KeepsBothClaimedFilesWhenAnyGroupFails()
    {
        using var temp = new TempDirectory();
        const string traceId = "0123456789abcdef0123456789abcdef";
        var invocationId = Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(temp.Path);
        reservations.Begin(traceId, invocationId);
        WriteTraceFile(temp.Path, traceId, "CLI", "command");
        WriteManagedAgentFile(temp.Path, traceId, invocationId);
        reservations.MarkReady(invocationId);

        new TraceUploader(new TraceFileStore(temp.Path), new FakeSink(exportSucceeds: false)).Run();

        Assert.Equal(2, Directory.GetFiles(temp.Path, "traces-*.uploading").Length);
        Assert.Single(Directory.GetFiles(temp.Path, "traces-hold-*.json"));
    }

    [Fact]
    public void Reservation_ExcludesUnpublishedAgentFileAndReleasesCliFile()
    {
        using var temp = new TempDirectory();
        const string traceId = "0123456789abcdef0123456789abcdef";
        var invocationId = Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(temp.Path);
        reservations.Begin(traceId, invocationId);
        WriteTraceFile(temp.Path, traceId, "CLI", "command");
        File.WriteAllText(Path.Combine(temp.Path, $"traces-agent-{invocationId}.partial"), "partial");
        reservations.MarkReady(invocationId);
        var sink = new FakeSink();

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Single(sink.Calls);
        Assert.Equal(
            [$"traces-agent-{invocationId}.partial"],
            Directory.GetFiles(temp.Path, "traces-*").Select(Path.GetFileName));
    }

    [Fact]
    public void Reservation_RejectsBothArtifactsWhenOneArtifactIsInvalid()
    {
        using var temp = new TempDirectory();
        const string traceId = "0123456789abcdef0123456789abcdef";
        var invocationId = Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(temp.Path);
        reservations.Begin(traceId, invocationId);
        WriteTraceFile(temp.Path, traceId, "CLI", "command");
        WriteManagedAgentFile(temp.Path, traceId, invocationId);
        File.AppendAllText(
            Path.Combine(temp.Path, $"traces-agent-{invocationId}.log"),
            "\n--- Batch at 2024-01-01T00:00:00.0000000Z ---\n[{");
        reservations.MarkReady(invocationId);

        var sink = new FakeSink();
        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(sink.Calls);
        Assert.Equal(2, Directory.GetFiles(temp.Path, "*.rejected").Length);
        Assert.Empty(Directory.GetFiles(temp.Path, "traces-hold-*.json"));
    }

    [Fact]
    public void Run_WithCyclicGroupDependencies_ReplaysParentsBeforeChildren()
    {
        using var temp = new TempDirectory();
        WriteSpan(temp.Path, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "CLI",
            "1111111111111111", "0000000000000000", "cli-root-a");
        WriteManagedSpan(temp.Path, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "2222222222222222", "1111111111111111", "Agent.Session");
        WriteManagedSpan(temp.Path, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "3333333333333333", "0000000000000000", "Agent.Session");
        WriteSpan(temp.Path, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "CLI",
            "4444444444444444", "3333333333333333", "cli-child-b");

        var sink = new FakeSink();
        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal(
            new[] { "Agent.Session", "cli-root-a", "Agent.Session", "cli-child-b" },
            sink.Calls.Select(call => Assert.Single(call.Entries).OperationName));
    }

    private static string WriteManagedFile(string directory, string secondService = "Agent", string boundary = "EU")
    {
        var invocation = Guid.NewGuid().ToString("N");
        var records = new List<object>();
        for (var index = 0; index < 2; index++)
        {
            var service = index == 0 ? "Agent" : secondService;
            records.Add(new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = index == 0 ? "1111111111111111" : "2222222222222222",
                ParentSpanId = index == 0 ? "0000000000000000" : "1111111111111111",
                OperationName = index == 0 ? "Agent.Session" : "tool.get_command",
                DisplayName = index == 0 ? "Agent.Session" : "tool.get_command",
                StartTime = "2024-01-01T00:00:00.0000000Z",
                Duration = 2,
                Status = "Unset",
                Kind = index == 0 ? "Server" : "Internal",
                DataBoundary = boundary,
                Tags = new Dictionary<string, object>
                {
                    [TelemetryLayerTags.Schema] = "agent-v1",
                    [TelemetryLayerTags.ServiceName] = service,
                    [TelemetryLayerTags.InvocationId] = invocation,
                    [TelemetryTags.DataBoundary] = boundary,
                }.ToArray(),
                Events = Array.Empty<object>(),
                Resource = new Dictionary<string, string> { ["service.name"] = service },
            });
        }

        var path = Path.Combine(directory, $"traces-agent-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, TraceFileFormat.FormatBatch(records, DateTime.UtcNow));
        return path;
    }

    private static void WriteManagedAgentFile(string directory, string traceId, string invocationId)
    {
        var record = new
        {
            TraceId = traceId,
            SpanId = "2222222222222222",
            ParentSpanId = "1111111111111111",
            OperationName = "Agent.Session",
            DisplayName = "Agent.Session",
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1.0,
            Status = "Unset",
            Kind = "Server",
            DataBoundary = "Global",
            Tags = new Dictionary<string, object>
            {
                [TelemetryLayerTags.Schema] = "agent-v1",
                [TelemetryLayerTags.ServiceName] = "Agent",
                [TelemetryLayerTags.InvocationId] = invocationId,
                [TelemetryTags.DataBoundary] = "Global",
            }.ToArray(),
            Events = Array.Empty<object>(),
            Resource = new Dictionary<string, string> { ["service.name"] = "Agent" },
        };
        File.WriteAllText(Path.Combine(directory, $"traces-agent-{invocationId}.log"),
            TraceFileFormat.FormatBatch([record], DateTime.UtcNow));
    }

    private static void WriteSpan(
        string directory,
        string traceId,
        string serviceName,
        string spanId,
        string parentSpanId,
        string operationName)
    {
        var record = new
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            OperationName = operationName,
            DisplayName = operationName,
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1.0,
            Status = "Unset",
            Kind = "Internal",
            DataBoundary = "Global",
            Tags = Array.Empty<object>(),
            Events = Array.Empty<object>(),
            Resource = new Dictionary<string, string> { ["service.name"] = serviceName },
        };
        var path = Path.Combine(directory, TraceFileStore.FileNameFor(traceId));
        var batch = TraceFileFormat.FormatBatch([record], DateTime.UtcNow);
        File.AppendAllText(path, batch);
    }

    private static void WriteManagedSpan(
        string directory,
        string traceId,
        string spanId,
        string parentSpanId,
        string operationName)
    {
        var invocationId = Guid.NewGuid().ToString("N");
        var boundary = "Global";
        var record = new
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            OperationName = operationName,
            DisplayName = operationName,
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1.0,
            Status = "Unset",
            Kind = "Server",
            DataBoundary = boundary,
            Tags = new Dictionary<string, object>
            {
                [TelemetryLayerTags.Schema] = "agent-v1",
                [TelemetryLayerTags.ServiceName] = "Agent",
                [TelemetryLayerTags.InvocationId] = invocationId,
                [TelemetryTags.DataBoundary] = boundary,
            }.ToArray(),
            Events = Array.Empty<object>(),
            Resource = new Dictionary<string, string> { ["service.name"] = "Agent" },
        };
        File.WriteAllText(
            Path.Combine(directory, $"traces-agent-{invocationId}.log"),
            TraceFileFormat.FormatBatch([record], DateTime.UtcNow));
    }

    private static void WriteTraceFile(
        string directory,
        string traceId,
        string? serviceName,
        params string[] operationNames)
    {
        WriteTraceFileCore(directory, traceId, serviceName, null, operationNames);
    }

    private static void WriteTraceFileWithBoundary(
        string directory,
        string traceId,
        string? serviceName,
        string? dataBoundary,
        params string[] operationNames)
    {
        WriteTraceFileCore(directory, traceId, serviceName, dataBoundary, operationNames);
    }

    private static void WriteTraceFileCore(
        string directory,
        string traceId,
        string? serviceName,
        string? dataBoundary,
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
                TenantId = "tenant-1",
                DataBoundary = dataBoundary,
                Tags = Array.Empty<object>(),
                Events = Array.Empty<object>(),
                Resource = resource,
            })
            .ToList();

        File.WriteAllText(
            Path.Combine(directory, TraceFileStore.FileNameFor(traceId)),
            TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
    }

    private static void WriteMixedBoundaryTraceFile(string directory, string traceId, string serviceName)
    {
        var resource = new Dictionary<string, string> { ["service.name"] = serviceName };
        var traces = new List<object>
        {
            new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = "1111111111111111",
                ParentSpanId = "0000000000000000",
                OperationName = "op-eu",
                DisplayName = "op-eu",
                StartTime = "2024-01-01T00:00:00.0000000Z",
                Duration = 1.0,
                Status = "Unset",
                Kind = "Internal",
                DataBoundary = "EU",
                Tags = Array.Empty<object>(),
                Events = Array.Empty<object>(),
                Resource = resource,
            },
            new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = "2222222222222222",
                ParentSpanId = "0000000000000000",
                OperationName = "op-global",
                DisplayName = "op-global",
                StartTime = "2024-01-01T00:00:00.0000000Z",
                Duration = 1.0,
                Status = "Unset",
                Kind = "Internal",
                DataBoundary = "Global",
                Tags = Array.Empty<object>(),
                Events = Array.Empty<object>(),
                Resource = resource,
            },
        };

        File.WriteAllText(
            Path.Combine(directory, TraceFileStore.FileNameFor(traceId)),
            TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
    }

    /// <summary>Records what the uploader tried to send and returns a fixed outcome.</summary>
    private sealed class FakeSink : ITraceExportSink
    {
        private readonly bool _exportSucceeds;
        private readonly HashSet<string> _boundaryFailures;

        public FakeSink(bool exportSucceeds = true, IEnumerable<string>? boundaryFailures = null)
        {
            _exportSucceeds = exportSucceeds;
            _boundaryFailures = new HashSet<string>(boundaryFailures ?? [], StringComparer.OrdinalIgnoreCase);
        }

        public List<(string ServiceName, string? DataBoundary, IReadOnlyList<TraceEntry> Entries, TraceReplayProfile Profile)> Calls { get; } = new();

        public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries, string? dataBoundary = null,
            TraceReplayProfile profile = TraceReplayProfile.Legacy, TraceReplayContext? replayContext = null)
        {
            Calls.Add((serviceName, dataBoundary, entries, profile));
            return _exportSucceeds && !_boundaryFailures.Contains(dataBoundary ?? string.Empty);
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
