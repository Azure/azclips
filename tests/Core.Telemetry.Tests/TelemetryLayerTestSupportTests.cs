// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

internal sealed class TelemetryLayerTestEnvironment : IAsyncDisposable
{
    private readonly List<LayerTelemetryScope> _scopes = [];
    internal static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    internal TelemetryLayerTestEnvironment(
        Func<TelemetryLayerManualTimeProvider, TelemetryLayerScopeOptions>? options = null,
        TelemetryLayer? layer = null)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "azclips.slnx")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("The test must run beneath the repository.");
        Root = Path.Combine(directory.FullName, "artifacts", "telemetry-layer-tests", Guid.NewGuid().ToString("N"));
        Layer = layer ?? new TelemetryLayer("TestLayer" + Guid.NewGuid().ToString("N"), "testlayer", "test-v1",
            ["Test.Root", "Test.Child", "Test.Other"], ["ProfileService."]);
        Options = options?.Invoke(Clock) ?? new TelemetryLayerScopeOptions { TimeProvider = Clock, Diagnostic = _ => { } };
        Factory = NewFactory(Layer);
    }

    internal string Root { get; }
    internal TelemetryLayerManualTimeProvider Clock { get; } = new();
    internal TelemetryLayer Layer { get; }
    internal TelemetryLayerScopeOptions Options { get; }
    internal TelemetryLayerConfiguration Configuration { get; } = new();
    internal TelemetryLayerScopeFactory Factory { get; }
    internal TelemetryInvocationContext Context(bool enabled = true) => new(
        Guid.NewGuid().ToString("N"), enabled,
        "11111111111111111111111111111111", "2222222222222222",
        "resource show", "tenant-test", "subscription-test", "EU");

    internal TelemetryLayerScopeFactory NewFactory(TelemetryLayer layer) => new(
        new FixedDirectory(Root), Configuration, new TelemetryLayerCatalog([layer]), Options);

    internal LayerTelemetryScope Create(
        TelemetryInvocationContext? context = null,
        TelemetryLayerScopeFactory? factory = null,
        string? layer = null,
        string? fileId = null)
    {
        var scope = (factory ?? Factory).CreateScope(layer ?? Layer.ServiceName, context ?? Context(), fileId);
        _scopes.Add(scope);
        return scope;
    }

    internal List<TraceEntry> Read(LayerTelemetryScope scope)
    {
        Assert.NotNull(scope.FileSession.PublishedPath);
        var content = File.ReadAllText(scope.FileSession.PublishedPath);
        Assert.True(TraceFileFormat.TryParseComplete(content, out var entries));
        return entries;
    }

    internal static JsonElement Tag(TraceEntry entry, string name) =>
        entry.Tags.EnumerateArray().Single(tag => tag.GetProperty("Key").GetString() == name).GetProperty("Value");

    internal async Task CompleteForCleanupAsync(LayerTelemetryScope scope, Activity? root = null)
    {
        var completion = scope.CompleteAsync(root);
        if (!completion.IsCompleted)
        {
            Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
            await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
            if (!completion.IsCompleted)
                Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        }
        await completion.WaitAsync(TestTimeout);
        await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
            await CompleteForCleanupAsync(scope);
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private sealed class FixedDirectory(string path) : ITraceDirectoryProvider
    {
        public string GetTraceDirectory() => path;
    }
}

internal sealed class TelemetryLayerConfiguration : IConfigurationService
{
    internal bool Enabled { get; set; } = true;
    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new() =>
        typeof(T) == typeof(TelemetryOptions) ? (T)(object)new TelemetryOptions { Enabled = Enabled } : defaultValue ?? new T();
    public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public string GetConfigFilePath() => throw new NotSupportedException();
    public IConfigurationSchema GetSchema() => throw new NotSupportedException();
}

internal sealed class TelemetryLayerBarrier : IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Hit()
    {
        Entered.TrySetResult();
        if (!_release.Wait(TelemetryLayerTestEnvironment.TestTimeout))
            throw new TimeoutException("A test barrier was not released.");
    }

    internal void Release() => _release.Set();

    public void Dispose()
    {
        Release();
        // A just-released writer may still be returning from Wait.
    }
}

internal sealed class TelemetryLayerManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly DateTimeOffset _origin = DateTimeOffset.UtcNow;
    private readonly HashSet<ManualTimer> _timers = [];
    private TaskCompletionSource _timerChanged = NewSignal();
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override DateTimeOffset GetUtcNow() { lock (_gate) return _origin.AddTicks(_ticks); }
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    internal void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _ticks += duration.Ticks;
            due = _timers.Where(timer => timer.Due <= _ticks).ToList();
            foreach (var timer in due)
            {
                if (timer.Period == Timeout.InfiniteTimeSpan)
                    _timers.Remove(timer);
                else
                    timer.Due = _ticks + timer.Period.Ticks;
            }
        }
        foreach (var timer in due)
            timer.Fire();
    }

    internal async Task WaitForDelayAsync(TimeSpan delay)
    {
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_timers.Any(timer => timer.Due - _ticks == delay.Ticks))
                    return;
                changed = _timerChanged.Task;
            }
            await changed.WaitAsync(TelemetryLayerTestEnvironment.TestTimeout);
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ManualTimer(
        TelemetryLayerManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;
        internal long Due { get; set; }
        internal TimeSpan Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (_disposed)
                    return false;
                owner._timers.Remove(this);
                Period = period;
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    Due = owner._ticks + dueTime.Ticks;
                    owner._timers.Add(this);
                }
                var changed = owner._timerChanged;
                owner._timerChanged = NewSignal();
                changed.TrySetResult();
                return true;
            }
        }

        internal void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner._gate)
            {
                _disposed = true;
                owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class TelemetryLayerTestFileSystem : ILayerTraceFileSystem
{
    private readonly LayerTraceFileSystem _real = new();
    private int _directories;
    private int _opens;
    private int _moves;
    internal int Directories => Volatile.Read(ref _directories);
    internal int Opens => Volatile.Read(ref _opens);
    internal int Moves => Volatile.Read(ref _moves);
    internal Func<Stream, Stream>? Wrap { get; init; }
    internal Action? BeforeMove { get; init; }

    public void CreateDirectory(string path)
    {
        Interlocked.Increment(ref _directories);
        _real.CreateDirectory(path);
    }

    public Stream OpenWrite(string path, bool createNew)
    {
        Interlocked.Increment(ref _opens);
        var stream = _real.OpenWrite(path, createNew);
        return Wrap?.Invoke(stream) ?? stream;
    }

    public void Flush(Stream stream) => _real.Flush(stream);

    public void Move(string source, string destination)
    {
        Interlocked.Increment(ref _moves);
        BeforeMove?.Invoke();
        _real.Move(source, destination);
    }
}

internal sealed class TelemetryLayerFaultStream(Stream inner) : Stream
{
    internal Action<Stream, byte[], int, int>? WriteAction { get; init; }
    internal Action? BeforeClose { get; init; }
    internal bool FailRollback { get; init; }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value)
    {
        if (FailRollback)
            throw new IOException("Test rollback failure");
        inner.SetLength(value);
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (WriteAction is not null)
            WriteAction(inner, buffer, offset, count);
        else
            inner.Write(buffer, offset, count);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BeforeClose?.Invoke();
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
