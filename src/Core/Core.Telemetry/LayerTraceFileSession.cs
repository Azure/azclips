// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>Owns one layer file, a single background writer and its publication decision.</summary>
public sealed class LayerTraceFileSession
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(150)];
    private readonly object _gate = new();
    private readonly Queue<WriteWork> _writes = new();
    private readonly ITraceDirectoryProvider _directory;
    private readonly TelemetryLayer _layer;
    private readonly TelemetryLayerScopeOptions _options;
    private readonly LayerTelemetryDiagnostics _diagnostics;
    private bool _draining;
    private TaskCompletionSource? _idle;
    private bool _created;
    private bool _failed;
    private bool _aborted;
    private bool _sealed;
    private LayerTracePublicationState _publicationState;
    private int _recordCount;
    private string? _partialPath;
    private string? _publishedPath;
    private TelemetryLayerBudget? _completionBudget;

    internal LayerTraceFileSession(
        ITraceDirectoryProvider directory,
        TelemetryLayer layer,
        string fileId,
        TelemetryLayerScopeOptions options,
        LayerTelemetryDiagnostics diagnostics)
    {
        ValidateId(fileId, nameof(fileId));
        _directory = directory;
        _layer = layer;
        FileId = Guid.ParseExact(fileId, "N").ToString("N");
        _options = options;
        _diagnostics = diagnostics;
    }

    public string FileId { get; }
    public string? PartialPath => Volatile.Read(ref _partialPath);
    public string? PublishedPath => Volatile.Read(ref _publishedPath);
    public bool HasRecords { get { lock (_gate) return _recordCount > 0; } }
    public bool Failed { get { lock (_gate) return _failed; } }
    public bool Published { get { lock (_gate) return _publicationState == LayerTracePublicationState.Published; } }
    internal bool IsIdle { get { lock (_gate) return !_draining && _writes.Count == 0; } }
    internal Task<bool>? PublicationCompletion { get; private set; }

    internal static void ValidateId(string id, string parameterName)
    {
        if (id is null || id.Length != 32 || !id.All(char.IsAsciiHexDigit) ||
            !Guid.TryParseExact(id, "N", out var guid) || guid == Guid.Empty)
            throw new ArgumentException("A layer ID must be a nonempty GUID in N format.", parameterName);
    }

    internal void BeginCompletion(TelemetryLayerBudget budget) =>
        Volatile.Write(ref _completionBudget, budget);

    internal Task WaitForIdleAsync()
    {
        lock (_gate)
            return _idle?.Task ?? Task.CompletedTask;
    }

    internal Task<bool> WriteBatchAsync(IReadOnlyList<TraceEntry> entries)
    {
        if (entries.Count == 0)
            return Task.FromResult(true);
        byte[] bytes;
        try
        {
            bytes = Encoding.UTF8.GetBytes(TraceFileFormat.FormatBatch(
                entries.Cast<object>().ToArray(), _options.TimeProvider.GetUtcNow().UtcDateTime));
        }
        catch
        {
            MarkFailure("serialization_failed");
            return Task.FromResult(false);
        }

        var work = new WriteWork(bytes, entries.Count);
        bool drain;
        lock (_gate)
        {
            if (_failed || _aborted || _sealed)
                return Task.FromResult(false);
            _writes.Enqueue(work);
            drain = !_draining;
            if (drain)
            {
                _draining = true;
                _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        if (drain)
        {
            if (ExecutionContext.IsFlowSuppressed())
                _ = Task.Run(DrainWritesAsync);
            else
            {
                using (ExecutionContext.SuppressFlow())
                    _ = Task.Run(DrainWritesAsync);
            }
        }
        return work.Completion.Task;
    }

    private async Task DrainWritesAsync()
    {
        using var suppression = SuppressInstrumentationScope.Begin();
        while (true)
        {
            WriteWork work;
            bool allowed;
            lock (_gate)
            {
                if (!_writes.TryDequeue(out work!))
                {
                    _draining = false;
                    _idle!.TrySetResult();
                    return;
                }
                allowed = !_failed && !_aborted;
            }

            var succeeded = false;
            if (allowed)
            {
                try
                {
                    succeeded = await WriteWithRollbackAsync(work.Bytes).ConfigureAwait(false);
                }
                catch
                {
                    MarkFailure("write_or_close_failed");
                }
            }
            if (succeeded)
            {
                lock (_gate)
                    _recordCount += work.Count;
            }
            work.Completion.TrySetResult(succeeded);
        }
    }

    private async Task<bool> WriteWithRollbackAsync(byte[] bytes)
    {
        Stream? stream = null;
        long? originalLength = null;
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (!CanContinue())
                    return false;
                var mayHaveWritten = false;
                try
                {
                    if (stream is null)
                    {
                        // Paths and directories are resolved only for an already admitted write.
                        if (_partialPath is null)
                        {
                            var root = Path.GetFullPath(_directory.GetTraceDirectory());
                            _partialPath = Path.Combine(root, $"traces-{_layer.FileToken}-{FileId}.partial");
                            _publishedPath = Path.ChangeExtension(_partialPath, ".log");
                        }
                        if (!CanContinue())
                            return false;
                        if (!_created)
                            _options.FileSystem.CreateDirectory(Path.GetDirectoryName(_partialPath)!);
                        stream = _options.FileSystem.OpenWrite(_partialPath, createNew: !_created);
                        _created = true;
                        originalLength = stream.Length;
                    }

                    stream.Position = originalLength!.Value;
                    mayHaveWritten = true;
                    stream.Write(bytes, 0, bytes.Length);
                    _options.FileSystem.Flush(stream);
                    return true;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    if (mayHaveWritten && !TryRollback(stream!, originalLength!.Value))
                    {
                        MarkFailure("rollback_unconfirmed");
                        return false;
                    }
                    if (attempt == 2 || !await DelayRetryAsync(attempt).ConfigureAwait(false))
                    {
                        MarkFailure("write_failed");
                        return false;
                    }
                }
            }
            return false;
        }
        finally
        {
            // Success is not acknowledged until disposal confirms that the handle is closed.
            stream?.Dispose();
        }
    }

    private bool TryRollback(Stream stream, long originalLength)
    {
        try
        {
            stream.SetLength(originalLength);
            stream.Position = originalLength;
            _options.FileSystem.Flush(stream);
            return stream.Length == originalLength && stream.Position == originalLength;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> DelayRetryAsync(int attempt)
    {
        var delay = RetryDelays[attempt];
        if (!CanContinue() ||
            Volatile.Read(ref _completionBudget) is { } budget && budget.Remaining < delay)
            return false;
        _diagnostics.Report("local_io_retry");
        await Task.Delay(delay, _options.TimeProvider).ConfigureAwait(false);
        return CanContinue();
    }

    private bool CanContinue()
    {
        lock (_gate)
        {
            return !_aborted && !_failed &&
                (Volatile.Read(ref _completionBudget) is not { } budget || budget.Remaining > TimeSpan.Zero);
        }
    }

    internal void MarkFailure(string code)
    {
        bool first;
        lock (_gate)
        {
            first = !_failed;
            _failed = true;
        }
        if (first)
            _diagnostics.Report(code);
    }

    internal void Abort(string code)
    {
        lock (_gate)
            _aborted = true;
        MarkFailure(code);
    }

    internal bool TrySeal()
    {
        lock (_gate)
        {
            if (_aborted || _failed || _draining || _writes.Count != 0)
                return false;
            _sealed = true;
            return true;
        }
    }

    internal LayerTracePublicationState StopPublicationAttempts()
    {
        lock (_gate)
        {
            _aborted = true;
            return _publicationState;
        }
    }

    internal Task<bool> PublishAsync() => PublicationCompletion = Task.Run(async () =>
    {
        try
        {
            _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.BeforeRename);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                lock (_gate)
                {
                    if (!_sealed || _aborted || _failed || !_created ||
                        _completionBudget?.Remaining == TimeSpan.Zero)
                        return false;
                    // Admission and abort share this gate; the admitted OS call
                    // may finish after the caller's budget, but cannot be undone.
                    _publicationState = LayerTracePublicationState.InFlight;
                }
                try
                {
                    _options.FileSystem.Move(_partialPath!, _publishedPath!);
                    lock (_gate)
                        _publicationState = LayerTracePublicationState.Published;
                    return true;
                }
                catch (Exception error)
                {
                    lock (_gate)
                        _publicationState = LayerTracePublicationState.Failed;
                    if (error is not (IOException or UnauthorizedAccessException) ||
                        attempt == 2 || !await DelayRetryAsync(attempt).ConfigureAwait(false))
                    {
                        MarkFailure("rename_failed");
                        return false;
                    }
                }
            }
        }
        catch
        {
            MarkFailure("rename_failed");
        }
        return false;
    });

    private sealed record WriteWork(byte[] Bytes, int Count)
    {
        internal TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal enum LayerTracePublicationState
{
    NotStarted,
    InFlight,
    Failed,
    Published,
}

internal interface ILayerTraceFileSystem
{
    void CreateDirectory(string path);
    Stream OpenWrite(string path, bool createNew);
    void Flush(Stream stream);
    void Move(string source, string destination);
}

internal sealed class LayerTraceFileSystem : ILayerTraceFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public Stream OpenWrite(string path, bool createNew) =>
        new FileStream(path, createNew ? FileMode.CreateNew : FileMode.Open,
            FileAccess.Write, FileShare.Read, bufferSize: 4096, FileOptions.None);

    public void Flush(Stream stream)
    {
        if (stream is FileStream file)
            file.Flush(flushToDisk: true);
        else
            stream.Flush();
    }

    public void Move(string source, string destination) => File.Move(source, destination);
}
